using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Contracts;
using ArielVertex.Application.Security;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Api.Controllers;

[Authorize]
[Route("api/v1/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IProjectAccessService _access;
    public DashboardController(AppDbContext db, ICurrentUser me, IProjectAccessService access)
    { _db = db; _me = me; _access = access; }

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get()
    {
        var audience = RoleGroups.DashboardFor(_me.Role);
        var visible = await _access.VisibleProjectIdsAsync();

        var projectsQ = _db.Projects.Include(p => p.Members).AsNoTracking().AsQueryable();
        if (visible is not null) projectsQ = projectsQ.Where(p => visible.Contains(p.Id));
        var projects = await projectsQ.ToListAsync();
        var projectIds = projects.Select(p => p.Id).ToList();

        var since = DateTime.UtcNow.Date.AddDays(-1);
        var missingByProject = new Dictionary<int, int>();
        foreach (var p in projects)
        {
            var memberIds = p.Members.Where(m => m.IsActive).Select(m => m.UserId).ToList();
            var expected = p.Members.Count(m => m.IsActive && (m.RoleOnProject == ProjectRole.Developer || m.RoleOnProject == ProjectRole.QA));
            var reported = await _db.StatusUpdates
                .Where(s => s.ProjectId == p.Id && s.UpdateDate >= since && memberIds.Contains(s.UserId))
                .Select(s => s.UserId).Distinct().CountAsync();
            missingByProject[p.Id] = Math.Max(0, expected - reported);
        }

        var health = projects.Select(p => new ProjectHealthDto(
            p.Id, p.Code, p.Name, p.Health.ToString(), p.Status.ToString(), missingByProject.GetValueOrDefault(p.Id))).ToList();

        // Upcoming: calls + review meetings within visible projects
        var upcomingCalls = await _db.ProjectCalls.Include(c => c.Project)
            .Where(c => projectIds.Contains(c.ProjectId) && c.ScheduledAt >= DateTime.UtcNow)
            .OrderBy(c => c.ScheduledAt).Take(5).ToListAsync();
        var upcomingReviews = await _db.ReviewMeetings.Include(m => m.ReviewRequest)
            .Where(m => m.ScheduledAt >= DateTime.UtcNow && projectIds.Contains(m.ReviewRequest!.ProjectId))
            .OrderBy(m => m.ScheduledAt).Take(5).ToListAsync();
        var upcoming = upcomingCalls
            .Select(c => new UpcomingDto("Call", c.Title, c.Project?.Name ?? "", c.ScheduledAt, $"/projects/{c.ProjectId}"))
            .Concat(upcomingReviews.Select(m => new UpcomingDto("Review", m.Title, "Review meeting", m.ScheduledAt, "/reviews")))
            .OrderBy(u => u.When).Take(6).ToList();

        var recent = await _db.AuditLogs.AsNoTracking().OrderByDescending(a => a.CreatedAt).Take(6).ToListAsync();
        var activity = recent.Select(a => new ActivityDto("activity", Common.Labels.Audit(a.Action), a.Summary, a.CreatedAt)).ToList();

        var stats = new List<StatDto>();
        var pending = new List<PendingDto>();
        var occupancy = new List<ResourceOccupancyDto>();

        int activeProjects = projects.Count(p => p.Status == ProjectStatus.Active);
        int totalMissing = missingByProject.Values.Sum();

        if (audience is "admin" or "hr" or "delivery")
        {
            var members = await _db.ProjectMembers.Include(m => m.User).Where(m => m.IsActive).ToListAsync();
            var byUser = members.GroupBy(m => m.UserId).Select(g => g.Sum(x => x.AllocationPct)).ToList();
            occupancy = new()
            {
                new("Free", byUser.Count(t => t == 0) + await _db.Users.CountAsync(u => u.Status == EmployeeStatus.Active && !members.Select(m => m.UserId).Contains(u.Id))),
                new("Partially Available", byUser.Count(t => t is > 0 and < 60)),
                new("Fully Allocated", byUser.Count(t => t is >= 60 and <= 100)),
                new("Overloaded", byUser.Count(t => t > 100)),
            };
        }

        switch (audience)
        {
            case "admin":
                var employeeCount = await _db.Users.CountAsync(u => u.Status == EmployeeStatus.Active);
                var pendingFeedback = await _db.Feedbacks.CountAsync(f => f.Status == FeedbackStatus.Submitted);
                var openReviews = await _db.ReviewRequests.CountAsync(r => r.Status == ReviewRequestStatus.Open);
                var openTickets = await _db.HelpdeskTickets.CountAsync(t => t.Status == HelpdeskTicketStatus.Open);
                stats.Add(new("projects", "Active Projects", activeProjects.ToString(), null, "info", "folder"));
                stats.Add(new("employees", "Employees", employeeCount.ToString(), null, "brand", "users"));
                // stats.Add(new("reviews", "Open Reviews", openReviews.ToString(), null, "warn", "clipboard"));
                stats.Add(new("tickets", "Open Tickets", openTickets.ToString(), null, openTickets > 0 ? "warn" : "good", "lifebuoy"));
                stats.Add(new("missing", "Missing Updates", totalMissing.ToString(), null, totalMissing > 0 ? "danger" : "good", "alert"));
                pending.Add(new("Feedback", "Feedback awaiting approval", "HR review required", pendingFeedback, "/feedback"));
                break;

            case "hr":
                var reqCount = await _db.ReviewRequests.CountAsync(r => r.Status != ReviewRequestStatus.Completed);
                var fbPending = await _db.Feedbacks.CountAsync(f => f.Status == FeedbackStatus.Submitted);
                var hiring = await _db.ResourceRequests.CountAsync(r => r.Status != ResourceRequestStatus.Closed && r.Status != ResourceRequestStatus.Fulfilled);
                stats.Add(new("reviewreq", "Open Review Requests", reqCount.ToString(), null, "info", "clipboard"));
                stats.Add(new("fbpending", "Feedback to Approve", fbPending.ToString(), null, fbPending > 0 ? "warn" : "good", "check"));
                stats.Add(new("hiring", "Active Hiring Requests", hiring.ToString(), null, "brand", "userplus"));
                stats.Add(new("projects", "Projects", projects.Count.ToString(), null, "info", "folder"));
                pending.Add(new("Feedback", "Feedback awaiting approval", "Approve & publish", fbPending, "/feedback"));
                pending.Add(new("Hiring", "Hiring requests", "Manage pipeline", hiring, "/hiring"));
                break;

            case "delivery":
                var myProjects = projects.Count;
                var myReviews = await _db.ReviewRequests.CountAsync(r => r.AssignedToId == _me.Id && r.Status != ReviewRequestStatus.Completed);
                var myResReq = await _db.ResourceRequests.CountAsync(r => r.RequestedById == _me.Id && r.Status != ResourceRequestStatus.Closed);
                stats.Add(new("myprojects", "My Projects", myProjects.ToString(), null, "brand", "folder"));
                stats.Add(new("missing", "Missing Updates", totalMissing.ToString(), null, totalMissing > 0 ? "danger" : "good", "alert"));
                stats.Add(new("myreviews", "Reviews to Run", myReviews.ToString(), null, "warn", "clipboard"));
                stats.Add(new("calls", "Upcoming Calls", upcomingCalls.Count.ToString(), null, "info", "calendar"));
                pending.Add(new("Reviews", "Reviews assigned to you", "Schedule / submit", myReviews, "/reviews"));
                pending.Add(new("Status", "Missing status updates", "Follow up with the team", totalMissing, "/status-updates"));
                break;

            case "business":
                stats.Add(new("projects", "My Projects", projects.Count.ToString(), null, "brand", "folder"));
                stats.Add(new("calls", "Upcoming Calls", upcomingCalls.Count.ToString(), null, "info", "calendar"));
                stats.Add(new("green", "Healthy Projects", projects.Count(p => p.Health == ProjectHealth.Green).ToString(), null, "good", "heart"));
                stats.Add(new("atrisk", "At-Risk Projects", projects.Count(p => p.Health != ProjectHealth.Green).ToString(), null, "warn", "alert"));
                break;

            default: // employee
                var myUpdates = await _db.StatusUpdates.CountAsync(s => s.UserId == _me.Id);
                var myPublished = await _db.PerformanceReports.Where(r => r.SubjectUserId == _me.Id && r.IsPublished).OrderBy(r => r.Period).ToListAsync();
                var latest = myPublished.LastOrDefault();
                stats.Add(new("myprojects", "My Projects", projects.Count.ToString(), null, "brand", "folder"));
                stats.Add(new("updates", "My Updates", myUpdates.ToString(), null, "info", "edit"));
                stats.Add(new("score", "Latest Score", latest is not null ? $"{latest.OverallScore:0.#}" : "—", latest is not null ? Performance.PerformanceModelLabel(latest.Rating) : null, "good", "trending"));
                stats.Add(new("calls", "Upcoming Calls", upcomingCalls.Count.ToString(), null, "info", "calendar"));
                pending.Add(new("Status", "Submit today's status", "Keep your projects updated", projects.Count, "/status-updates"));
                break;
        }

        // A PIP is always visible to its subject through /pip/mine, regardless of role.
        // Surface active plans on the dashboard so employees do not have to rely on a
        // notification link or an HR-only navigation permission to find them.
        var myActivePips = await _db.Pips.CountAsync(p => p.SubjectUserId == _me.Id &&
            (p.Status == PipStatus.Open || p.Status == PipStatus.InProgress));
        if (myActivePips > 0)
            pending.Insert(0, new PendingDto("PIP", "Your improvement plan",
                "Review your goals, support and progress", myActivePips, "/pip"));

        return Ok(new DashboardDto(audience, stats, health, occupancy, activity, upcoming, pending));
    }
}

/// <summary>Tiny local helper to label a rating without pulling the whole model into the controller.</summary>
file static class Performance
{
    public static string PerformanceModelLabel(PerformanceRating r) =>
        ArielVertex.Application.Performance.PerformanceModel.RatingLabel(r);
}
