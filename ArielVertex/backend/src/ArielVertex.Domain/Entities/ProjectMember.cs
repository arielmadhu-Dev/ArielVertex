using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Join of a user to a project with their role and allocation (spec 6.10). This is the
/// authority the project-scoped authorization handler consults on every project request.
/// </summary>
public class ProjectMember : BaseEntity
{
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public ProjectRole RoleOnProject { get; set; } = ProjectRole.Developer;
    public int AllocationPct { get; set; } = 100;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}
