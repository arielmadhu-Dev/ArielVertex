using ArielVertex.Domain.Common;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Single-row configuration for the expense module (Id = 1). Everything the Front-desk flow needs
/// is admin-configurable: whether approval is on, who approves, and who receives the summaries.
/// </summary>
public class ExpenseSetting : BaseEntity
{
    public bool ApprovalRequiredByDefault { get; set; } = true;

    /// <summary>Approver roles, comma-separated (e.g. "HrDirector,Accountant").</summary>
    public string ApproverRoles { get; set; } = "HrDirector,Accountant";

    /// <summary>Recipients of the every-evening summary — comma-separated emails.</summary>
    public string DailySummaryRecipients { get; set; } = string.Empty;

    /// <summary>Recipients of the weekly summary — comma-separated emails (HR Director by default).</summary>
    public string WeeklySummaryRecipients { get; set; } = string.Empty;

    /// <summary>Optional Microsoft Teams Incoming Webhook URL — summaries also post here when set.</summary>
    public string? TeamsWebhookUrl { get; set; }
}
