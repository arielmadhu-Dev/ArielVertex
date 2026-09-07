using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Abstractions;

/// <summary>The authenticated caller, projected from the validated JWT by the API layer.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int Id { get; }
    string Email { get; }
    string Name { get; }
    PortalRole Role { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool Has(string permission);
    string? IpAddress { get; }
    string? CorrelationId { get; }
}

public interface IJwtTokenService
{
    (string token, DateTime expiresAt) Create(User user);
}

/// <summary>
/// Authentication boundary. The local implementation validates seeded credentials; an Entra
/// implementation (added when the app registration is ready) validates an Entra id-token and
/// maps it to a <see cref="User"/> — controllers never change.
/// </summary>
public interface IIdentityProvider
{
    string Mode { get; }                       // "Local" | "Entra"
    Task<User?> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);
}

public interface IAuditService
{
    Task WriteAsync(AuditAction action, string entityType, int? entityId, string summary, CancellationToken ct = default);
}

/// <summary>
/// Inbound Microsoft login (spec 6.1). Validates an Entra-issued token, enforces the allowed
/// domain, and maps/provisions the caller to an internal <see cref="User"/> — which then receives
/// the app's own JWT, so all existing RBAC is unchanged. Returns null if validation fails.
/// </summary>
public interface IMicrosoftAuthService
{
    bool IsEnabled { get; }
    Task<User?> ValidateAndProvisionAsync(string token, CancellationToken ct = default);
}

public interface INotificationService
{
    Task NotifyAsync(int recipientId, NotificationType type, string title, string message, string? link = null, CancellationToken ct = default);
    Task NotifyManyAsync(IEnumerable<int> recipientIds, NotificationType type, string title, string message, string? link = null, CancellationToken ct = default);
}

/// <summary>
/// Project-scoped access primitive (spec section 4): "a user should never access another
/// project simply by changing an ID in the URL." Consulted by every project sub-resource.
/// </summary>
public interface IProjectAccessService
{
    Task<bool> CanViewAsync(int projectId, CancellationToken ct = default);
    Task<bool> CanManageAsync(int projectId, CancellationToken ct = default);
    Task<ProjectRole?> RoleOnProjectAsync(int projectId, CancellationToken ct = default);
    /// <summary>Ids of projects the current user may view (null = all, for privileged roles).</summary>
    Task<IReadOnlyCollection<int>?> VisibleProjectIdsAsync(CancellationToken ct = default);
}

/// <summary>
/// Microsoft Graph meeting boundary (spec 6.5/6.7). Live implementations create or update
/// Outlook calendar events and return the generated Teams join URL.
/// </summary>
public interface IGraphMeetingService
{
    bool IsLive { get; }
    Task<(string outlookEventId, string teamsJoinUrl)> CreateMeetingAsync(
        string title, string description, DateTime scheduledAt, int durationMinutes,
        IEnumerable<string> attendeeEmails, string? existingEventId = null, CancellationToken ct = default);
}

public sealed class MeetingIntegrationException : Exception
{
    public MeetingIntegrationException(string message, Exception? inner = null) : base(message, inner) { }
}

public record DirectorySyncResult(int Created, int Updated, int Deactivated, int Failed, SyncStatus Status, string Message);

/// <summary>Employee sync boundary (spec 6.2). Stub is a no-op returning zero counts.</summary>
public interface IDirectorySyncService
{
    bool IsLive { get; }
    Task<DirectorySyncResult> RunAsync(bool manual, string triggeredBy, CancellationToken ct = default);
}

/// <summary>
/// Generates a monthly/quarterly performance report by rolling the transparent evidence —
/// status-update consistency, code/project review outcomes and structured feedback — into the
/// weighted category model (spec section 7). Reports are created unpublished; HR publishes.
/// </summary>
public interface IPerformanceReportService
{
    Task<int> GenerateAsync(int subjectUserId, Domain.Enums.PerformancePeriodType periodType, string period, CancellationToken ct = default);
}

/// <summary>
/// Performance Improvement Plan automation. When an approved score falls below the admin-configured
/// threshold, auto-creates a PIP and sends a combined notification/email to HR + the employee.
/// </summary>
public interface IPipService
{
    /// <summary>Returns the new PIP id if one was auto-created, else null.</summary>
    Task<int?> EvaluateAndTriggerAsync(int subjectUserId, decimal scorePercent, string period, int? actorId, CancellationToken ct = default);
}

public record JobResult(string Job, int Affected, string Message);

/// <summary>
/// Scheduled operational automation (spec 6.6/6.8/6.9): missing-update reminders, quarter-end
/// feedback requests, and monthly report generation. Runs on a timer via the hosted service, and
/// each job can be triggered on demand from the Admin screen. All jobs are idempotent.
/// </summary>
public interface IJobService
{
    Task<JobResult> RunStatusRemindersAsync(CancellationToken ct = default);
    Task<JobResult> RunQuarterlyFeedbackRequestsAsync(CancellationToken ct = default);
    Task<JobResult> RunMonthlyReportGenerationAsync(CancellationToken ct = default);
    /// <summary>Every-evening expense summary to the configured daily recipients (spec: front desk).</summary>
    Task<JobResult> RunDailyExpenseSummaryAsync(CancellationToken ct = default);
    /// <summary>Weekly expense summary to the HR Director (+ any configured recipients).</summary>
    Task<JobResult> RunWeeklyExpenseSummaryAsync(CancellationToken ct = default);
    /// <summary>Due-date reminders for bills approaching their due date (7d, 3d, 1d).</summary>
    Task<JobResult> RunBillDueRemindersAsync(CancellationToken ct = default);
    /// <summary>Every-evening bill summary to the configured daily recipients.</summary>
    Task<JobResult> RunDailyBillSummaryAsync(CancellationToken ct = default);
    /// <summary>Weekly bill summary to the configured recipients.</summary>
    Task<JobResult> RunWeeklyBillSummaryAsync(CancellationToken ct = default);
}

/// <summary>Delivers a message to a Microsoft Teams channel via an Incoming Webhook (config-gated).</summary>
public interface ITeamsWebhookSender
{
    Task SendAsync(string webhookUrl, string title, string markdownBody, CancellationToken ct = default);
}

public record MinutesDraft(string MinutesText, bool UsedAi);

/// <summary>
/// Turns raw meeting notes into structured, grammar-corrected minutes of the meeting. The built-in
/// implementation structures + cleans the notes deterministically (works offline); when an AI service
/// is configured and enabled it produces polished, AI-grade minutes and grammar — a config-gated
/// drop-in, mirroring the Microsoft/Graph integration pattern. Never throws: falls back to the
/// built-in generator so a preview is always produced.
/// </summary>
public interface IMinutesGenerator
{
    Task<MinutesDraft> GenerateAsync(
        string title, DateTime meetingDate, string? location,
        IReadOnlyList<string> attendees, string rawNotes, CancellationToken ct = default);
}

/// <summary>
/// AI-powered bill/invoice data extraction. Sends the uploaded file (PDF or image) to an LLM
/// with vision capabilities and returns structured bill fields. Falls back gracefully on error.
/// </summary>
public interface IBillExtractor
{
    bool IsConfigured { get; }
    Task<Contracts.ExtractedBillData?> ExtractAsync(byte[] fileContent, string fileName, string contentType, CancellationToken ct = default);
}

/// <summary>
/// Runtime configuration read from the admin-editable settings store (feature flags, thresholds,
/// toggles) plus rendering of admin-editable notification templates. Changes take effect without a
/// redeploy — this is what makes emails, modules, and values configurable from the portal.
/// </summary>
public interface IPlatformConfig
{
    Task<bool> GetBoolAsync(string key, bool fallback = false, CancellationToken ct = default);
    Task<int> GetIntAsync(string key, int fallback = 0, CancellationToken ct = default);
    Task<string> GetStringAsync(string key, string fallback = "", CancellationToken ct = default);
    Task<bool> IsFeatureEnabledAsync(string feature, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, bool>> GetFeaturesAsync(CancellationToken ct = default);
    /// <summary>Render a template's subject + body, substituting {token} placeholders.</summary>
    Task<(string subject, string body)> RenderAsync(string templateKey, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default);
}
