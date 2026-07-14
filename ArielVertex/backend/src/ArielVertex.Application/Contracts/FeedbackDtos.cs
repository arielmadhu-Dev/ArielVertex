using System.ComponentModel.DataAnnotations;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Contracts;

public record FeedbackCategoryScoreDto(string Category, string CategoryName, int Score, bool NotApplicable, string Comment);

public record FeedbackDto(
    int Id, int SubjectUserId, string SubjectName, string AvatarColor, int AuthorId, string AuthorName,
    int? ProjectId, string? ProjectName, string Period, FeedbackStatus Status, string StatusLabel,
    string ConstructiveSummary, string Strengths, string ImprovementAreas, string ActionPlan,
    string? InternalNotes, string? RevisionReason, DateTime CreatedAt, DateTime? ApprovedAt, DateTime? PublishedAt,
    IReadOnlyList<FeedbackCategoryScoreDto> CategoryScores, bool CanApprove);

/// <summary>Employee-facing view — internal notes never included.</summary>
public record FeedbackPublishedDto(
    int Id, string Period, string? ProjectName, string ConstructiveSummary, string Strengths,
    string ImprovementAreas, string ActionPlan, DateTime? PublishedAt,
    IReadOnlyList<FeedbackCategoryScoreDto> CategoryScores);

public record CategoryScoreInput(
    [Required] string Category, [Range(0, 100)] int Score, bool NotApplicable, string? Comment);

public record CreateFeedbackRequest(
    [Required] int SubjectUserId, int? ProjectId,
    [Required] string Period,
    [Required, MaxLength(4000)] string ConstructiveSummary,
    string? Strengths, string? ImprovementAreas, string? ActionPlan, string? InternalNotes,
    IReadOnlyList<CategoryScoreInput>? CategoryScores, bool SubmitNow);

public record RevisionRequest([Required, MaxLength(1000)] string Reason);
