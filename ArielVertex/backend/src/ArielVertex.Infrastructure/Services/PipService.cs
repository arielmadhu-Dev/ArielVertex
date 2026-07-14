using System.Globalization;
using ArielVertex.Application.Abstractions;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Auth;
using ArielVertex.Infrastructure.Integration;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ArielVertex.Infrastructure.Services;

public class PipService : IPipService
{
    private readonly AppDbContext _db;
    private readonly IPlatformConfig _config;
    private readonly INotificationService _notify;
    private readonly IAuditService _audit;
    private readonly IntegrationSettings _integration;
    private readonly AzureAdSettings _azure;
    private readonly MicrosoftGraphClient _graph;

    public PipService(AppDbContext db, IPlatformConfig config, INotificationService notify, IAuditService audit,
        IOptions<IntegrationSettings> integration, IOptions<AzureAdSettings> azure, MicrosoftGraphClient graph)
    { _db = db; _config = config; _notify = notify; _audit = audit; _integration = integration.Value; _azure = azure.Value; _graph = graph; }

    public async Task<int?> EvaluateAndTriggerAsync(int subjectUserId, decimal scorePercent, string period, int? actorId, CancellationToken ct = default)
    {
        if (!await _config.GetBoolAsync("pip.enabled", true, ct)) return null;
        var threshold = await _config.GetIntAsync("pip.thresholdPercent", 50, ct);
        if (scorePercent >= threshold) return null;

        // Don't stack PIPs — skip if the employee already has an open/in-progress one.
        var active = await _db.Pips.AnyAsync(p => p.SubjectUserId == subjectUserId &&
            (p.Status == PipStatus.Open || p.Status == PipStatus.InProgress), ct);
        if (active) return null;

        var subject = await _db.Users.FirstOrDefaultAsync(u => u.Id == subjectUserId, ct);
        if (subject is null) return null;

        var pip = new Pip
        {
            SubjectUserId = subjectUserId, IsAuto = true, TriggerScore = scorePercent, TriggerPeriod = period,
            Status = PipStatus.Open, Outcome = PipOutcome.Pending, StartDate = DateTime.UtcNow,
            Reason = $"Auto-initiated: {period} performance score {scorePercent:0.#}% is below the {threshold}% threshold.",
            ExpectedImprovement = "Reach and sustain performance at or above the threshold next cycle.",
            SupportProvided = "Manager check-ins and a documented improvement plan.",
            CreatedById = actorId
        };
        _db.Pips.Add(pip);
        await _db.SaveChangesAsync(ct);

        // Combined notification/email to HR + the employee (spec: send as soon as the flag is true).
        var hrIds = await _db.Users.Where(u => u.Role == PortalRole.HrManager || u.Role == PortalRole.HrDirector)
            .Select(u => u.Id).ToListAsync(ct);
        var recipients = hrIds.Append(subjectUserId).Distinct().ToList();

        var tokens = new Dictionary<string, string>
        {
            ["employee"] = subject.Name,
            ["score"] = scorePercent.ToString("0.#", CultureInfo.InvariantCulture),
            ["threshold"] = threshold.ToString(),
            ["period"] = period
        };
        var (subjectLine, body) = await _config.RenderAsync("pip.combined", tokens, ct);
        if (string.IsNullOrWhiteSpace(subjectLine)) subjectLine = $"Performance Improvement Plan — {subject.Name}";
        if (string.IsNullOrWhiteSpace(body)) body = pip.Reason;

        await _notify.NotifyManyAsync(recipients, NotificationType.General, subjectLine, body, "/pip", ct);

        if (_integration.OutlookNotificationsLive && _azure.IsConfigured)
        {
            var emails = await _db.Users.Where(u => recipients.Contains(u.Id)).Select(u => u.Email).ToListAsync(ct);
            var html = $"<p>{System.Net.WebUtility.HtmlEncode(body)}</p>";
            foreach (var email in emails)
                try { await _graph.SendMailAsync(email, subjectLine, html, ct); } catch { /* best-effort */ }
        }

        await _audit.WriteAsync(AuditAction.PipTriggered, "Pip", pip.Id,
            $"Auto-PIP for {subject.Name} ({scorePercent:0.#}% < {threshold}%).");
        return pip.Id;
    }
}
