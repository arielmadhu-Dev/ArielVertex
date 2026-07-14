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
[Route("api/v1/review-requests")]
public class ReviewsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IProjectAccessService _access;
    private readonly IGraphMeetingService _graph;
    private readonly INotificationService _notify;
    private readonly IAuditService _audit;

    public ReviewsController(AppDbContext db, ICurrentUser me, IProjectAccessService access,
        IGraphMeetingService graph, INotificationService notify, IAuditService audit)
    { _db = db; _me = me; _access = access; _graph = graph; _notify = notify; _audit = audit; }

    /// <summary>Review requests visible to the caller: HR/mgmt see all; PM/PC see assigned; subjects see their own.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var q = _db.ReviewRequests
            .Include(r => r.Project).Include(r => r.SubjectUser).Include(r => r.RequestedBy)
            .Include(r => r.AssignedTo).Include(r => r.Meeting).Include(r => r.CodeReview).Include(r => r.ProjectReview)
            .AsNoTracking().AsQueryable();

        if (!(_me.Has(Permissions.FeedbackViewAll) || _me.Has(Permissions.ReviewsRequest)))
            q = q.Where(r => r.AssignedToId == _me.Id || r.SubjectUserId == _me.Id || r.RequestedById == _me.Id);
        if (Enum.TryParse<ReviewRequestStatus>(status, true, out var st)) q = q.Where(r => r.Status == st);

        var list = await q.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return Ok(list.Select(r => r.ToDto()));
    }

    [HttpPost]
    [Capability(Permissions.ReviewsRequest)]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequestRequest req)
    {
        if (!await _db.Projects.AnyAsync(p => p.Id == req.ProjectId)) return BadInput("Unknown project.");
        if (!await _db.Users.AnyAsync(u => u.Id == req.SubjectUserId)) return BadInput("Unknown employee.");

        var r = new ReviewRequest
        {
            ProjectId = req.ProjectId, SubjectUserId = req.SubjectUserId, RequestedById = _me.Id,
            AssignedToId = req.AssignedToId, ReviewType = req.ReviewType, Status = ReviewRequestStatus.Open,
            Notes = req.Notes?.Trim() ?? "", DueDate = req.DueDate?.ToUniversalTime()
        };
        _db.ReviewRequests.Add(r);
        await _db.SaveChangesAsync();

        if (req.AssignedToId is int a)
            await _notify.NotifyAsync(a, NotificationType.ReviewRequested, "Review requested",
                $"You have been asked to run a {req.ReviewType} review.", "/reviews");
        await _audit.WriteAsync(AuditAction.ReviewRequested, "ReviewRequest", r.Id, $"{req.ReviewType} review requested.");
        return Ok(new { r.Id });
    }

    /// <summary>PM/PC schedules the review in one click — creates the Outlook/Teams meeting (spec 6.7).</summary>
    [HttpPost("{id:int}/schedule")]
    [Capability(Permissions.ReviewsSchedule)]
    public async Task<IActionResult> Schedule(int id, [FromBody] ScheduleReviewRequest req)
    {
        var r = await _db.ReviewRequests.Include(x => x.Meeting).FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return Missing("Review request not found.");
        if (!await _access.CanManageAsync(r.ProjectId)) return Denied("Only the project's PM/PC can schedule this review.");

        var attendees = (req.Attendees ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var (eventId, joinUrl) = await _graph.CreateMeetingAsync(req.Title, req.Description ?? "", req.ScheduledAt.ToUniversalTime(), req.DurationMinutes, attendees);

        if (r.Meeting is null)
            r.Meeting = new ReviewMeeting { ReviewRequestId = r.Id };
        r.Meeting.Title = req.Title.Trim(); r.Meeting.Description = req.Description?.Trim() ?? "";
        r.Meeting.ScheduledAt = req.ScheduledAt.ToUniversalTime(); r.Meeting.DurationMinutes = req.DurationMinutes;
        r.Meeting.Attendees = req.Attendees ?? ""; r.Meeting.OutlookEventId = eventId; r.Meeting.TeamsJoinUrl = joinUrl;
        r.Meeting.ScheduledById = _me.Id;
        if (req.AssignedToId is int a) r.AssignedToId = a;
        r.Status = ReviewRequestStatus.Scheduled;
        await _db.SaveChangesAsync();

        await _notify.NotifyManyAsync(new[] { r.SubjectUserId, r.AssignedToId ?? _me.Id },
            NotificationType.ReviewScheduled, "Review scheduled", $"'{req.Title}' is scheduled.", "/reviews");
        await _audit.WriteAsync(AuditAction.ReviewScheduled, "ReviewRequest", r.Id, $"Review scheduled: {req.Title}.");
        return Ok(new { meetingId = r.Meeting.Id, teamsJoinUrl = joinUrl, graphLive = _graph.IsLive });
    }

    [HttpPost("{id:int}/code-review")]
    [Capability(Permissions.ReviewsSubmit)]
    public async Task<IActionResult> SubmitCodeReview(int id, [FromBody] SubmitCodeReviewRequest req)
    {
        var r = await _db.ReviewRequests.Include(x => x.CodeReview).FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return Missing();
        if (r.AssignedToId != _me.Id && !_me.Has(Permissions.FeedbackViewAll)) return Denied("Only the assigned reviewer can submit.");

        r.CodeReview ??= new CodeReview { ReviewRequestId = r.Id, ReviewerId = _me.Id };
        r.CodeReview.CodeQualityRating = req.CodeQualityRating; r.CodeReview.ArchitectureRating = req.ArchitectureRating;
        r.CodeReview.TestingRating = req.TestingRating; r.CodeReview.Strengths = req.Strengths?.Trim() ?? "";
        r.CodeReview.Observations = req.Observations?.Trim() ?? ""; r.CodeReview.Blockers = req.Blockers?.Trim() ?? "";
        r.CodeReview.ActionItems = req.ActionItems?.Trim() ?? ""; r.CodeReview.SubmittedAt = DateTime.UtcNow;
        r.Status = ReviewRequestStatus.Completed; r.ClosedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(r.RequestedById, NotificationType.FeedbackSubmitted, "Code review submitted",
            "A requested code review outcome has been submitted.", "/reviews");
        return Ok(new { ok = true });
    }

    [HttpPost("{id:int}/project-review")]
    [Capability(Permissions.ReviewsSubmit)]
    public async Task<IActionResult> SubmitProjectReview(int id, [FromBody] SubmitProjectReviewRequest req)
    {
        var r = await _db.ReviewRequests.Include(x => x.ProjectReview).FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return Missing();
        if (r.AssignedToId != _me.Id && !_me.Has(Permissions.FeedbackViewAll)) return Denied("Only the assigned reviewer can submit.");

        r.ProjectReview ??= new ProjectReview { ReviewRequestId = r.Id, ReviewerId = _me.Id };
        r.ProjectReview.DeliveryRating = req.DeliveryRating; r.ProjectReview.OwnershipRating = req.OwnershipRating;
        r.ProjectReview.CollaborationRating = req.CollaborationRating; r.ProjectReview.Strengths = req.Strengths?.Trim() ?? "";
        r.ProjectReview.Observations = req.Observations?.Trim() ?? ""; r.ProjectReview.ActionItems = req.ActionItems?.Trim() ?? "";
        r.ProjectReview.SubmittedAt = DateTime.UtcNow;
        r.Status = ReviewRequestStatus.Completed; r.ClosedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(r.RequestedById, NotificationType.FeedbackSubmitted, "Project review submitted",
            "A requested project review outcome has been submitted.", "/reviews");
        return Ok(new { ok = true });
    }
}
