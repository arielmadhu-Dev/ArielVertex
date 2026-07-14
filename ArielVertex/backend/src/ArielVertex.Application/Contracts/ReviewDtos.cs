using System.ComponentModel.DataAnnotations;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Contracts;

public record ReviewRequestDto(
    int Id, int ProjectId, string ProjectName, int SubjectUserId, string SubjectName, string AvatarColor,
    int RequestedById, string RequestedByName, int? AssignedToId, string? AssignedToName,
    ReviewType ReviewType, ReviewRequestStatus Status, string Notes, DateTime? DueDate,
    DateTime? ClosedAt, DateTime CreatedAt, ReviewMeetingDto? Meeting, bool HasOutcome);

public record ReviewMeetingDto(
    int Id, string Title, string Description, DateTime ScheduledAt, int DurationMinutes,
    string Attendees, string? TeamsJoinUrl, string? OutlookEventId, MeetingResponse ResponseStatus);

/// <summary>Schedule one review event for one *or many* employees at once (spec 6.7 / #8, #9).</summary>
public record ScheduleGroupReviewRequest(
    [Required] int ProjectId,
    [Required, MinLength(1)] List<int> SubjectUserIds,
    int? AssignedToId,
    ReviewType ReviewType,
    [Required] string Title,
    string? Description,
    [Required] DateTime ScheduledAt,
    [Range(15, 240)] int DurationMinutes,
    [MaxLength(2000)] string? Notes);

/// <summary>A review subject accepting/declining their scheduled review from inside the portal.</summary>
public record RespondToReviewRequest([Required] MeetingResponse Response);

public record CreateReviewRequestRequest(
    [Required] int ProjectId,
    [Required] int SubjectUserId,
    int? AssignedToId, ReviewType ReviewType,
    [MaxLength(2000)] string? Notes, DateTime? DueDate);

public record ScheduleReviewRequest(
    [Required] string Title, string? Description,
    [Required] DateTime ScheduledAt,
    [Range(15, 240)] int DurationMinutes,
    string? Attendees, int? AssignedToId);

public record SubmitCodeReviewRequest(
    [Range(1, 5)] int CodeQualityRating,
    [Range(1, 5)] int ArchitectureRating,
    [Range(1, 5)] int TestingRating,
    string? Strengths, string? Observations, string? Blockers, string? ActionItems);

public record SubmitProjectReviewRequest(
    [Range(1, 5)] int DeliveryRating,
    [Range(1, 5)] int OwnershipRating,
    [Range(1, 5)] int CollaborationRating,
    string? Strengths, string? Observations, string? ActionItems);
