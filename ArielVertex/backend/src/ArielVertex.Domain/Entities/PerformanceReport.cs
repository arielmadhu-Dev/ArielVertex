using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Monthly/quarterly calculated performance summary (spec 6.9 / 7). Scores are indicators;
/// the record stays unpublished until HR/PM/PC approval so employees only ever see
/// approved, constructive output.
/// </summary>
public class PerformanceReport : BaseEntity
{
    public int SubjectUserId { get; set; }
    public User? SubjectUser { get; set; }

    public PerformancePeriodType PeriodType { get; set; } = PerformancePeriodType.Monthly;
    public string Period { get; set; } = string.Empty;      // "2026-06" or "2026-Q2"

    public decimal OverallScore { get; set; }               // 0-100 weighted
    public PerformanceRating Rating { get; set; }

    public string Strengths { get; set; } = string.Empty;
    public string ImprovementAreas { get; set; } = string.Empty;
    public string RecommendedActions { get; set; } = string.Empty;
    public string DataSources { get; set; } = string.Empty; // transparency: what fed the score

    public bool IsPublished { get; set; }
    public int? ApprovedById { get; set; }
    public User? ApprovedBy { get; set; }
    public DateTime? PublishedAt { get; set; }

    public ICollection<PerformanceCategoryScore> CategoryScores { get; set; } = new List<PerformanceCategoryScore>();
}
