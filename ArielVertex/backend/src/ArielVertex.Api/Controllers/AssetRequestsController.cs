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
[Route("api/v1/asset-requests")]
public class AssetRequestsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly INotificationService _notify;
    private readonly IAuditService _audit;
    private bool CanApprove => _me.Has(Permissions.AssetsApprove);
    private bool CanManage => _me.Has(Permissions.AssetsManage);

    public AssetRequestsController(AppDbContext db, ICurrentUser me, INotificationService notify, IAuditService audit)
    { _db = db; _me = me; _notify = notify; _audit = audit; }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var q = _db.AssetRequests.Include(r => r.RaisedBy).Include(r => r.TargetUser).Include(r => r.Approver).Include(r => r.Asset).AsNoTracking().AsQueryable();
        if (!CanManage && !CanApprove)
            q = q.Where(r => r.RaisedById == _me.Id || r.TargetUserId == _me.Id);

        var list = await q.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return Ok(list.Select(r => r.ToDto(CanApprove)));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var r = await _db.AssetRequests.Include(x => x.RaisedBy).Include(x => x.TargetUser).Include(x => x.Approver).Include(x => x.Asset).FirstOrDefaultAsync(r => r.Id == id);
        if (r is null) return Missing();
        if (!CanManage && !CanApprove && r.RaisedById != _me.Id && r.TargetUserId != _me.Id) return Denied();
        return Ok(r.ToDto(CanApprove));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAssetRequestRequest req)
    {
        if (req.RequestType == AssetRequestType.Assignment)
        {
            if (!CanManage) return Denied("Only asset managers can assign assets.");
            if (!req.AssetId.HasValue) return BadInput("An asset is required for an assignment request.");
            if (!req.TargetUserId.HasValue) return BadInput("A target employee is required for an assignment request.");
            var target = await _db.Users.FindAsync(req.TargetUserId.Value);
            if (target is null) return BadInput("Unknown target employee.");
        }

        var isAssignment = req.RequestType == AssetRequestType.Assignment;
        var request = new AssetRequest
        {
            Subject = req.Subject.Trim(),
            Description = req.Description.Trim(),
            RequestType = req.RequestType,
            AssetId = req.AssetId,
            TargetUserId = req.TargetUserId,
            Quantity = req.Quantity,
            EstimatedCost = req.EstimatedCost,
            Vendor = req.Vendor?.Trim(),
            Status = isAssignment ? AssetRequestStatus.PendingAcceptance : AssetRequestStatus.Pending,
            RaisedById = _me.Id
        };

        _db.AssetRequests.Add(request);
        if (isAssignment && req.AssetId is int assetId)
        {
            var asset = await _db.Assets.FindAsync(assetId);
            if (asset is { Status: AssetStatus.Available })
                asset.Status = AssetStatus.PendingAssignment;
        }
        await _db.SaveChangesAsync();

        if (isAssignment && req.TargetUserId is int targetId)
        {
            await _notify.NotifyAsync(targetId, NotificationType.General, "Asset assignment awaiting your acceptance",
                $"'{request.Subject}' has been assigned to you. Please accept or decline on the Assets page.", "/assets");
        }
        await _notify.NotifyAsync(_me.Id, NotificationType.General, "Asset request submitted",
            $"'{request.Subject}' is awaiting the target employee's acceptance.", "/assets");
        await _audit.WriteAsync(AuditAction.BillCreated, "AssetRequest", request.Id, $"Asset request '{request.Subject}' raised.");

        return Ok((await _db.AssetRequests.Include(x => x.Asset).Include(x => x.RaisedBy).Include(x => x.TargetUser).FirstAsync(x => x.Id == request.Id)).ToDto(CanApprove));
    }

    [HttpPost("{id:int}/approve")]
    [Capability(Permissions.AssetsApprove)]
    public async Task<IActionResult> Approve(int id, [FromBody] AssetRequestDecisionRequest req)
    {
        var r = await _db.AssetRequests.FindAsync(id);
        if (r is null) return Missing();
        if (r.Status != AssetRequestStatus.Pending) return BadInput("Only pending requests can be approved.");
        if (!CanApprove) return Denied();

        r.Status = AssetRequestStatus.Approved;
        r.ApproverId = _me.Id;
        r.DecidedAt = DateTime.UtcNow;
        r.DecisionNote = req.Note?.Trim();

        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(r.RaisedById, NotificationType.General, "Asset request approved",
            $"'{r.Subject}' was approved by HR Director. You can now complete it.", "/assets");
        await _audit.WriteAsync(AuditAction.BillApproved, "AssetRequest", r.Id, $"Asset request '{r.Subject}' approved.");

        return Ok(new { ok = true });
    }

    [HttpPost("{id:int}/reject")]
    [Capability(Permissions.AssetsApprove)]
    public async Task<IActionResult> Reject(int id, [FromBody] AssetRequestDecisionRequest req)
    {
        var r = await _db.AssetRequests.FindAsync(id);
        if (r is null) return Missing();
        if (r.Status != AssetRequestStatus.Pending) return BadInput("Only pending requests can be rejected.");
        if (!CanApprove) return Denied();

        r.Status = AssetRequestStatus.Rejected;
        r.ApproverId = _me.Id;
        r.DecidedAt = DateTime.UtcNow;
        r.DecisionNote = req.Note?.Trim();

        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(r.RaisedById, NotificationType.General, "Asset request rejected",
            $"'{r.Subject}' was rejected. {req.Note}", "/assets");
        await _audit.WriteAsync(AuditAction.BillRejected, "AssetRequest", r.Id, $"Asset request '{r.Subject}' rejected.");

        return Ok(new { ok = true });
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var r = await _db.AssetRequests.FindAsync(id);
        if (r is null) return Missing();
        if (r.Status != AssetRequestStatus.Pending) return BadInput("Only pending requests can be cancelled.");
        if (r.RaisedById != _me.Id && !CanManage) return Denied();

        r.Status = AssetRequestStatus.Cancelled;
        r.DecidedAt = DateTime.UtcNow;
        r.DecisionNote = "Cancelled by requester/admin";

        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.BillRejected, "AssetRequest", r.Id, $"Asset request '{r.Subject}' cancelled.");

        return Ok(new { ok = true });
    }

    [HttpPost("{id:int}/accept")]
    public async Task<IActionResult> Accept(int id)
    {
        var r = await _db.AssetRequests.Include(x => x.Asset).Include(x => x.TargetUser).FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return Missing();
        if (r.Status != AssetRequestStatus.PendingAcceptance && r.Status != AssetRequestStatus.Pending)
            return BadInput("Only pending assignments can be accepted.");
        if (r.TargetUserId != _me.Id && !CanManage) return Denied("Only the assigned employee can accept this asset.");

        if (r.AssetId is int assetId)
        {
            var a = await _db.Assets.FindAsync(assetId);
            if (a is not null)
            {
                a.AssignedToId = r.TargetUserId;
                a.AssignedAt = DateTime.UtcNow;
                a.ReturnedAt = null;
                a.Status = AssetStatus.Assigned;
            }
        }

        r.Status = AssetRequestStatus.Completed;
        r.ApproverId = _me.Id;
        r.DecidedAt = DateTime.UtcNow;
        r.DecisionNote = "Accepted by employee";

        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(r.RaisedById, NotificationType.General, "Asset assignment accepted",
            $"'{r.Subject}' was accepted by {r.TargetUser?.Name ?? "the employee"} — the asset is now assigned.", "/assets");
        await _audit.WriteAsync(AuditAction.BillApproved, "AssetRequest", r.Id, $"Asset request '{r.Subject}' accepted by target employee.");

        return Ok(new { ok = true });
    }

    [HttpPost("{id:int}/decline")]
    public async Task<IActionResult> Decline(int id)
    {
        var r = await _db.AssetRequests.Include(x => x.Asset).Include(x => x.TargetUser).FirstOrDefaultAsync(x => x.Id == id);
        if (r is null) return Missing();
        if (r.Status != AssetRequestStatus.PendingAcceptance && r.Status != AssetRequestStatus.Pending)
            return BadInput("Only pending assignments can be declined.");
        if (r.TargetUserId != _me.Id && !CanManage) return Denied("Only the assigned employee can decline this asset.");

        if (r.AssetId is int assetId)
        {
            var a = await _db.Assets.FindAsync(assetId);
            if (a is not null && a.Status == AssetStatus.PendingAssignment)
                a.Status = AssetStatus.Available;
        }

        r.Status = AssetRequestStatus.Rejected;
        r.ApproverId = _me.Id;
        r.DecidedAt = DateTime.UtcNow;
        r.DecisionNote = "Declined by employee";

        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(r.RaisedById, NotificationType.General, "Asset assignment declined",
            $"'{r.Subject}' was declined by {r.TargetUser?.Name ?? "the employee"} — the asset remains available.", "/assets");
        await _audit.WriteAsync(AuditAction.BillRejected, "AssetRequest", r.Id, $"Asset request '{r.Subject}' declined by target employee.");

        return Ok(new { ok = true });
    }
}
