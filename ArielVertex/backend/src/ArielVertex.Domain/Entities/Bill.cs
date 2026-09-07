using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Vendor / supplier bill (accounts payable) received by the organization. Optionally carries
/// an invoice file (private storage) and, when flagged, requires approval before payment.
/// </summary>
public class Bill : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Vendor { get; set; } = string.Empty;
    public DateTime BillDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;

    // Optional proof of invoice
    public string? InvoiceNumber { get; set; }
    public string? InvoiceFileName { get; set; }
    public string? InvoiceContentType { get; set; }
    public long InvoiceSizeBytes { get; set; }
    public string? InvoiceStoragePath { get; set; }        // never returned to clients directly

    public BillStatus Status { get; set; } = BillStatus.Submitted;
    public bool ApprovalRequired { get; set; }

    public int RaisedById { get; set; }
    public User? RaisedBy { get; set; }

    public int? ApproverId { get; set; }
    public User? Approver { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime? PaidAt { get; set; }

    /// <summary>Tracks when the last due-date reminder was sent to avoid duplicate sends.</summary>
    public DateTime? LastReminderAt { get; set; }
}
