namespace ArielVertex.Domain.Enums;

/// <summary>
/// Broad portal roles (spec section 3). Project-level authority is layered on top
/// via <see cref="ProjectRole"/> membership so access is both role- and data-scoped.
/// </summary>
public enum PortalRole
{
    SuperAdmin = 0,
    CeoAdmin = 1,
    HrManager = 2,
    HrDirector = 3,
    ProjectManager = 4,
    ProjectCoordinator = 5,
    BusinessDirector = 6,
    BusinessManager = 7,
    BusinessPerson = 8,
    TechnicalLead = 9,
    SystemAdmin = 10,
    Frontdesk = 11,
    Accountant = 12,
    Employee = 13
}

/// <summary>Role a user holds *on a specific project* (spec section 5).</summary>
public enum ProjectRole
{
    ProjectManager = 0,
    ProjectCoordinator = 1,
    TechnicalLead = 2,
    BusinessPerson = 3,
    Developer = 4,
    QA = 5,
    Member = 6
}

public enum EmployeeStatus { Active = 0, Inactive = 1 }

public enum ProjectStatus { Planning = 0, Active = 1, OnHold = 2, Completed = 3, Cancelled = 4 }

public enum ProjectHealth { Green = 0, Amber = 1, Red = 2 }

public enum Priority { Low = 0, Medium = 1, High = 2, Critical = 3 }

public enum DocumentCategory
{
    Requirement = 0, Technical = 1, Demo = 2, CustomerCallRecording = 3,
    MeetingNotes = 4, Deployment = 5, Scope = 6, Other = 7
}

/// <summary>Who may see a document/note. Ascending = more open.</summary>
public enum Visibility { Internal = 0, BusinessVisible = 1, ClientShareable = 2 }

public enum CallType { Customer = 0, Internal = 1 }

/// <summary>Developer status update health for a given day (spec 6.6).</summary>
public enum UpdateStatus { OnTrack = 0, AtRisk = 1, Blocked = 2 }

public enum ReviewType { ProjectReview = 0, CodeReview = 1, GeneralFeedback = 2 }

public enum ReviewRequestStatus { Open = 0, Scheduled = 1, InProgress = 2, Completed = 3, Cancelled = 4 }

/// <summary>Structured-feedback lifecycle (spec 6.8) — nothing reaches an employee un-approved.</summary>
public enum FeedbackStatus { Draft = 0, Submitted = 1, RevisionRequested = 2, Approved = 3, Published = 4 }

/// <summary>Organizational rating language (spec section 7). Never expose raw scores as "final".</summary>
public enum PerformanceRating
{
    Outstanding = 0,            // 90-100
    ExceedsExpectations = 1,    // 80-89
    MeetsExpectations = 2,      // 70-79
    NeedsImprovement = 3,       // 60-69
    PerformanceAttentionRequired = 4 // < 60
}

public enum PerformancePeriodType { Monthly = 0, Quarterly = 1 }

/// <summary>Hiring/resource request status flow (spec 6.11).</summary>
public enum ResourceRequestStatus
{
    Open = 0, UnderReview = 1, NeedMoreInformation = 2, Approved = 3,
    InHiringProcess = 4, CandidateShared = 5, Fulfilled = 6, Rejected = 7, Closed = 8
}

public enum NotificationType
{
    ReviewRequested = 0, ReviewScheduled = 1, FeedbackDue = 2, FeedbackSubmitted = 3,
    FeedbackPublished = 4, DocumentUploaded = 5, StatusMissed = 6,
    ResourceRequestCreated = 7, ResourceRequestUpdated = 8, ReportGenerated = 9,
    CallScheduled = 10, General = 11
}

public enum NotificationChannel { Portal = 0, Teams = 1, Outlook = 2 }

public enum SyncStatus { Success = 0, Partial = 1, Failed = 2 }

public enum ExpenseCategory
{
    OfficeSupplies = 0, Utilities = 1, Travel = 2, Refreshments = 3,
    Maintenance = 4, Software = 5, Courier = 6, Other = 7
}

/// <summary>Front-desk expense / payment-request lifecycle.</summary>
public enum ExpenseStatus
{
    Draft = 0, PaymentRequested = 1, Approved = 2, Rejected = 3, Paid = 4
}

/// <summary>Performance Improvement Plan lifecycle.</summary>
public enum PipStatus { Open = 0, InProgress = 1, Completed = 2, Closed = 3 }

public enum PipOutcome { Pending = 0, Improved = 1, NotImproved = 2 }

/// <summary>
/// Meeting-minutes lifecycle: notes captured → minutes auto-generated & shown for preview →
/// coordinator/HR approves → sent to all attendees. Corrections loop back through Preview.
/// </summary>
public enum MeetingMinuteStatus { Draft = 0, Preview = 1, Approved = 2, Sent = 3 }

/// <summary>Audited sensitive actions (spec section 9 / 4).</summary>
public enum AuditAction
{
    Login = 0, LoginFailed = 1, RoleChanged = 2, ProjectAssignment = 3,
    DocumentUploaded = 4, DocumentDeleted = 5, ReviewRequested = 6, ReviewScheduled = 7,
    FeedbackSubmitted = 8, FeedbackApproved = 9, FeedbackPublished = 10,
    ReportPublished = 11, IntegrationSettingChanged = 12, ResourceRequestCreated = 13,
    EmployeeSyncRun = 14, PermissionChanged = 15,
    ExpenseCreated = 16, ExpensePaymentRequested = 17, ExpenseApproved = 18,
    ExpenseRejected = 19, ExpensePaid = 20, ExpenseSettingsChanged = 21,
    PipTriggered = 22, PipUpdated = 23, ConfigChanged = 24, TemplateChanged = 25,
    MinutesGenerated = 26, MinutesApproved = 27, MinutesSent = 28
}
