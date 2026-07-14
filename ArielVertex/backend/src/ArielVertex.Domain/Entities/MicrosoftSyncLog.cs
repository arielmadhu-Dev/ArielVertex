using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>Employee sync run history with success/failure counts (spec 6.2).</summary>
public class MicrosoftSyncLog : BaseEntity
{
    public DateTime RunAt { get; set; }
    public SyncStatus Status { get; set; } = SyncStatus.Success;
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Deactivated { get; set; }
    public int Failed { get; set; }
    public bool WasManual { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;
    public string? Message { get; set; }
}
