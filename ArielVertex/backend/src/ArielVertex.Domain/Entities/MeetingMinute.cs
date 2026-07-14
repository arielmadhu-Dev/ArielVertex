using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Minutes of a meeting. A Project Coordinator or HR captures raw notes; the system auto-generates
/// structured, grammar-corrected minutes from them and shows a preview. Once the preview is approved
/// it is sent to all attendees. Corrections re-run the preview and can be re-sent.
/// </summary>
public class MeetingMinute : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public DateTime MeetingDate { get; set; }
    public string? Location { get; set; }

    /// <summary>Attendee emails, comma/newline separated. Internal ones are matched to portal users.</summary>
    public string Attendees { get; set; } = string.Empty;

    /// <summary>The raw notes the organiser typed — the reference the minutes are generated from.</summary>
    public string RawNotes { get; set; } = string.Empty;

    /// <summary>The generated (and optionally edited) minutes shown for preview and sent to attendees.</summary>
    public string MinutesText { get; set; } = string.Empty;

    public MeetingMinuteStatus Status { get; set; } = MeetingMinuteStatus.Draft;

    /// <summary>True when an AI service polished the minutes; false when the built-in generator did.</summary>
    public bool GeneratedByAi { get; set; }

    public int CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public int? ApprovedById { get; set; }
    public User? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public DateTime? SentAt { get; set; }
    public int SentCount { get; set; }
}
