using System.ComponentModel.DataAnnotations;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Contracts;

// ---------- Users / directory ----------
public record UserListItemDto(
    int Id, string Name, string Email, string EmployeeCode, PortalRole Role, string RoleLabel,
    string Designation, string? Department, string AvatarColor, EmployeeStatus Status,
    string? ManagerName, DateTime JoiningDate);

public record UpdateUserRoleRequest([Required] PortalRole Role);

// ---------- Performance ----------
public record PerformanceCategoryDto(string Key, string Name, string Factors, decimal DefaultWeight, bool CanBeNa);

public record PerformanceReportDto(
    int Id, int SubjectUserId, string SubjectName, PerformancePeriodType PeriodType, string Period,
    decimal OverallScore, PerformanceRating Rating, string RatingLabel, string Strengths,
    string ImprovementAreas, string RecommendedActions, string DataSources, bool IsPublished,
    DateTime? PublishedAt, IReadOnlyList<CategoryBreakdownDto> Categories);

public record CategoryBreakdownDto(string Category, string CategoryName, int Score, decimal Weight, bool NotApplicable);

public record MyPerformanceDto(
    bool HasPublishedReport, PerformanceReportDto? Latest, IReadOnlyList<TrendPointDto> Trend);

public record GeneratePerformanceRequest(
    [Required] int SubjectUserId, PerformancePeriodType PeriodType, [Required] string Period);

public record TrendPointDto(string Period, decimal Score, string RatingLabel);

// ---------- Resource requests ----------
public record ResourceRequestDto(
    int Id, int ProjectId, string ProjectName, int RequestedById, string RequestedByName,
    string RoleTitle, string Skills, string Reason, Priority Priority, int Count,
    DateTime? ExpectedStartDate, ResourceRequestStatus Status, string StatusLabel,
    DateTime CreatedAt, IReadOnlyList<ResourceCommentDto> Comments);

public record ResourceCommentDto(int Id, string AuthorName, string Message, DateTime CreatedAt);

public record CreateResourceRequestRequest(
    [Required] int ProjectId,
    [Required, MaxLength(120)] string RoleTitle,
    string? Skills, string? Reason, Priority Priority,
    [Range(1, 50)] int Count, DateTime? ExpectedStartDate);

public record UpdateResourceStatusRequest(ResourceRequestStatus Status, string? Comment);

// ---------- Resource occupancy ----------
public record ResourceAllocationDto(
    int UserId, string Name, string Designation, string AvatarColor, int TotalAllocationPct,
    string Availability, IReadOnlyList<AllocationLineDto> Projects);

public record AllocationLineDto(int ProjectId, string ProjectName, ProjectRole RoleOnProject, int AllocationPct);

// ---------- Notifications ----------
public record NotificationDto(
    int Id, NotificationType Type, string Title, string Message, string? Link, bool IsRead, DateTime CreatedAt);

// ---------- Audit ----------
public record AuditLogDto(
    int Id, string ActorName, AuditAction Action, string ActionLabel, string EntityType,
    int? EntityId, string Summary, string? IpAddress, DateTime CreatedAt);

// ---------- Sync ----------
public record SyncLogDto(
    int Id, DateTime RunAt, SyncStatus Status, int Created, int Updated, int Deactivated,
    int Failed, bool WasManual, string TriggeredBy, string? Message);

public record IntegrationStatusDto(
    string AuthMode, bool MicrosoftLoginEnabled, bool GraphMeetingsLive, bool DirectorySyncLive,
    bool OutlookNotificationsLive, string AllowedDomain);
