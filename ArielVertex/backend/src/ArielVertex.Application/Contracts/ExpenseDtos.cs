using System.ComponentModel.DataAnnotations;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Contracts;

public record ExpenseDto(
    int Id, string Title, string Description, ExpenseCategory Category, string CategoryLabel,
    decimal Amount, string Currency, string Vendor, DateTime ExpenseDate, string PaymentMethod,
    string? InvoiceNumber, bool HasInvoiceFile, string? InvoiceFileName,
    ExpenseStatus Status, string StatusLabel, bool ApprovalRequired,
    int RaisedById, string RaisedByName, string? ApproverName, DateTime? DecidedAt,
    string? DecisionNote, DateTime? PaidAt, DateTime CreatedAt,
    bool CanApprove, bool CanManage);

public record ExpenseSummaryDto(
    int Total, int PendingApproval, int Paid, decimal TotalAmount, decimal PaidAmount);

public record CreateExpenseRequest(
    [Required, MaxLength(160)] string Title, string? Description, ExpenseCategory Category,
    [Range(0.01, 100000000)] decimal Amount, string? Vendor,
    DateTime ExpenseDate, string? PaymentMethod, string? InvoiceNumber, bool ApprovalRequired);

public record ExpenseDecisionRequest(string? Note);

public record ExpenseSettingsDto(
    bool ApprovalRequiredByDefault, IReadOnlyList<string> ApproverRoles,
    string DailySummaryRecipients, string WeeklySummaryRecipients, string? TeamsWebhookUrl);

public record UpdateExpenseSettingsRequest(
    bool ApprovalRequiredByDefault, IReadOnlyList<string>? ApproverRoles,
    string? DailySummaryRecipients, string? WeeklySummaryRecipients, string? TeamsWebhookUrl);
