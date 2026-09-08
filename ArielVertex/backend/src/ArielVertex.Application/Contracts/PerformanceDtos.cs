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
    int? ManagerId, string? ManagerName, AppraisalStage Stage,
    decimal? SelfRating, string? SelfComments, DateTime? SelfSubmittedAt,
    decimal? ManagerRating, string? ManagerComments, DateTime? ManagerReviewedAt,
    decimal? FinalRating, DateTime? ReleasedAt, DateTime CreatedAt);
public record SelfAppraisalRequest([Range(0, 5)] decimal SelfRating, [MaxLength(4000)] string? SelfComments);
public record ManagerAppraisalRequest([Range(0, 5)] decimal ManagerRating, [MaxLength(4000)] string? ManagerComments);
public record ReleaseAppraisalRequest([Range(0, 5)] decimal FinalRating);

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
    PromotionStage Stage, PromotionRecommendationType RecommendationType, string? DecisionNote, string? RecommendedByName,
    DateTime? ValidatedAt, DateTime? ApprovedAt, DateTime? CompletedAt, DateTime CreatedAt);
public record CreatePromotionRequest(
    [Required] int EmployeeId,
    [Required, MaxLength(120)] string ProposedDesignation,
    decimal? ProposedSalary,
    [MaxLength(2000)] string? Justification,
    PromotionRecommendationType RecommendationType = PromotionRecommendationType.Promotion);
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
