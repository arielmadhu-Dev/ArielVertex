using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Internal company expense / payment request raised by the Front Desk. Optionally carries a
/// proof-of-invoice file (private storage) and, when flagged, requires approval before payment.
/// </summary>
public class Expense : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Vendor { get; set; } = string.Empty;
    public DateTime ExpenseDate { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;

    // Optional proof of invoice
    public string? InvoiceNumber { get; set; }
    public string? InvoiceFileName { get; set; }
    public string? InvoiceContentType { get; set; }
    public long InvoiceSizeBytes { get; set; }
    public string? InvoiceStoragePath { get; set; }        // never returned to clients directly

    public ExpenseStatus Status { get; set; } = ExpenseStatus.PaymentRequested;
    public bool ApprovalRequired { get; set; }

    public int RaisedById { get; set; }
    public User? RaisedBy { get; set; }

    public int? ApproverId { get; set; }
    public User? Approver { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime? PaidAt { get; set; }
}
