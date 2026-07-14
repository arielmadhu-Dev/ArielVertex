using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

// Performance-management entities ported additively from the PMS project:
// appraisal cycles, per-employee appraisals, goals/KRAs, promotions, training recommendations.
// These sit alongside (not replacing) the existing project/code review + PIP + feedback modules.

/// <summary>An appraisal cycle created by HR, e.g. "Q1 2026 Appraisal".</summary>
public class AppraisalCycle : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public CycleStatus Status { get; set; } = CycleStatus.Draft;

    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public ICollection<Appraisal> Appraisals { get; set; } = new List<Appraisal>();
    public ICollection<Goal> Goals { get; set; } = new List<Goal>();
}

/// <summary>One employee's appraisal within a cycle — Self → Manager → Release.</summary>
public class Appraisal : BaseEntity
{
    public AppraisalStage Stage { get; set; } = AppraisalStage.SelfPending;

    public decimal? SelfRating { get; set; }
    public string? SelfComments { get; set; }
    public DateTime? SelfSubmittedAt { get; set; }

    public decimal? ManagerRating { get; set; }
    public string? ManagerComments { get; set; }
    public DateTime? ManagerReviewedAt { get; set; }

    public decimal? FinalRating { get; set; }
    public DateTime? ReleasedAt { get; set; }

    public int CycleId { get; set; }
    public AppraisalCycle? Cycle { get; set; }

    public int EmployeeId { get; set; }
    public User? Employee { get; set; }

    public int? ManagerId { get; set; }
    public User? Manager { get; set; }
}

/// <summary>Goal / KRA owned by an employee, carrying a category, weightage and progress.</summary>
public class Goal : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;   // KRA category
    public int Weightage { get; set; }                     // 0-100
    public int Progress { get; set; }                      // 0-100
    public DateTime TargetDate { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.NotStarted;

    public int EmployeeId { get; set; }
    public User? Employee { get; set; }

    public int? AssignedById { get; set; }
    public User? AssignedBy { get; set; }

    public int? CycleId { get; set; }
    public AppraisalCycle? Cycle { get; set; }
}

/// <summary>Promotion / increment moving through Manager → HR → Leadership approval.</summary>
public class Promotion : BaseEntity
{
    public int EmployeeId { get; set; }
    public User? Employee { get; set; }

    public string CurrentDesignation { get; set; } = string.Empty;
    public string ProposedDesignation { get; set; } = string.Empty;
    public decimal? CurrentSalary { get; set; }
    public decimal? ProposedSalary { get; set; }
    public string Justification { get; set; } = string.Empty;

    public PromotionStage Stage { get; set; } = PromotionStage.ManagerRecommended;
    public string? DecisionNote { get; set; }

    public int? RecommendedById { get; set; }
    public User? RecommendedBy { get; set; }

    public DateTime? ValidatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>Training recommendation addressing a skill gap (manual or rule-based "AI").</summary>
public class TrainingRecommendation : BaseEntity
{
    public int EmployeeId { get; set; }
    public User? Employee { get; set; }

    public string SkillGap { get; set; } = string.Empty;
    public string RecommendedTraining { get; set; } = string.Empty;
    public int DurationMonths { get; set; } = 1;
    public TrainingStatus Status { get; set; } = TrainingStatus.Recommended;
    public string Source { get; set; } = "Manual";        // "Manual" | "AI"

    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }
}
