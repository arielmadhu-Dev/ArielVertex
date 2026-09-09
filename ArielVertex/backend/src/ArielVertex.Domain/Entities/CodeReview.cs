using ArielVertex.Domain.Common;

namespace ArielVertex.Domain.Entities;

/// <summary>Technical review outcome captured by a Team Lead (spec 6.7 / 5).</summary>
public class CodeReview : BaseEntity
{
    public int ReviewRequestId { get; set; }
    public ReviewRequest? ReviewRequest { get; set; }

    public int ReviewerId { get; set; }
    public User? Reviewer { get; set; }

    public int CodeQualityRating { get; set; }             // 1-5
    public int ArchitectureRating { get; set; }            // 1-5
    public int TestingRating { get; set; }                 // 1-5
    public string Strengths { get; set; } = string.Empty;
    public string Observations { get; set; } = string.Empty;
    public string Blockers { get; set; } = string.Empty;
    public string ActionItems { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}
