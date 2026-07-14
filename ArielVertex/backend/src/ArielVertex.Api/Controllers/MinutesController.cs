using System.Globalization;
using ArielVertex.Api.Common;
using ArielVertex.Api.Security;
using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Contracts;
using ArielVertex.Application.Security;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Auth;
using ArielVertex.Infrastructure.Integration;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ArielVertex.Api.Controllers;

/// <summary>
/// Meeting minutes for Project Coordinators and HR: capture notes → auto-generate minutes → preview →
/// approve → send to all attendees. Corrections re-run the preview and can be re-sent. Grammar is
/// corrected automatically by the generator.
/// </summary>
[Authorize]
[RequireFeature("minutes")]
[Capability(Permissions.MinutesManage)]
[Route("api/v1/minutes")]
public class MinutesController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IMinutesGenerator _generator;
    private readonly IPlatformConfig _config;
    private readonly INotificationService _notify;
    private readonly IAuditService _audit;
    private readonly IntegrationSettings _integration;
    private readonly AzureAdSettings _azure;
    private readonly MicrosoftGraphClient _graph;

    public MinutesController(AppDbContext db, ICurrentUser me, IMinutesGenerator generator, IPlatformConfig config,
        INotificationService notify, IAuditService audit, IOptions<IntegrationSettings> integration,
        IOptions<AzureAdSettings> azure, MicrosoftGraphClient graph)
    {
        _db = db; _me = me; _generator = generator; _config = config; _notify = notify; _audit = audit;
        _integration = integration.Value; _azure = azure.Value; _graph = graph;
    }

    /// <summary>The minutes the current user owns (each PC/HR sees the ones they captured).</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var q = _db.MeetingMinutes.Include(m => m.CreatedBy).Include(m => m.ApprovedBy)
            .Where(m => m.CreatedById == _me.Id).AsNoTracking().AsQueryable();
        if (Enum.TryParse<MeetingMinuteStatus>(status, true, out var st)) q = q.Where(m => m.Status == st);
        var list = await q.OrderByDescending(m => m.CreatedAt).ToListAsync();
        return Ok(list.Select(m => m.ToDto(true)));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var m = await Owned(id);
        if (m is null) return Missing();
        return Ok(m.ToDto(true));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMinutesRequest req)
    {
        var m = new MeetingMinute
        {
            Title = req.Title.Trim(),
            MeetingDate = req.MeetingDate == default ? DateTime.UtcNow : req.MeetingDate.ToUniversalTime(),
            Location = req.Location?.Trim(),
            Attendees = NormalizeAttendees(req.Attendees),
            RawNotes = req.RawNotes.Trim(),
            Status = MeetingMinuteStatus.Draft,
            CreatedById = _me.Id
        };
        _db.MeetingMinutes.Add(m);
        await _db.SaveChangesAsync();
        return Ok(new { m.Id });
    }

    /// <summary>Auto-generate the minutes from the notes and move to Preview for the organiser to review.</summary>
    [HttpPost("{id:int}/generate")]
    public async Task<IActionResult> Generate(int id)
    {
        var m = await Owned(id, tracking: true);
        if (m is null) return Missing();
        if (string.IsNullOrWhiteSpace(m.RawNotes)) return BadInput("Add some meeting notes before generating minutes.");
        if (m.Status == MeetingMinuteStatus.Sent) return BadInput("These minutes were already sent. Edit them to make corrections, then re-send.");

        var draft = await _generator.GenerateAsync(m.Title, m.MeetingDate, m.Location,
            Mappers.SplitAttendees(m.Attendees), m.RawNotes);
        m.MinutesText = draft.MinutesText;
        m.GeneratedByAi = draft.UsedAi;
        m.Status = MeetingMinuteStatus.Preview;
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.MinutesGenerated, "MeetingMinute", m.Id,
            $"Minutes generated for '{m.Title}' ({(draft.UsedAi ? "AI" : "built-in")}).");
        return Ok(m.ToDto(true));
    }

    /// <summary>Save corrections to the notes, the generated minutes, or the meeting details.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMinutesRequest req)
    {
        var m = await Owned(id, tracking: true);
        if (m is null) return Missing();
        m.Title = req.Title.Trim();
        m.MeetingDate = req.MeetingDate == default ? m.MeetingDate : req.MeetingDate.ToUniversalTime();
        m.Location = req.Location?.Trim();
        m.Attendees = NormalizeAttendees(req.Attendees);
        if (req.RawNotes is not null) m.RawNotes = req.RawNotes.Trim();
        if (req.MinutesText is not null) m.MinutesText = req.MinutesText;
        // Editing an approved/sent set of minutes sends it back to Preview so it must be re-approved.
        if (m.Status is MeetingMinuteStatus.Approved or MeetingMinuteStatus.Sent && !string.IsNullOrWhiteSpace(m.MinutesText))
        {
            m.Status = MeetingMinuteStatus.Preview;
            m.ApprovedById = null; m.ApprovedAt = null;
        }
        await _db.SaveChangesAsync();
        return Ok(m.ToDto(true));
    }

    /// <summary>Approve the preview so it can be sent to attendees.</summary>
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        var m = await Owned(id, tracking: true);
        if (m is null) return Missing();
        if (string.IsNullOrWhiteSpace(m.MinutesText)) return BadInput("Generate the minutes before approving.");
        m.Status = MeetingMinuteStatus.Approved; m.ApprovedById = _me.Id; m.ApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.MinutesApproved, "MeetingMinute", m.Id, $"Minutes approved for '{m.Title}'.");
        return Ok(m.ToDto(true));
    }

    /// <summary>Send the approved minutes to every attendee (portal notification + Outlook email when live).</summary>
    [HttpPost("{id:int}/send")]
    public async Task<IActionResult> Send(int id)
    {
        var m = await Owned(id, tracking: true);
        if (m is null) return Missing();
        if (m.Status != MeetingMinuteStatus.Approved && m.Status != MeetingMinuteStatus.Sent)
            return BadInput("Approve the minutes before sending.");
        if (string.IsNullOrWhiteSpace(m.MinutesText)) return BadInput("There are no minutes to send.");

        var emails = Mappers.SplitAttendees(m.Attendees);
        if (emails.Length == 0) return BadInput("Add at least one attendee to send the minutes to.");

        var tokens = new Dictionary<string, string>
        {
            ["title"] = m.Title,
            ["date"] = m.MeetingDate.ToString("dddd, dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture),
            ["minutes"] = m.MinutesText
        };
        var (subject, body) = await _config.RenderAsync("minutes.sent", tokens);
        if (string.IsNullOrWhiteSpace(subject)) subject = $"Minutes of meeting — {m.Title}";
        if (string.IsNullOrWhiteSpace(body)) body = $"Please find the minutes of '{m.Title}' below.\n\n{m.MinutesText}";

        // Internal attendees get a portal notification.
        var internalUsers = await _db.Users.Where(u => emails.Contains(u.Email)).ToListAsync();
        if (internalUsers.Count > 0)
            await _notify.NotifyManyAsync(internalUsers.Select(u => u.Id), NotificationType.General, subject, body, "/meetings");

        // Everyone (internal + external) gets an Outlook email when delivery is live.
        int emailed = 0;
        if (_integration.OutlookNotificationsLive && _azure.IsConfigured)
        {
            var html = "<pre style=\"font-family:inherit;white-space:pre-wrap\">" + System.Net.WebUtility.HtmlEncode(body) + "</pre>";
            foreach (var email in emails)
            {
                try { await _graph.SendMailAsync(email, subject, html); emailed++; } catch { /* best-effort */ }
            }
        }

        m.Status = MeetingMinuteStatus.Sent; m.SentAt = DateTime.UtcNow; m.SentCount++;
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.MinutesSent, "MeetingMinute", m.Id,
            $"Minutes for '{m.Title}' sent to {emails.Length} attendee(s).");
        return Ok(new { ok = true, attendees = emails.Length, portalNotified = internalUsers.Count, emailed, minutes = m.ToDto(true) });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var m = await Owned(id, tracking: true);
        if (m is null) return Missing();
        _db.MeetingMinutes.Remove(m);
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    // ---- helpers ----
    private async Task<MeetingMinute?> Owned(int id, bool tracking = false)
    {
        var q = _db.MeetingMinutes.Include(m => m.CreatedBy).Include(m => m.ApprovedBy).AsQueryable();
        if (!tracking) q = q.AsNoTracking();
        var m = await q.FirstOrDefaultAsync(m => m.Id == id);
        // Ownership: only the organiser who captured the minutes may touch them.
        return m is null || m.CreatedById != _me.Id ? null : m;
    }

    private static string NormalizeAttendees(string? raw) => string.Join(", ", Mappers.SplitAttendees(raw));
}
