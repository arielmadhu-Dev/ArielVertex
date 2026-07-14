using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Security;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Infrastructure.Services;

/// <summary>
/// The core data-scoping primitive (spec section 4). Privileged/HR/management roles see all
/// projects; everyone else only projects they are an active member of. Changing an id in the
/// URL cannot grant access because every project sub-resource asks this service first.
/// </summary>
public class ProjectAccessService : IProjectAccessService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    public ProjectAccessService(AppDbContext db, ICurrentUser me) { _db = db; _me = me; }

    private bool SeesAllProjects =>
        _me.Has(Permissions.ProjectsViewAll) ||
        RoleGroups.IsPrivileged(_me.Role) || RoleGroups.IsManagement(_me.Role) || RoleGroups.IsHr(_me.Role);

    public async Task<bool> CanViewAsync(int projectId, CancellationToken ct = default)
    {
        if (SeesAllProjects) return true;
        return await _db.ProjectMembers
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == _me.Id && m.IsActive, ct);
    }

    public async Task<bool> CanManageAsync(int projectId, CancellationToken ct = default)
    {
        if (RoleGroups.IsPrivileged(_me.Role)) return true;
        if (!_me.Has(Permissions.ProjectsManage)) return false;
        // Must additionally be a PM/PC on this specific project.
        return await _db.ProjectMembers.AnyAsync(m =>
            m.ProjectId == projectId && m.UserId == _me.Id && m.IsActive &&
            (m.RoleOnProject == ProjectRole.ProjectManager || m.RoleOnProject == ProjectRole.ProjectCoordinator), ct);
    }

    public async Task<ProjectRole?> RoleOnProjectAsync(int projectId, CancellationToken ct = default)
    {
        var m = await _db.ProjectMembers
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == _me.Id && x.IsActive, ct);
        return m?.RoleOnProject;
    }

    public async Task<IReadOnlyCollection<int>?> VisibleProjectIdsAsync(CancellationToken ct = default)
    {
        if (SeesAllProjects) return null; // null = unrestricted
        return await _db.ProjectMembers
            .Where(m => m.UserId == _me.Id && m.IsActive)
            .Select(m => m.ProjectId).Distinct().ToListAsync(ct);
    }
}
