using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Customer/internal call scheduled from the project screen (spec 6.5). Outlook/Teams
/// identifiers are populated by the Graph boundary when integration is switched on.
/// </summary>
public class ProjectCall : BaseEntity
{
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Agenda { get; set; } = string.Empty;
    public CallType Type { get; set; } = CallType.Customer;
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public string Attendees { get; set; } = string.Empty;   // comma-separated emails

    public string? OutlookEventId { get; set; }
    public string? TeamsJoinUrl { get; set; }

    public string? MeetingNotes { get; set; }
    public string? FollowUpActionItems { get; set; }

    public int ScheduledById { get; set; }
    public User? ScheduledBy { get; set; }
}
