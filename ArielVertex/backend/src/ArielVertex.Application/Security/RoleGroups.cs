using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Security;

/// <summary>Convenience groupings used for coarse UI/navigation decisions and seeding.</summary>
public static class RoleGroups
{
    public static bool IsHr(PortalRole r) => r is PortalRole.HrManager or PortalRole.HrDirector;

    public static bool IsManagement(PortalRole r) =>
        r is PortalRole.SuperAdmin or PortalRole.CeoAdmin;

    public static bool IsDelivery(PortalRole r) =>
        r is PortalRole.ProjectManager or PortalRole.ProjectCoordinator;

    public static bool IsBusiness(PortalRole r) =>
        r is PortalRole.BusinessDirector or PortalRole.BusinessManager or PortalRole.BusinessPerson;

    public static bool IsPrivileged(PortalRole r) =>
        r is PortalRole.SuperAdmin or PortalRole.SystemAdmin;

    /// <summary>Which dashboard a role lands on after login.</summary>
    public static string DashboardFor(PortalRole r) => r switch
    {
        PortalRole.SuperAdmin or PortalRole.CeoAdmin or PortalRole.SystemAdmin => "admin",
        PortalRole.HrManager or PortalRole.HrDirector => "hr",
        PortalRole.ProjectManager or PortalRole.ProjectCoordinator => "delivery",
        PortalRole.BusinessDirector or PortalRole.BusinessManager or PortalRole.BusinessPerson => "business",
        _ => "employee"
    };
}
