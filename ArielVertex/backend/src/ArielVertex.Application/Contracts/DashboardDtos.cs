namespace ArielVertex.Application.Contracts;

/// <summary>Role-tailored dashboard payload (spec section 8). Fields are populated per audience.</summary>
public record DashboardDto(
    string Audience,
    IReadOnlyList<StatDto> Stats,
    IReadOnlyList<ProjectHealthDto> ProjectHealth,
    IReadOnlyList<ResourceOccupancyDto> ResourceOccupancy,
    IReadOnlyList<ActivityDto> RecentActivity,
    IReadOnlyList<UpcomingDto> Upcoming,
    IReadOnlyList<PendingDto> Pending);

public record StatDto(string Key, string Label, string Value, string? Delta, string Tone, string Icon);

public record ProjectHealthDto(int ProjectId, string Code, string Name, string Health, string Status, int MissingUpdates);

public record ResourceOccupancyDto(string Bucket, int Count);

public record ActivityDto(string Icon, string Title, string Detail, DateTime When);

public record UpcomingDto(string Kind, string Title, string Detail, DateTime When, string? Link);

public record PendingDto(string Kind, string Title, string Detail, int Count, string? Link);
