using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>Petty cash ledger entry matching the standard cash-book sheet (SL No., Date, Particulars, Opening Balance, Credit, Debit, Balance).</summary>
public class PettyCashEntry : BaseEntity
{
    public DateTime Date { get; set; }
    public string Particulars { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public decimal Credit { get; set; }
    public decimal Debit { get; set; }
    public decimal Balance { get; set; }
    public string? Notes { get; set; }
    public int CreatedById { get; set; }
    public User? CreatedBy { get; set; }
    public PettyCashEntryStatus Status { get; set; } = PettyCashEntryStatus.Approved;
}
