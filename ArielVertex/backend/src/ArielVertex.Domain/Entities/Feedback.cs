using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Structured quarterly feedback from RM/PM/PC/HR (spec 6.8). Carries a visibility
/// lifecycle: nothing is employee-visible until HR moves it to Published. Internal notes
/// are stored separately from the constructive summary the employee eventually sees.
/// </summary>
public class Feedback : BaseEntity
{
    public int SubjectUserId { get; set; }
    public User? SubjectUser { get; set; }

    public int AuthorId { get; set; }
    public User? Author { get; set; }

    public int? ProjectId { get; set; }
    public Project? Project { get; set; }

    public string Period { get; set; } = string.Empty;      // e.g. "2026-Q3"
    public FeedbackStatus Status { get; set; } = FeedbackStatus.Draft;

    /// <summary>Constructive, publish-safe summary shown to the employee once approved.</summary>
    public string ConstructiveSummary { get; set; } = string.Empty;
    public string Strengths { get; set; } = string.Empty;
    public string ImprovementAreas { get; set; } = string.Empty;
    public string ActionPlan { get; set; } = string.Empty;

    /// <summary>Confidential management notes — never exposed on the employee dashboard.</summary>
    public string InternalNotes { get; set; } = string.Empty;

    public int? ApprovedById { get; set; }
    public User? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? RevisionReason { get; set; }

    public ICollection<FeedbackCategoryScore> CategoryScores { get; set; } = new List<FeedbackCategoryScore>();
}
