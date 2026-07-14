using System.ComponentModel.DataAnnotations;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Contracts;

public record ProjectListItemDto(
    int Id, string Code, string Name, ProjectStatus Status, ProjectHealth Health,
    Priority Priority, string ClientName, DateTime StartDate, DateTime? ExpectedEndDate,
    int MemberCount, string[] Tags, string? MyRoleOnProject);

public record ProjectDetailDto(
    int Id, string Code, string Name, string Description, ProjectStatus Status,
    ProjectHealth Health, Priority Priority, DateTime StartDate, DateTime? ExpectedEndDate,
    string ClientName, string BusinessOwner, string[] Tags, string Notes,
    IReadOnlyList<ProjectMemberDto> Members, bool CanManage, string? MyRoleOnProject);

public record ProjectMemberDto(
    int Id, int UserId, string Name, string Email, string Designation, string AvatarColor,
    ProjectRole RoleOnProject, int AllocationPct, bool IsActive, DateTime StartDate, DateTime? EndDate);

public record CreateProjectRequest(
    [Required, MaxLength(40)] string Code,
    [Required, MaxLength(160)] string Name,
    string? Description, ProjectStatus Status, Priority Priority,
    DateTime StartDate, DateTime? ExpectedEndDate,
    string? ClientName, string? BusinessOwner, string? Tags, string? Notes);

public record UpdateProjectRequest(
    [Required, MaxLength(160)] string Name, string? Description,
    ProjectStatus Status, ProjectHealth Health, Priority Priority,
    DateTime StartDate, DateTime? ExpectedEndDate,
    string? ClientName, string? BusinessOwner, string? Tags, string? Notes);

public record AddMemberRequest(
    [Required] int UserId, ProjectRole RoleOnProject,
    [Range(0, 100)] int AllocationPct, DateTime StartDate, DateTime? EndDate);

public record AddDocumentRequest(
    [Required, MaxLength(160)] string Title, string? Description,
    DocumentCategory Category, Visibility Visibility, bool IsVideoLink,
    [MaxLength(500)] string? VideoUrl, [MaxLength(260)] string? FileName);

public record ScheduleCallRequest(
    [Required, MaxLength(160)] string Title, string? Agenda, CallType Type,
    [Required] DateTime ScheduledAt, [Range(15, 240)] int DurationMinutes, string? Attendees);

public record AddCommentRequest([Required, MaxLength(2000)] string Message);
