using System.ComponentModel.DataAnnotations;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Contracts;

// ---------- PIP ----------
public record PipDto(
    int Id, int SubjectUserId, string SubjectName, string AvatarColor, string Reason,
    string ExpectedImprovement, string SupportProvided, PipStatus Status, string StatusLabel,
    PipOutcome Outcome, string OutcomeLabel, string? ReviewNotes, DateTime StartDate,
    bool IsAuto, decimal? TriggerScore, string? TriggerPeriod, string? CreatedByName, DateTime CreatedAt);

public record CreatePipRequest(
    [Required] int SubjectUserId, [Required] string Reason,
    string? ExpectedImprovement, string? SupportProvided);

public record UpdatePipRequest(PipStatus Status, PipOutcome Outcome, string? ReviewNotes);

// ---------- Admin configuration ----------
public record PlatformSettingDto(string Key, string Value, string Group, string Label, string Type, string Description, bool Editable);

public record UpdateSettingsRequest(IReadOnlyList<SettingValue> Settings);
public record SettingValue([Required] string Key, string Value);

public record TemplateDto(string Key, string Name, string Subject, string Body, string Placeholders);
public record UpdateTemplateRequest([Required] string Subject, [Required] string Body);
