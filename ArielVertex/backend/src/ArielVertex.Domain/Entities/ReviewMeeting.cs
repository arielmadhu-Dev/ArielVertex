using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>Scheduled review meeting with Outlook/Teams metadata (spec 6.7).</summary>
public class ReviewMeeting : BaseEntity
{
    public int ReviewRequestId { get; set; }
    public ReviewRequest? ReviewRequest { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public string Attendees { get; set; } = string.Empty;

    public string? OutlookEventId { get; set; }
    public string? TeamsJoinUrl { get; set; }

    /// <summary>Portal RSVP by the review subject — feeds HR's attendance tracking.</summary>
    public MeetingResponse ResponseStatus { get; set; } = MeetingResponse.NoResponse;
    public DateTime? RespondedAt { get; set; }

    public int ScheduledById { get; set; }
    public User? ScheduledBy { get; set; }
}
