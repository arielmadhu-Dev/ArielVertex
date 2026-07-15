using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Internal user profile, mapped 1:1 to a Microsoft Entra identity when sync is on
/// (spec 6.1/6.2). <see cref="PasswordHash"/> is only populated for local/dev accounts;
/// Entra-provisioned users never carry a local password.
/// </summary>
public class User : BaseEntity
{
    public string? MicrosoftUserId { get; set; }         // Entra object id (oid)
    public string EmployeeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;    // unique login / sync key
    public string? PasswordHash { get; set; }            // null for Entra-only users

    public PortalRole Role { get; set; } = PortalRole.Employee;
    public string Designation { get; set; } = string.Empty;
    public string Skills { get; set; } = string.Empty;   // comma separated
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public DateTime JoiningDate { get; set; }

    /// <summary>Stable accent colour for avatars, derived once at seed/sync time.</summary>
    public string AvatarColor { get; set; } = "#1E7FD4";

    public bool IsProvisionedFromEntra { get; set; }
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>When true, HR-entered name/designation/department values are not overwritten by directory sync.</summary>
    public bool ProfileManagedLocally { get; set; }

    /// <summary>When true, HR's manager assignment is retained instead of using the Entra manager relationship.</summary>
    public bool ManagerManagedLocally { get; set; }

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public int? ManagerId { get; set; }
    public User? Manager { get; set; }
    public ICollection<User> DirectReports { get; set; } = new List<User>();

    public ICollection<ProjectMember> ProjectMemberships { get; set; } = new List<ProjectMember>();
}
