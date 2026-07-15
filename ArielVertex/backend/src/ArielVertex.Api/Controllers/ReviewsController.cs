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
        var r = await _db.ReviewRequests
            .Include(x => x.Meeting).Include(x => x.SubjectUser).Include(x => x.AssignedTo)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return Missing("Review request not found.");
        if (!await _access.CanManageAsync(r.ProjectId)) return Denied("Only the project's PM/PC can schedule this review.");

        User? assignee = r.AssignedTo;
        if (req.AssignedToId is int assigneeId)
        {
            assignee = await _db.Users.FindAsync(assigneeId);
            if (assignee is null) return BadInput("Unknown reviewer.");
        }

        // Required calendar attendees are automatic: review subject, assigned reviewer and the
        // PM/PC scheduling it. The form only supplies optional extra email addresses.
        var attendees = Mappers.SplitAttendees(req.Attendees)
            .Concat(new[] { r.SubjectUser?.Email, assignee?.Email, _me.Email })
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email!.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        (string eventId, string joinUrl) calendar;
        try
        {
            calendar = await _graph.CreateMeetingAsync(
                req.Title, req.Description ?? "", req.ScheduledAt.ToUniversalTime(),
                req.DurationMinutes, attendees, ExistingGraphEventId(r.Meeting));
        }
        catch (MeetingIntegrationException ex)
        {
            return Fail(StatusCodes.Status503ServiceUnavailable, "calendar_integration_unavailable", ex.Message);
        }
        var (eventId, joinUrl) = calendar;

        if (r.Meeting is null)
            r.Meeting = new ReviewMeeting { ReviewRequestId = r.Id };
        r.Meeting.Title = req.Title.Trim(); r.Meeting.Description = req.Description?.Trim() ?? "";
        r.Meeting.ScheduledAt = req.ScheduledAt.ToUniversalTime(); r.Meeting.DurationMinutes = req.DurationMinutes;
        r.Meeting.Attendees = string.Join(", ", attendees);
        r.Meeting.OutlookEventId = string.IsNullOrWhiteSpace(eventId) ? null : eventId;
        r.Meeting.TeamsJoinUrl = string.IsNullOrWhiteSpace(joinUrl) ? null : joinUrl;
        r.Meeting.ScheduledById = _me.Id;
        if (req.AssignedToId is int a) r.AssignedToId = a;
        r.Status = ReviewRequestStatus.Scheduled;
        await _db.SaveChangesAsync();

        await _notify.NotifyManyAsync(new[] { r.SubjectUserId, r.AssignedToId ?? _me.Id },
            NotificationType.ReviewScheduled, "Review scheduled", $"'{req.Title}' is scheduled.", "/reviews");
        await _audit.WriteAsync(AuditAction.ReviewScheduled, "ReviewRequest", r.Id, $"Review scheduled: {req.Title}.");
        return Ok(new { meetingId = r.Meeting.Id, teamsJoinUrl = joinUrl, graphLive = _graph.IsLive });
    }

    /// <summary>
    /// Schedule one review event for one *or many* employees in a single action (#8, #9).
    /// Creates the shared Outlook/Teams event that invites all selected employees, and a tracked
    /// review record per employee so each appears in the portal and HR can follow up individually.
    /// </summary>
    [HttpPost("schedule")]
    [Capability(Permissions.ReviewsRequest)]
    public async Task<IActionResult> ScheduleGroup([FromBody] ScheduleGroupReviewRequest req)
    {
        if (!await _db.Projects.AnyAsync(p => p.Id == req.ProjectId)) return BadInput("Unknown project.");

        var subjectIds = req.SubjectUserIds.Distinct().ToList();
        var subjects = await _db.Users.Where(u => subjectIds.Contains(u.Id)).ToListAsync();
        if (subjects.Count == 0) return BadInput("Select at least one employee.");

        User? assignee = null;
        if (req.AssignedToId is int aid)
        {
            assignee = await _db.Users.FindAsync(aid);
            if (assignee is null) return BadInput("Unknown reviewer.");
        }

        var scheduledAt = req.ScheduledAt.ToUniversalTime();
        var attendeeEmails = subjects.Select(s => s.Email)
            .Concat(assignee is null ? Array.Empty<string>() : new[] { assignee.Email })
            .Append(_me.Email)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // One calendar event invites everyone (native Outlook/Teams accept/decline); each employee
        // gets an individual portal record for workflow tracking.
        (string eventId, string joinUrl) calendar;
        try
        {
            calendar = await _graph.CreateMeetingAsync(
                req.Title, req.Description ?? "", scheduledAt, req.DurationMinutes, attendeeEmails);
        }
        catch (MeetingIntegrationException ex)
        {
            return Fail(StatusCodes.Status503ServiceUnavailable, "calendar_integration_unavailable", ex.Message);
        }
        var (eventId, joinUrl) = calendar;
        var attendeesCsv = string.Join(", ", attendeeEmails);

        foreach (var s in subjects)
        {
            _db.ReviewRequests.Add(new ReviewRequest
            {
                ProjectId = req.ProjectId, SubjectUserId = s.Id, RequestedById = _me.Id,
                AssignedToId = req.AssignedToId, ReviewType = req.ReviewType,
                Status = ReviewRequestStatus.Scheduled, Notes = req.Notes?.Trim() ?? "",
                Meeting = new ReviewMeeting
                {
                    Title = req.Title.Trim(), Description = req.Description?.Trim() ?? "",
                    ScheduledAt = scheduledAt, DurationMinutes = req.DurationMinutes,
                    Attendees = attendeesCsv, OutlookEventId = eventId, TeamsJoinUrl = joinUrl,
                    ScheduledById = _me.Id
                }
            });
        }
        await _db.SaveChangesAsync();

        await _notify.NotifyManyAsync(subjects.Select(s => s.Id).ToArray(),
            NotificationType.ReviewScheduled, "Review scheduled",
            $"'{req.Title}' is scheduled. Please accept or decline in the portal.", "/reviews");
        if (assignee is not null)
            await _notify.NotifyAsync(assignee.Id, NotificationType.ReviewRequested, "Review to run",
                $"You've been assigned to run '{req.Title}' for {subjects.Count} employee(s).", "/reviews");
        await _audit.WriteAsync(AuditAction.ReviewScheduled, "ReviewRequest", 0,
            $"{req.ReviewType} scheduled for {subjects.Count} employee(s): {req.Title}.");

        return Ok(new { count = subjects.Count, teamsJoinUrl = joinUrl, graphLive = _graph.IsLive });
    }

    private static string? ExistingGraphEventId(ReviewMeeting? meeting)
    {
        if (string.IsNullOrWhiteSpace(meeting?.OutlookEventId)) return null;
        if (meeting.OutlookEventId.StartsWith("AV-EVT-", StringComparison.OrdinalIgnoreCase)) return null;
        if (meeting.TeamsJoinUrl?.Contains("av-placeholder", StringComparison.OrdinalIgnoreCase) == true) return null;
        return meeting.OutlookEventId;
    }

    /// <summary>A review subject accepts/declines their scheduled review from inside the portal (#8).</summary>
    [HttpPost("{id:int}/respond")]
    public async Task<IActionResult> Respond(int id, [FromBody] RespondToReviewRequest req)
    {
        var r = await _db.ReviewRequests.Include(x => x.Meeting).FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return Missing("Review request not found.");
        if (r.SubjectUserId != _me.Id) return Denied("Only the review subject can respond to this invitation.");
        if (r.Meeting is null) return BadInput("This review has not been scheduled yet.");

        r.Meeting.ResponseStatus = req.Response;
        r.Meeting.RespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var who = string.IsNullOrWhiteSpace(_me.Name) ? "An employee" : _me.Name;
        await _notify.NotifyAsync(r.RequestedById, NotificationType.ReviewScheduled,
            $"Review {req.Response}",
            $"{who} responded '{req.Response}' to the '{r.Meeting.Title}' review.", "/reviews");
        await _audit.WriteAsync(AuditAction.ReviewScheduled, "ReviewMeeting", r.Meeting.Id, $"Subject responded: {req.Response}.");
        return Ok(new { ok = true });
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
