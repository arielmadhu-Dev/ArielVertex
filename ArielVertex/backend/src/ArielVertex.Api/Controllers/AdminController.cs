using ArielVertex.Api.Common;
using ArielVertex.Api.Security;
using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Common;
using ArielVertex.Application.Contracts;
using ArielVertex.Application.Security;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Auth;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ArielVertex.Api.Controllers;

[Authorize]
[Route("api/v1/admin")]
public class AdminController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IIdentityProvider _identity;
    private readonly IGraphMeetingService _graph;
    private readonly IDirectorySyncService _sync;
    private readonly IAuditService _audit;
    private readonly AuthSettings _auth;
    private readonly IMicrosoftAuthService _msauth;
    private readonly IntegrationSettings _integration;

    public AdminController(AppDbContext db, IIdentityProvider identity, IGraphMeetingService graph,
        IDirectorySyncService sync, IAuditService audit, IOptions<AuthSettings> auth,
        IMicrosoftAuthService msauth, IOptions<IntegrationSettings> integration)
    { _db = db; _identity = identity; _graph = graph; _sync = sync; _audit = audit; _auth = auth.Value; _msauth = msauth; _integration = integration.Value; }

    [HttpGet("audit")]
    [Capability(Permissions.AuditView)]
    public async Task<IActionResult> Audit([FromQuery] PageQuery q)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(a => a.Summary.Contains(q.Search) || a.ActorName.Contains(q.Search));
        var total = await query.CountAsync();
        var page = await query.OrderByDescending(a => a.CreatedAt).Skip(q.Skip).Take(q.SafeSize).ToListAsync();
        return Ok(new PagedResult<AuditLogDto>(page.Select(a => a.ToDto()).ToList(), total, q.SafePage, q.SafeSize));
    }

    [HttpGet("sync-logs")]
    [Capability(Permissions.SyncRun)]
    public async Task<IActionResult> SyncLogs()
    {
        var logs = await _db.MicrosoftSyncLogs.AsNoTracking().OrderByDescending(s => s.RunAt).Take(30).ToListAsync();
        return Ok(logs.Select(s => s.ToDto()));
    }

    [HttpPost("sync/run")]
    [Capability(Permissions.SyncRun)]
    public async Task<IActionResult> RunSync()
    {
        var result = await _sync.RunAsync(manual: true, triggeredBy: "admin");
        var log = new MicrosoftSyncLog
        {
            RunAt = DateTime.UtcNow, Status = result.Status, Created = result.Created, Updated = result.Updated,
            Deactivated = result.Deactivated, Failed = result.Failed, WasManual = true, TriggeredBy = "admin", Message = result.Message
        };
        _db.MicrosoftSyncLogs.Add(log);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.EmployeeSyncRun, "Sync", log.Id, result.Message);
        return Ok(log.ToDto());
    }

    /// <summary>Reports what integrations are live so the Admin screen shows honest status.</summary>
    [HttpGet("integration-status")]
    public IActionResult Integration() =>
        Ok(new IntegrationStatusDto(_identity.Mode, _msauth.IsEnabled, _graph.IsLive, _sync.IsLive,
            _integration.OutlookNotificationsLive, _auth.AllowedDomain));

    /// <summary>Run an automation job on demand (spec 6.6/6.8/6.9). Same code the scheduler runs.</summary>
    [HttpPost("jobs/{job}/run")]
    [Capability(Permissions.AdminSettings)]
    public async Task<IActionResult> RunJob(string job, [FromServices] Application.Abstractions.IJobService jobs)
    {
        var result = job.ToLowerInvariant() switch
        {
            "status-reminders" => await jobs.RunStatusRemindersAsync(),
            "quarterly-feedback" => await jobs.RunQuarterlyFeedbackRequestsAsync(),
            "monthly-reports" => await jobs.RunMonthlyReportGenerationAsync(),
            "expense-daily" => await jobs.RunDailyExpenseSummaryAsync(),
            "expense-weekly" => await jobs.RunWeeklyExpenseSummaryAsync(),
            _ => null
        };
        if (result is null) return BadInput("Unknown job. Use status-reminders, quarterly-feedback, monthly-reports, expense-daily, or expense-weekly.");
        await _audit.WriteAsync(AuditAction.EmployeeSyncRun, "Job", null, $"Job '{result.Job}' run manually: {result.Message}");
        return Ok(result);
    }
}
