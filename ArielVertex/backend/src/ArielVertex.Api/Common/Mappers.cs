using ArielVertex.Application.Contracts;
using ArielVertex.Application.Performance;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Api.Common;

public static class Mappers
{
    public static string[] SplitTags(string? csv) =>
        string.IsNullOrWhiteSpace(csv) ? Array.Empty<string>()
        : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string CatName(string key) =>
        PerformanceModel.Categories.FirstOrDefault(c => c.Key == key)?.Name ?? key;

    // ---- Users ----
    public static CurrentUserDto ToCurrentUserDto(this User u, IReadOnlyCollection<string> perms, string dashboard) =>
        new(u.Id, u.Name, u.Email, u.EmployeeCode, u.Role, Labels.Role(u.Role), u.Designation,
            u.Department?.Name, u.AvatarColor, dashboard, perms);

    public static UserListItemDto ToListItem(this User u) =>
        new(u.Id, u.Name, u.Email, u.EmployeeCode, u.Role, Labels.Role(u.Role), u.Designation,
            u.DepartmentId, u.Department?.Name, u.AvatarColor, u.Status, u.ManagerId, u.Manager?.Name,
            u.JoiningDate, u.Skills, u.IsProvisionedFromEntra, u.ProfileManagedLocally,
            u.ManagerManagedLocally, u.LastSyncedAt);

    // ---- Projects ----
    public static ProjectListItemDto ToListItem(this Project p, int memberCount, string? myRole) =>
        new(p.Id, p.Code, p.Name, p.Status, p.Health, p.Priority, p.ClientName, p.StartDate,
            p.ExpectedEndDate, memberCount, SplitTags(p.Tags), myRole);

    public static ProjectMemberDto ToDto(this ProjectMember m) =>
        new(m.Id, m.UserId, m.User?.Name ?? "", m.User?.Email ?? "", m.User?.Designation ?? "",
            m.User?.AvatarColor ?? "#1E7FD4", m.RoleOnProject, m.AllocationPct, m.IsActive, m.StartDate, m.EndDate);

    public static ProjectDetailDto ToDetail(this Project p, bool canManage, string? myRole) =>
        new(p.Id, p.Code, p.Name, p.Description, p.Status, p.Health, p.Priority, p.StartDate,
            p.ExpectedEndDate, p.ClientName, p.BusinessOwner, SplitTags(p.Tags), p.Notes,
            p.Members.Where(m => m.IsActive).Select(m => m.ToDto()).OrderBy(m => m.RoleOnProject).ToList(), canManage, myRole);

    // ---- Status updates ----
    public static StatusUpdateDto ToDto(this StatusUpdate s, bool includeInternal) =>
        new(s.Id, s.ProjectId, s.Project?.Name ?? "", s.UserId, s.User?.Name ?? "", s.User?.AvatarColor ?? "#1E7FD4",
            s.UpdateDate, s.WorkCompleted, s.NextPlannedWork, s.Blockers, s.Dependencies, s.HoursSpent, s.Status,
            includeInternal ? s.InternalNote : null, s.ClientShareableSummary, s.CreatedAt);

    public static StatusUpdateBusinessDto ToBusinessDto(this StatusUpdate s) =>
        new(s.Id, s.ProjectId, s.Project?.Name ?? "", s.User?.Name ?? "", s.UpdateDate, s.Status,
            string.IsNullOrWhiteSpace(s.ClientShareableSummary) ? "(No client summary provided.)" : s.ClientShareableSummary);

    // ---- Reviews ----
    public static ReviewMeetingDto ToDto(this ReviewMeeting m) =>
        new(m.Id, m.Title, m.Description, m.ScheduledAt, m.DurationMinutes, m.Attendees, m.TeamsJoinUrl, m.OutlookEventId, m.ResponseStatus);

    public static ReviewRequestDto ToDto(this ReviewRequest r) =>
        new(r.Id, r.ProjectId, r.Project?.Name ?? "", r.SubjectUserId, r.SubjectUser?.Name ?? "",
            r.SubjectUser?.AvatarColor ?? "#1E7FD4", r.RequestedById, r.RequestedBy?.Name ?? "",
            r.AssignedToId, r.AssignedTo?.Name, r.ReviewType, r.Status, r.Notes, r.DueDate, r.ClosedAt, r.CreatedAt,
            r.Meeting?.ToDto(), r.CodeReview != null || r.ProjectReview != null);

    // ---- Feedback ----
    public static FeedbackCategoryScoreDto ToDto(this FeedbackCategoryScore c) =>
        new(c.Category, CatName(c.Category), c.Score, c.NotApplicable, c.Comment);

    public static FeedbackDto ToDto(this Feedback f, bool canApprove) =>
        new(f.Id, f.SubjectUserId, f.SubjectUser?.Name ?? "", f.SubjectUser?.AvatarColor ?? "#1E7FD4",
            f.AuthorId, f.Author?.Name ?? "", f.ProjectId, f.Project?.Name, f.Period, f.Status,
            Labels.FeedbackStatus(f.Status), f.ConstructiveSummary, f.Strengths, f.ImprovementAreas, f.ActionPlan,
            f.InternalNotes, f.RevisionReason, f.CreatedAt, f.ApprovedAt, f.PublishedAt,
            f.CategoryScores.Select(c => c.ToDto()).ToList(), canApprove);

    public static FeedbackPublishedDto ToPublishedDto(this Feedback f) =>
        new(f.Id, f.Period, f.Project?.Name, f.ConstructiveSummary, f.Strengths, f.ImprovementAreas,
            f.ActionPlan, f.PublishedAt, f.CategoryScores.Select(c => c.ToDto()).ToList());

    // ---- Performance ----
    public static PerformanceReportDto ToDto(this PerformanceReport r) =>
        new(r.Id, r.SubjectUserId, r.SubjectUser?.Name ?? "", r.PeriodType, r.Period, r.OverallScore, r.Rating,
            PerformanceModel.RatingLabel(r.Rating), r.Strengths, r.ImprovementAreas, r.RecommendedActions,
            r.DataSources, r.IsPublished, r.PublishedAt,
            r.CategoryScores.Select(c => new CategoryBreakdownDto(c.Category, CatName(c.Category), c.Score, c.Weight, c.NotApplicable)).ToList());

    // ---- Resource requests ----
    public static ResourceRequestDto ToDto(this ResourceRequest r) =>
        new(r.Id, r.ProjectId, r.Project?.Name ?? "", r.RequestedById, r.RequestedBy?.Name ?? "", r.RoleTitle,
            r.Skills, r.Reason, r.Priority, r.Count, r.ExpectedStartDate, r.Status, Labels.ResourceStatus(r.Status),
            r.CreatedAt, r.Comments.OrderBy(c => c.CreatedAt)
                .Select(c => new ResourceCommentDto(c.Id, c.Author?.Name ?? "", c.Message, c.CreatedAt)).ToList());

    // ---- Notifications / audit / sync ----
    public static NotificationDto ToDto(this Notification n) =>
        new(n.Id, n.Type, n.Title, n.Message, n.Link, n.IsRead, n.CreatedAt);

    public static AuditLogDto ToDto(this AuditLog a) =>
        new(a.Id, a.ActorName, a.Action, Labels.Audit(a.Action), a.EntityType, a.EntityId, a.Summary, a.IpAddress, a.CreatedAt);

    public static SyncLogDto ToDto(this MicrosoftSyncLog s) =>
        new(s.Id, s.RunAt, s.Status, s.Created, s.Updated, s.Deactivated, s.Failed, s.WasManual, s.TriggeredBy, s.Message);

    // ---- PIP ----
    public static PipDto ToDto(this Pip p) =>
        new(p.Id, p.SubjectUserId, p.SubjectUser?.Name ?? "", p.SubjectUser?.AvatarColor ?? "#1E7FD4",
            p.Reason, p.ExpectedImprovement, p.SupportProvided, p.Status, Labels.PipStatus(p.Status),
            p.Outcome, Labels.PipOutcome(p.Outcome), p.ReviewNotes, p.StartDate, p.IsAuto, p.TriggerScore,
            p.TriggerPeriod, p.CreatedBy?.Name, p.CreatedAt);

    // ---- Meeting minutes ----
    public static string[] SplitAttendees(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? Array.Empty<string>()
        : raw.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
             .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static MeetingMinuteDto ToDto(this MeetingMinute m, bool canManage) =>
        new(m.Id, m.Title, m.MeetingDate, m.Location, SplitAttendees(m.Attendees), m.RawNotes, m.MinutesText,
            m.Status, Labels.MinutesStatus(m.Status), m.GeneratedByAi, m.CreatedById, m.CreatedBy?.Name ?? "",
            m.ApprovedBy?.Name, m.ApprovedAt, m.SentAt, m.SentCount, m.CreatedAt, canManage);

    // ---- Expenses ----
    public static ExpenseDto ToDto(this Expense e, bool canApprove, bool canManage) =>
        new(e.Id, e.Title, e.Description, e.Category, Labels.ExpenseCategory(e.Category), e.Amount, e.Currency,
            e.Vendor, e.ExpenseDate, e.PaymentMethod, e.InvoiceNumber, e.InvoiceStoragePath != null, e.InvoiceFileName,
            e.Status, Labels.ExpenseStatus(e.Status), e.ApprovalRequired, e.RaisedById, e.RaisedBy?.Name ?? "",
            e.Approver?.Name, e.DecidedAt, e.DecisionNote, e.PaidAt, e.CreatedAt, canApprove, canManage);

    // ---- Performance management (appraisal cycles, appraisals, goals, promotions, training) ----
    public static CycleDto ToDto(this AppraisalCycle c) =>
        new(c.Id, c.Name, c.StartDate, c.EndDate, c.Status, c.Appraisals?.Count ?? 0, c.CreatedAt);

    public static AppraisalDto ToDto(this Appraisal a) =>
        new(a.Id, a.CycleId, a.Cycle?.Name ?? "", a.EmployeeId, a.Employee?.Name ?? "", a.Employee?.AvatarColor ?? "#1E7FD4",
            a.ManagerId, a.Manager?.Name, a.Stage, a.SelfRating, a.SelfComments, a.SelfSubmittedAt,
            a.ManagerRating, a.ManagerComments, a.ManagerReviewedAt, a.FinalRating, a.ReleasedAt, a.CreatedAt);

    public static GoalDto ToDto(this Goal g) =>
        new(g.Id, g.EmployeeId, g.Employee?.Name ?? "", g.Employee?.AvatarColor ?? "#1E7FD4", g.Title, g.Description,
            g.Category, g.Weightage, g.Progress, g.TargetDate, g.Status, g.CycleId, g.AssignedBy?.Name, g.CreatedAt);

    public static PromotionDto ToDto(this Promotion p) =>
        new(p.Id, p.EmployeeId, p.Employee?.Name ?? "", p.Employee?.AvatarColor ?? "#1E7FD4", p.CurrentDesignation,
            p.ProposedDesignation, p.CurrentSalary, p.ProposedSalary, p.Justification, p.Stage, p.DecisionNote,
            p.RecommendedBy?.Name, p.ValidatedAt, p.ApprovedAt, p.CompletedAt, p.CreatedAt);

    public static TrainingDto ToDto(this TrainingRecommendation t) =>
        new(t.Id, t.EmployeeId, t.Employee?.Name ?? "", t.Employee?.AvatarColor ?? "#1E7FD4", t.SkillGap,
            t.RecommendedTraining, t.DurationMonths, t.Status, t.Source, t.CreatedAt);
}
