using ArielVertex.Domain.Common;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Singleton configuration row (Id = 1) for the Bills module. Controls approval defaults,
/// approver roles, summary recipients, and due-date reminder settings.
/// </summary>
public class BillSetting : BaseEntity
{
    public bool ApprovalRequiredByDefault { get; set; } = true;
    public string ApproverRoles { get; set; } = "HrDirector,Accountant";
    public string DailySummaryRecipients { get; set; } = string.Empty;
    public string WeeklySummaryRecipients { get; set; } = string.Empty;
    /// <summary>Comma-separated number of days before due date to send reminders (e.g. "7,3,1").</summary>
    public string ReminderDaysBefore { get; set; } = "7,3,1";
    /// <summary>Comma-separated emails of users who receive due-date reminders.</summary>
    public string ReminderRecipients { get; set; } = string.Empty;
    public string? TeamsWebhookUrl { get; set; }
}
