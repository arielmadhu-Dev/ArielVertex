using System.ComponentModel.DataAnnotations;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Contracts;

// DTOs for the ported performance-management modules (appraisal cycles, appraisals, goals,
// promotions, training). Enums serialize as strings (configured globally).

// ---- Appraisal cycles ----
public record CycleDto(int Id, string Name, DateTime StartDate, DateTime EndDate, CycleStatus Status, int AppraisalCount, DateTime CreatedAt);
public record CreateCycleRequest(
    [Required, MaxLength(120)] string Name,
    [Required] DateTime StartDate,
    [Required] DateTime EndDate);

// ---- Appraisals (self → manager → release) ----
public record AppraisalDto(
    int Id, int CycleId, string CycleName, int EmployeeId, string EmployeeName, string AvatarColor,
    /// <summary>The employee's designation — selects which role-based form this appraisal uses.</summary>
    string EmployeeRole,
    int? ManagerId, string? ManagerName, AppraisalStage Stage,
    decimal? SelfRating, string? SelfComments, DateTime? SelfSubmittedAt,
    decimal? ManagerRating, string? ManagerComments, DateTime? ManagerReviewedAt,
    decimal? FinalRating, DateTime? ReleasedAt, DateTime CreatedAt);
public record SelfAppraisalRequest([Range(0, 5)] decimal SelfRating, [MaxLength(4000)] string? SelfComments)
{
    /// <summary>Per-area answers from the role's Self form. When supplied the rating is computed server-side.</summary>
    public List<AreaScoreInput>? Areas { get; init; }
}
public record ManagerAppraisalRequest([Range(0, 5)] decimal ManagerRating, [MaxLength(4000)] string? ManagerComments)
{
    /// <summary>Per-area ratings from the role's Manager form. When supplied the rating is computed server-side.</summary>
    public List<AreaScoreInput>? Areas { get; init; }
}
public record ReleaseAppraisalRequest([Range(0, 5)] decimal FinalRating);

// ---- Role-based appraisal forms (HR configured) ----
public record AreaScoreInput(
    [Required, MaxLength(200)] string AreaName,
    [Range(1, 5)] int? Rating,
    [MaxLength(4000)] string? Comment,
    bool NotApplicable = false);

public record AppraisalAreaScoreDto(string AreaName, AppraisalFormVariant Stage, int? Rating, string? Comment, bool NotApplicable);

public record AppraisalFormAreaDto(
    [Required, MaxLength(200)] string Name,
    AppraisalAreaType Type,
    [Range(0, 100)] int Weight,
    bool AllowNa);

public record AppraisalFormDto(
    int Id, string Role, AppraisalFormVariant Variant, bool Weighted, List<AppraisalFormAreaDto> Areas);

public record SaveAppraisalFormRequest(
    [Required, MaxLength(120)] string Role,
    AppraisalFormVariant Variant,
    bool Weighted,
    [Required] List<AppraisalFormAreaDto> Areas);

/// <summary>Both stages' scores plus the blended result, for the HR release screen.</summary>
public record AppraisalScoreSummaryDto(
    int? SelfScore, int? ManagerScore, int? BlendedScore,
    int SelfWeightPct, int ManagerWeightPct,
    List<AppraisalAreaScoreDto> Areas);

// ---- Goals / KRA ----
public record GoalDto(
    int Id, int EmployeeId, string EmployeeName, string AvatarColor, string Title, string Description,
    string Category, int Weightage, int Progress, DateTime TargetDate, GoalStatus Status,
    int? CycleId, string? AssignedByName, DateTime CreatedAt);
public record CreateGoalRequest(
    [Required] int EmployeeId,
    [Required, MaxLength(160)] string Title,
    [MaxLength(2000)] string? Description,
    [MaxLength(80)] string? Category,
    [Range(0, 100)] int Weightage,
    [Required] DateTime TargetDate,
    int? CycleId);
public record UpdateGoalRequest([Range(0, 100)] int Progress, GoalStatus Status);

// ---- Promotions & increments ----
public record PromotionDto(
    int Id, int EmployeeId, string EmployeeName, string AvatarColor, string CurrentDesignation,
    string ProposedDesignation, decimal? CurrentSalary, decimal? ProposedSalary, string Justification,
    PromotionStage Stage, string? DecisionNote, string? RecommendedByName,
    DateTime? ValidatedAt, DateTime? ApprovedAt, DateTime? CompletedAt, DateTime CreatedAt);
public record CreatePromotionRequest(
    [Required] int EmployeeId,
    [Required, MaxLength(120)] string ProposedDesignation,
    decimal? ProposedSalary,
    [MaxLength(2000)] string? Justification);
public record PromotionDecisionRequest([MaxLength(2000)] string? Note);

// ---- Learning & development ----
public record TrainingDto(
    int Id, int EmployeeId, string EmployeeName, string AvatarColor, string SkillGap,
    string RecommendedTraining, int DurationMonths, TrainingStatus Status, string Source, DateTime CreatedAt);
public record CreateTrainingRequest(
    [Required] int EmployeeId,
    [Required, MaxLength(160)] string SkillGap,
    [Required, MaxLength(200)] string RecommendedTraining,
    [Range(1, 24)] int DurationMonths);
public record UpdateTrainingStatusRequest(TrainingStatus Status);
