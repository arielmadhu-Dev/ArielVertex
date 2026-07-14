using ArielVertex.Api.Common;
using ArielVertex.Api.Security;
using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Contracts;
using ArielVertex.Application.Security;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Api.Controllers;

[Authorize]
[Route("api/v1/resource-requests")]
public class ResourceRequestsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly INotificationService _notify;
    private readonly IAuditService _audit;
    public ResourceRequestsController(AppDbContext db, ICurrentUser me, INotificationService notify, IAuditService audit)
    { _db = db; _me = me; _notify = notify; _audit = audit; }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var q = _db.ResourceRequests.Include(r => r.Project).Include(r => r.RequestedBy)
            .Include(r => r.Comments).ThenInclude(c => c.Author).AsNoTracking().AsQueryable();
        if (!(_me.Has(Permissions.ResourcesManage) || _me.Has(Permissions.ResourcesViewAll)))
            q = q.Where(r => r.RequestedById == _me.Id);
        var list = await q.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return Ok(list.Select(r => r.ToDto()));
    }

    [HttpPost]
    [Capability(Permissions.ResourcesRequest)]
    public async Task<IActionResult> Create([FromBody] CreateResourceRequestRequest req)
    {
        if (!await _db.Projects.AnyAsync(p => p.Id == req.ProjectId)) return BadInput("Unknown project.");
        var r = new ResourceRequest
        {
            ProjectId = req.ProjectId, RequestedById = _me.Id, RoleTitle = req.RoleTitle.Trim(),
            Skills = req.Skills?.Trim() ?? "", Reason = req.Reason?.Trim() ?? "", Priority = req.Priority,
            Count = req.Count, ExpectedStartDate = req.ExpectedStartDate?.ToUniversalTime(),
            Status = ResourceRequestStatus.Open
        };
        _db.ResourceRequests.Add(r);
        await _db.SaveChangesAsync();

        var hr = await _db.Users.Where(u => u.Role == PortalRole.HrManager || u.Role == PortalRole.HrDirector)
            .Select(u => u.Id).ToListAsync();
        await _notify.NotifyManyAsync(hr, NotificationType.ResourceRequestCreated, "New resource request",
            $"{req.RoleTitle} requested (x{req.Count}).", "/hiring");
        await _audit.WriteAsync(AuditAction.ResourceRequestCreated, "ResourceRequest", r.Id, $"Resource request '{req.RoleTitle}'.");
        return Ok(new { r.Id });
    }

    [HttpPost("{id:int}/status")]
    [Capability(Permissions.ResourcesManage)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateResourceStatusRequest req)
    {
        var r = await _db.ResourceRequests.FindAsync(id);
        if (r is null) return Missing();
        r.Status = req.Status;
        if (!string.IsNullOrWhiteSpace(req.Comment))
            _db.ResourceRequestComments.Add(new ResourceRequestComment { ResourceRequestId = id, AuthorId = _me.Id, Message = req.Comment.Trim() });
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(r.RequestedById, NotificationType.ResourceRequestUpdated,
            "Resource request updated", $"Status: {req.Status}.", "/hiring");
        return Ok(new { ok = true });
    }
}
