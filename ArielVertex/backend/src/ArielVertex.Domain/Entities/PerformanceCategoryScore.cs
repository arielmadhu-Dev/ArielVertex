using ArielVertex.Domain.Common;

namespace ArielVertex.Domain.Entities;

/// <summary>Weighted category breakdown behind a performance report (spec section 7).</summary>
public class PerformanceCategoryScore : BaseEntity
{
    public int PerformanceReportId { get; set; }
    public PerformanceReport? PerformanceReport { get; set; }

    public string Category { get; set; } = string.Empty;
    public int Score { get; set; }                          // 0-100
    public decimal Weight { get; set; }                     // effective weight applied
    public bool NotApplicable { get; set; }
}
