using System.ComponentModel.DataAnnotations;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Contracts;

public record StatusUpdateDto(
    int Id, int ProjectId, string ProjectName, int UserId, string UserName, string AvatarColor,
    DateTime UpdateDate, string WorkCompleted, string NextPlannedWork, string Blockers,
    string Dependencies, decimal HoursSpent, UpdateStatus Status,
    string? InternalNote, string ClientShareableSummary, DateTime CreatedAt);

/// <summary>Business-safe projection — internal notes and blockers detail are stripped.</summary>
public record StatusUpdateBusinessDto(
    int Id, int ProjectId, string ProjectName, string UserName,
    DateTime UpdateDate, UpdateStatus Status, string ClientShareableSummary);

public record CreateStatusUpdateRequest(
    [Required] int ProjectId,
    DateTime UpdateDate,
    [Required, MaxLength(4000)] string WorkCompleted,
    string? NextPlannedWork, string? Blockers, string? Dependencies,
    [Range(0, 24)] decimal HoursSpent,
    UpdateStatus Status, string? InternalNote,
    [MaxLength(2000)] string? ClientShareableSummary);

public record ConsolidatedSummaryDto(
    int ProjectId, string ProjectName, DateTime PeriodStart, DateTime PeriodEnd,
    int UpdatesCount, int MembersReporting, int MembersExpected,
    string[] MissingUpdateFrom, IReadOnlyList<StatusUpdateBusinessDto> ClientReadyUpdates);
