using ArielVertex.Api.Common;
using ArielVertex.Api.Security;
using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Contracts;
using ArielVertex.Application.Performance;
using ArielVertex.Application.Security;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Api.Controllers;

[Authorize]
[Route("api/v1/performance")]
public class PerformanceController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;
    private readonly IPerformanceReportService _generator;
    private readonly INotificationService _notify;
    private readonly IPipService _pip;
    public PerformanceController(AppDbContext db, ICurrentUser me, IAuditService audit,
        IPerformanceReportService generator, INotificationService notify, IPipService pip)
    { _db = db; _me = me; _audit = audit; _generator = generator; _notify = notify; _pip = pip; }

    /// <summary>The transparent weighted model (spec section 7) — powers the scoring UI.</summary>
    [HttpGet("categories")]
    public IActionResult Categories() => Ok(new
    {
        categories = PerformanceModel.Categories.Select(c =>
            new PerformanceCategoryDto(c.Key, c.Name, c.Factors, c.DefaultWeight, c.CanBeNa)),
        bands = new[]
        {
            new { min = 90, label = "Outstanding" }, new { min = 80, label = "Exceeds Expectations" },
            new { min = 70, label = "Meets Expectations" }, new { min = 60, label = "Needs Improvement" },
            new { min = 0, label = "Performance Attention Required" }
        }
    });

    /// <summary>Employee's own approved performance dashboard + monthly trend (spec 6.9).</summary>
    [HttpGet("my")]
    public async Task<IActionResult> My()
    {
        var reports = await _db.PerformanceReports.Include(r => r.CategoryScores).Include(r => r.SubjectUser)
            .Where(r => r.SubjectUserId == _me.Id && r.IsPublished)
            .AsNoTracking().OrderBy(r => r.Period).ToListAsync();

        var latest = reports.LastOrDefault();
        var trend = reports.Select(r => new TrendPointDto(r.Period, r.OverallScore, PerformanceModel.RatingLabel(r.Rating))).ToList();
        return Ok(new MyPerformanceDto(latest is not null, latest?.ToDto(), trend));
    }

    /// <summary>
    /// A single report for the printable/PDF view. The subject may fetch their own *published*
    /// report; managers with PerformanceViewAll may fetch any (incl. drafts).
    /// </summary>
    [HttpGet("report/{id:int}")]
    public async Task<IActionResult> Report(int id)
    {
        var r = await _db.PerformanceReports.Include(x => x.CategoryScores).Include(x => x.SubjectUser)
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return Missing();
        var mine = r.SubjectUserId == _me.Id && r.IsPublished;
        if (!mine && !_me.Has(Permissions.PerformanceViewAll)) return Denied();
        return Ok(r.ToDto());
    }

    [HttpGet("employee/{userId:int}")]
    [Capability(Permissions.PerformanceViewAll)]
    public async Task<IActionResult> Employee(int userId)
    {
        var reports = await _db.PerformanceReports.Include(r => r.CategoryScores).Include(r => r.SubjectUser)
            .Where(r => r.SubjectUserId == userId).AsNoTracking().OrderBy(r => r.Period).ToListAsync();
        return Ok(reports.Select(r => r.ToDto()));
    }

    /// <summary>All performance reports (management view) — drafts and published (spec 6.9).</summary>
    [HttpGet("reports")]
    [Capability(Permissions.PerformanceViewAll)]
    public async Task<IActionResult> Reports([FromQuery] bool? published)
    {
        var q = _db.PerformanceReports.Include(r => r.CategoryScores).Include(r => r.SubjectUser).AsNoTracking().AsQueryable();
        if (published is bool p) q = q.Where(r => r.IsPublished == p);
        var reports = await q.OrderByDescending(r => r.Period).ThenBy(r => r.SubjectUser!.Name).ToListAsync();
        return Ok(reports.Select(r => r.ToDto()));
    }

    /// <summary>Generate (or regenerate) a report from evidence. Created unpublished for HR review.</summary>
    [HttpPost("generate")]
    [Capability(Permissions.PerformanceViewAll)]
    public async Task<IActionResult> Generate([FromBody] GeneratePerformanceRequest req)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == req.SubjectUserId)) return BadInput("Unknown employee.");
        var id = await _generator.GenerateAsync(req.SubjectUserId, req.PeriodType, req.Period.Trim());
        await _audit.WriteAsync(Domain.Enums.AuditAction.ReportPublished, "PerformanceReport", id, $"Performance report {req.Period} generated (draft).");
        var dto = await _db.PerformanceReports.Include(r => r.CategoryScores).Include(r => r.SubjectUser)
            .AsNoTracking().FirstAsync(r => r.Id == id);
        return Ok(dto.ToDto());
    }

    [HttpPost("{id:int}/publish")]
    [Capability(Permissions.PerformancePublish)]
    public async Task<IActionResult> Publish(int id)
    {
        var r = await _db.PerformanceReports.FindAsync(id);
        if (r is null) return Missing();
        r.IsPublished = true; r.ApprovedById = _me.Id; r.PublishedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(r.SubjectUserId, Domain.Enums.NotificationType.ReportGenerated,
            "Your performance summary is available", $"Your approved performance report for {r.Period} has been published.", "/my-performance");
        await _audit.WriteAsync(Domain.Enums.AuditAction.ReportPublished, "PerformanceReport", r.Id, $"Performance report {r.Period} published.");

        // Auto-PIP: if the approved score is below the configured threshold, start a PIP + email HR & employee.
        var pipId = await _pip.EvaluateAndTriggerAsync(r.SubjectUserId, r.OverallScore, r.Period, _me.Id);
        return Ok(new { ok = true, pipTriggered = pipId != null });
    }
}
