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
