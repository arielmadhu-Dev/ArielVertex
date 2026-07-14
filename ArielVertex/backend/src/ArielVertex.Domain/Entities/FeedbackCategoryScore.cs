using ArielVertex.Domain.Common;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Per-competency score inside a feedback record (spec section 7 categories:
/// Technical Competency, Task Ownership, Team Collaboration, etc.).
/// </summary>
public class FeedbackCategoryScore : BaseEntity
{
    public int FeedbackId { get; set; }
    public Feedback? Feedback { get; set; }

    public string Category { get; set; } = string.Empty;   // matches PerformanceCategory.Key
    public int Score { get; set; }                          // 0-100
    public bool NotApplicable { get; set; }
    public string Comment { get; set; } = string.Empty;
}
