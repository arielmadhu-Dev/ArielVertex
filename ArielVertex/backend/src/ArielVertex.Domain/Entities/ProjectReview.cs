using ArielVertex.Domain.Common;

namespace ArielVertex.Domain.Entities;

/// <summary>Project contribution review submitted by PM/PC (spec 6.7).</summary>
public class ProjectReview : BaseEntity
{
    public int ReviewRequestId { get; set; }
    public ReviewRequest? ReviewRequest { get; set; }

    public int ReviewerId { get; set; }
    public User? Reviewer { get; set; }

    public int DeliveryRating { get; set; }                // 1-5
    public int OwnershipRating { get; set; }               // 1-5
    public int CollaborationRating { get; set; }           // 1-5
    public string Strengths { get; set; } = string.Empty;
    public string Observations { get; set; } = string.Empty;
    public string ActionItems { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}
