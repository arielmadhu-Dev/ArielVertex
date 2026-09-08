using System.ComponentModel.DataAnnotations;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Contracts;

// ── Read DTOs ────────────────────────────────────────────────────────────────

public record BillDto(
    int Id, string Title, string Description, string Category,
    decimal Amount, string Currency, string Vendor,
    DateTime BillDate, DateTime? DueDate, string PaymentMethod,
    string? InvoiceNumber, bool HasInvoiceFile, string? InvoiceFileName,
    BillStatus Status, string StatusLabel, bool ApprovalRequired,
    int RaisedById, string RaisedByName,
    string? ApproverName, DateTime? DecidedAt, string? DecisionNote,
    DateTime? PaidAt, DateTime CreatedAt,
    bool CanApprove, bool CanManage, int DaysUntilDue);

public record BillSummaryDto(
    int Total, int PendingApproval, int Paid, int Overdue,
    decimal TotalAmount, decimal PaidAmount);

// ── Request DTOs ─────────────────────────────────────────────────────────────

public record CreateBillRequest(
    [Required, MaxLength(160)] string Title,
    string? Description,
    [Required] string Category,
    [Range(0.01, 100_000_000)] decimal Amount,
    string? Vendor,
    DateTime? BillDate,
    DateTime? DueDate,
    string? PaymentMethod,
    string? InvoiceNumber,
    bool ApprovalRequired,
    DateTime? PaidDate);

public record BillDecisionRequest(string? Note);

// ── Settings DTOs ────────────────────────────────────────────────────────────

public record BillSettingsDto(
    bool ApprovalRequiredByDefault,
    IReadOnlyList<string> ApproverRoles,
    string DailySummaryRecipients,
    string WeeklySummaryRecipients,
    string ReminderDaysBefore,
    string ReminderRecipients,
    string? TeamsWebhookUrl);

public record UpdateBillSettingsRequest(
    bool? ApprovalRequiredByDefault,
    IReadOnlyList<string>? ApproverRoles,
    string? DailySummaryRecipients,
    string? WeeklySummaryRecipients,
    string? ReminderDaysBefore,
    string? ReminderRecipients,
    string? TeamsWebhookUrl);

// ── AI Extraction DTO ────────────────────────────────────────────────────────

public record ExtractedBillData(
    string? Vendor,
    decimal? Amount,
    DateTime? DueDate,
    DateTime? BillDate,
    string? Category,
    string? InvoiceNumber);
