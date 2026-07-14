using ArielVertex.Api.Common;
using ArielVertex.Api.Security;
using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Contracts;
using ArielVertex.Application.Security;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Api.Controllers;

[Authorize]
[Route("api/v1/feedback")]
public class FeedbackController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly INotificationService _notify;
    private readonly IAuditService _audit;
    private readonly IPipService _pip;

    public FeedbackController(AppDbContext db, ICurrentUser me, INotificationService notify, IAuditService audit, IPipService pip)
    { _db = db; _me = me; _notify = notify; _audit = audit; _pip = pip; }

    private bool CanApprove => _me.Has(Permissions.FeedbackApprove);

    /// <summary>
    /// Management/authoring view of feedback. HR/approvers see everything; authors see what they
    /// wrote. Subjects never reach their own un-published feedback here (spec 6.8 / non-goal §2).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var q = _db.Feedbacks.Include(f => f.SubjectUser).Include(f => f.Author).Include(f => f.Project)
            .Include(f => f.CategoryScores).AsNoTracking().AsQueryable();

        if (CanApprove || _me.Has(Permissions.FeedbackViewAll))
        { /* full visibility */ }
        else
            q = q.Where(f => f.AuthorId == _me.Id);  // authors only

        if (Enum.TryParse<FeedbackStatus>(status, true, out var st)) q = q.Where(f => f.Status == st);
        var list = await q.OrderByDescending(f => f.CreatedAt).ToListAsync();
        return Ok(list.Select(f => f.ToDto(CanApprove)));
    }

    [HttpPost]
    [Capability(Permissions.FeedbackSubmit)]
    public async Task<IActionResult> Create([FromBody] CreateFeedbackRequest req)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == req.SubjectUserId)) return BadInput("Unknown employee.");
        if (req.SubjectUserId == _me.Id) return BadInput("You cannot submit feedback about yourself.");

        var f = new Feedback
        {
            SubjectUserId = req.SubjectUserId, AuthorId = _me.Id, ProjectId = req.ProjectId,
            Period = req.Period.Trim(),
            Status = req.SubmitNow ? FeedbackStatus.Submitted : FeedbackStatus.Draft,
            ConstructiveSummary = req.ConstructiveSummary.Trim(), Strengths = req.Strengths?.Trim() ?? "",
            ImprovementAreas = req.ImprovementAreas?.Trim() ?? "", ActionPlan = req.ActionPlan?.Trim() ?? "",
            InternalNotes = req.InternalNotes?.Trim() ?? ""
        };
        if (req.CategoryScores is not null)
            foreach (var c in req.CategoryScores)
                f.CategoryScores.Add(new FeedbackCategoryScore
                { Category = c.Category, Score = c.Score, NotApplicable = c.NotApplicable, Comment = c.Comment?.Trim() ?? "" });

        _db.Feedbacks.Add(f);
        await _db.SaveChangesAsync();

        if (req.SubmitNow)
        {
            var approvers = await _db.Users.Where(u => u.Role == PortalRole.HrManager || u.Role == PortalRole.HrDirector)
                .Select(u => u.Id).ToListAsync();
            await _notify.NotifyManyAsync(approvers, NotificationType.FeedbackSubmitted, "Feedback awaiting approval",
                "New structured feedback needs HR review before it can be published.", "/feedback");
            await _audit.WriteAsync(AuditAction.FeedbackSubmitted, "Feedback", f.Id, $"Feedback submitted for review.");
        }
        return Ok(new { f.Id });
    }

    [HttpPost("{id:int}/request-revision")]
    [Capability(Permissions.FeedbackApprove)]
    public async Task<IActionResult> RequestRevision(int id, [FromBody] RevisionRequest req)
    {
        var f = await _db.Feedbacks.FindAsync(id);
        if (f is null) return Missing();
        f.Status = FeedbackStatus.RevisionRequested; f.RevisionReason = req.Reason.Trim();
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(f.AuthorId, NotificationType.General, "Feedback revision requested", req.Reason, "/feedback");
        return Ok(new { ok = true });
    }

    [HttpPost("{id:int}/approve")]
    [Capability(Permissions.FeedbackApprove)]
    public async Task<IActionResult> Approve(int id)
    {
        var f = await _db.Feedbacks.FindAsync(id);
        if (f is null) return Missing();
        if (f.Status is not (FeedbackStatus.Submitted or FeedbackStatus.RevisionRequested))
            return BadInput("Only submitted feedback can be approved.");
        f.Status = FeedbackStatus.Approved; f.ApprovedById = _me.Id; f.ApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.FeedbackApproved, "Feedback", f.Id, "Feedback approved by HR.");
        return Ok(new { ok = true });
    }

    /// <summary>Publish to the employee dashboard — the only path that makes feedback employee-visible.</summary>
    [HttpPost("{id:int}/publish")]
    [Capability(Permissions.FeedbackApprove)]
    public async Task<IActionResult> Publish(int id)
    {
        var f = await _db.Feedbacks.FindAsync(id);
        if (f is null) return Missing();
        if (f.Status != FeedbackStatus.Approved) return BadInput("Approve the feedback before publishing.");
        f.Status = FeedbackStatus.Published; f.PublishedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(f.SubjectUserId, NotificationType.FeedbackPublished, "Your feedback is available",
            $"Your approved feedback for {f.Period} has been published.", "/my-performance");
        await _audit.WriteAsync(AuditAction.FeedbackPublished, "Feedback", f.Id, "Feedback published to employee.");

        // Auto-PIP: if the average competency score of this feedback is below the threshold, start a PIP.
        var scores = await _db.FeedbackCategoryScores.Where(c => c.FeedbackId == f.Id && !c.NotApplicable).Select(c => (decimal)c.Score).ToListAsync();
        if (scores.Count > 0)
            await _pip.EvaluateAndTriggerAsync(f.SubjectUserId, scores.Average(), f.Period, _me.Id);
        return Ok(new { ok = true });
    }

    /// <summary>Employee-facing: only published feedback, never internal notes (spec 6.9 / §2).</summary>
    [HttpGet("my-published")]
    public async Task<IActionResult> MyPublished()
    {
        var list = await _db.Feedbacks.Include(f => f.Project).Include(f => f.CategoryScores)
            .Where(f => f.SubjectUserId == _me.Id && f.Status == FeedbackStatus.Published)
            .AsNoTracking().OrderByDescending(f => f.PublishedAt).ToListAsync();
        return Ok(list.Select(f => f.ToPublishedDto()));
    }
}
