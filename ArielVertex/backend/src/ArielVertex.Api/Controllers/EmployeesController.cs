using ArielVertex.Api.Common;
using ArielVertex.Api.Security;
using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Common;
using ArielVertex.Application.Contracts;
using ArielVertex.Application.Security;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Api.Controllers;

[Authorize]
[Route("api/v1/employees")]
public class EmployeesController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    public EmployeesController(AppDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    /// <summary>Directory list — used by member/subject pickers across the app.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PageQuery q)
    {
        var query = _db.Users.Include(u => u.Department).Include(u => u.Manager).AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(u => u.Name.Contains(q.Search) || u.Email.Contains(q.Search) || u.Designation.Contains(q.Search));
        var total = await query.CountAsync();
        var page = await query.OrderBy(u => u.Name).Skip(q.Skip).Take(q.SafeSize).ToListAsync();
        return Ok(new PagedResult<UserListItemDto>(page.Select(u => u.ToListItem()).ToList(), total, q.SafePage, q.SafeSize));
    }

    /// <summary>Master data for the HR employee editor.</summary>
    [HttpGet("edit-options")]
    [Capability(Permissions.EmployeesManage)]
    public async Task<IActionResult> EditOptions(CancellationToken ct)
    {
        var departments = await _db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);
        var managers = await _db.Users.AsNoTracking().OrderBy(u => u.Name).ToListAsync(ct);

        return Ok(new EmployeeEditOptionsDto(
            departments.Select(d => new EmployeeEditOptionDto(d.Id, d.Name, d.Code)).ToList(),
            managers.Select(u => new EmployeeEditOptionDto(
                u.Id, u.Name, $"{u.Designation} · {u.Status}")).ToList()));
    }

    /// <summary>HR-editable employee profile fields. Email and portal role have separate identity/RBAC controls.</summary>
    [HttpPut("{id:int}")]
    [Capability(Permissions.EmployeesManage)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEmployeeRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { id }, ct);
        if (user is null) return Missing();

        var employeeCode = req.EmployeeCode.Trim();
        if (string.IsNullOrWhiteSpace(employeeCode)) return BadInput("Employee code is required.");
        if (await _db.Users.AnyAsync(u => u.Id != id && u.EmployeeCode.ToLower() == employeeCode.ToLower(), ct))
            return Conflict409("Another employee already uses this employee code.");

        if (req.DepartmentId is int departmentId &&
            !await _db.Departments.AnyAsync(d => d.Id == departmentId, ct))
            return BadInput("The selected department does not exist.");

        if (req.ManagerId is int)
        {
            var managerLinks = await _db.Users.AsNoTracking()
                .Select(u => new { u.Id, u.ManagerId })
                .ToDictionaryAsync(u => u.Id, u => u.ManagerId, ct);
            if (!managerLinks.ContainsKey(req.ManagerId.Value))
                return BadInput("The selected manager does not exist.");

            var visited = new HashSet<int>();
            var current = req.ManagerId;
            while (current is int managerId)
            {
                if (managerId == id)
                    return BadInput("This manager assignment would create a reporting cycle.");
                if (!visited.Add(managerId)) break;
                current = managerLinks.TryGetValue(managerId, out var next) ? next : null;
            }
        }

        var oldManagerId = user.ManagerId;
        user.Name = req.Name.Trim();
        user.EmployeeCode = employeeCode;
        user.Designation = req.Designation.Trim();
        user.Skills = req.Skills?.Trim() ?? "";
        user.Status = req.Status;
        user.JoiningDate = req.JoiningDate.Kind switch
        {
            DateTimeKind.Local => req.JoiningDate.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(req.JoiningDate, DateTimeKind.Utc),
            _ => req.JoiningDate
        };
        user.DepartmentId = req.DepartmentId;
        user.ManagerId = req.ManagerId;
        user.ProfileManagedLocally = req.ProfileManagedLocally;
        user.ManagerManagedLocally = req.ManagerManagedLocally;

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(AuditAction.EmployeeUpdated, "User", user.Id,
            $"{user.Name}: employee profile updated; manager {oldManagerId?.ToString() ?? "none"} -> {user.ManagerId?.ToString() ?? "none"}.", ct);

        await _db.Entry(user).Reference(u => u.Department).LoadAsync(ct);
        await _db.Entry(user).Reference(u => u.Manager).LoadAsync(ct);
        return Ok(user.ToListItem());
    }

    [HttpPut("{id:int}/role")]
    [Capability(Permissions.RolesManage)]
    public async Task<IActionResult> ChangeRole(int id, [FromBody] UpdateUserRoleRequest req)
    {
        var u = await _db.Users.FindAsync(id);
        if (u is null) return Missing();
        // Guard against removing the last Super Admin (avoids locking the org out of user/role management).
        if (u.Role == PortalRole.SuperAdmin && req.Role != PortalRole.SuperAdmin)
        {
            var superAdmins = await _db.Users.CountAsync(x => x.Role == PortalRole.SuperAdmin);
            if (superAdmins <= 1) return Conflict409("Cannot change the role of the last Super Admin.");
        }
        var old = u.Role;
        u.Role = req.Role;
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.RoleChanged, "User", u.Id, $"{u.Name}: {old} -> {req.Role}.");
        return Ok(new { ok = true });
    }
}
