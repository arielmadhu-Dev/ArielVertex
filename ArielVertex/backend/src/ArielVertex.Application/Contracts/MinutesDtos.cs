using System.ComponentModel.DataAnnotations;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Contracts;

// ---------- Meeting Minutes ----------
public record MeetingMinuteDto(
    int Id, string Title, DateTime MeetingDate, string? Location, IReadOnlyList<string> Attendees,
    string RawNotes, string MinutesText, MeetingMinuteStatus Status, string StatusLabel,
    bool GeneratedByAi, int CreatedById, string CreatedByName, string? ApprovedByName,
    DateTime? ApprovedAt, DateTime? SentAt, int SentCount, DateTime CreatedAt, bool CanManage);

public record CreateMinutesRequest(
    [Required] string Title, DateTime MeetingDate, string? Location, string? Attendees,
    [Required] string RawNotes);

/// <summary>Save corrections — to the notes, the generated minutes, or the meeting metadata.</summary>
public record UpdateMinutesRequest(
    [Required] string Title, DateTime MeetingDate, string? Location, string? Attendees,
    string? RawNotes, string? MinutesText);
