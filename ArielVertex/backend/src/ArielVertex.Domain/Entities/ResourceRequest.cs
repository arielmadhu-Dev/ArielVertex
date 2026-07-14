using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>Hiring/resource request raised by PM/PC from the project workspace (spec 6.11).</summary>
public class ResourceRequest : BaseEntity
{
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int RequestedById { get; set; }
    public User? RequestedBy { get; set; }

    public string RoleTitle { get; set; } = string.Empty;
    public string Skills { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public Priority Priority { get; set; } = Priority.Medium;
    public int Count { get; set; } = 1;
    public DateTime? ExpectedStartDate { get; set; }
    public ResourceRequestStatus Status { get; set; } = ResourceRequestStatus.Open;

    public ICollection<ResourceRequestComment> Comments { get; set; } = new List<ResourceRequestComment>();
}
