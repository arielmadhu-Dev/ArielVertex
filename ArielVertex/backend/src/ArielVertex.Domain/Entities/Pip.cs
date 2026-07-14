using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Performance Improvement Plan. Can be created manually by HR, or auto-triggered when an
/// employee's approved performance score drops below the configured threshold.
/// </summary>
public class Pip : BaseEntity
{
    public int SubjectUserId { get; set; }
    public User? SubjectUser { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string ExpectedImprovement { get; set; } = string.Empty;
    public string SupportProvided { get; set; } = string.Empty;

    public PipStatus Status { get; set; } = PipStatus.Open;
    public PipOutcome Outcome { get; set; } = PipOutcome.Pending;
    public string? ReviewNotes { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    // Auto-trigger provenance
    public bool IsAuto { get; set; }
    public decimal? TriggerScore { get; set; }
    public string? TriggerPeriod { get; set; }

    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }
}
