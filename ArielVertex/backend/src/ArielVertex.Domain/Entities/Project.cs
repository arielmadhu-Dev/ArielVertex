using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>Project master record and root of the project workspace (spec 6.3).</summary>
public class Project : BaseEntity
{
    public string Code { get; set; } = string.Empty;        // e.g. MIB
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public ProjectHealth Health { get; set; } = ProjectHealth.Green;
    public Priority Priority { get; set; } = Priority.Medium;

    public DateTime StartDate { get; set; }
    public DateTime? ExpectedEndDate { get; set; }

    public string ClientName { get; set; } = string.Empty;
    public string BusinessOwner { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;        // comma separated
    public string Notes { get; set; } = string.Empty;

    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<ProjectDocument> Documents { get; set; } = new List<ProjectDocument>();
    public ICollection<ProjectCall> Calls { get; set; } = new List<ProjectCall>();
    public ICollection<StatusUpdate> StatusUpdates { get; set; } = new List<StatusUpdate>();
}
