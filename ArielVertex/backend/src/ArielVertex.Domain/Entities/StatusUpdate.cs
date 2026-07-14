using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Developer daily/weekly status update, scoped to an assigned project (spec 6.6).
/// The internal note stays internal; only <see cref="ClientShareableSummary"/> rolls up
/// into the business-ready consolidated summary.
/// </summary>
public class StatusUpdate : BaseEntity
{
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public DateTime UpdateDate { get; set; }
    public string WorkCompleted { get; set; } = string.Empty;
    public string NextPlannedWork { get; set; } = string.Empty;
    public string Blockers { get; set; } = string.Empty;
    public string Dependencies { get; set; } = string.Empty;
    public decimal HoursSpent { get; set; }
    public UpdateStatus Status { get; set; } = UpdateStatus.OnTrack;

    public string InternalNote { get; set; } = string.Empty;         // never client-visible
    public string ClientShareableSummary { get; set; } = string.Empty;
}
