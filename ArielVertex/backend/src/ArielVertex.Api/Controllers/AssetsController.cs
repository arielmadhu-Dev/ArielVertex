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
[Route("api/v1/assets")]
public class AssetsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private bool CanManage => _me.Has(Permissions.AssetsManage);

    public AssetsController(AppDbContext db, ICurrentUser me)
    { _db = db; _me = me; }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var q = _db.Assets.Include(a => a.AssignedTo).AsNoTracking().AsQueryable();
        if (!CanManage)
        {
            var pendingForMe = await _db.AssetRequests
                .Where(r => r.TargetUserId == _me.Id && r.AssetId.HasValue &&
                            (r.Status == AssetRequestStatus.Pending || r.Status == AssetRequestStatus.PendingAcceptance))
                .Select(r => r.AssetId!.Value)
                .ToListAsync();
            q = q.Where(a => a.AssignedToId == _me.Id || pendingForMe.Contains(a.Id));
        }

        var list = await q.OrderByDescending(a => a.CreatedAt).ToListAsync();
        return Ok(list.Select(a => a.ToDto(CanManage)));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var a = await _db.Assets.Include(x => x.AssignedTo).FirstOrDefaultAsync(a => a.Id == id);
        if (a is null) return Missing();
        if (!CanManage && a.AssignedToId != _me.Id) return Denied();
        return Ok(a.ToDto(CanManage));
    }

    [HttpPost]
    [Capability(Permissions.AssetsManage)]
    public async Task<IActionResult> Create([FromBody] CreateAssetRequest req)
    {
        var asset = new Asset
        {
            Name = req.Name.Trim(),
            Description = req.Description?.Trim() ?? "",
            Category = req.Category,
            SerialNumber = req.SerialNumber?.Trim(),
            PurchaseDate = req.PurchaseDate?.ToUniversalTime(),
            PurchasePrice = req.PurchasePrice,
            Condition = req.Condition,
            Status = AssetStatus.Available,
            CreatedById = _me.Id
        };

        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        return Ok((await _db.Assets.FindAsync(asset.Id)).ToDto(CanManage));
    }

    [HttpPut("{id:int}")]
    [Capability(Permissions.AssetsManage)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAssetRequest req)
    {
        var a = await _db.Assets.FindAsync(id);
        if (a is null) return Missing();

        if (req.Name is not null) a.Name = req.Name.Trim();
        if (req.Description is not null) a.Description = req.Description.Trim();
        if (req.Category.HasValue) a.Category = req.Category.Value;
        if (req.SerialNumber is not null) a.SerialNumber = req.SerialNumber?.Trim();
        if (req.PurchaseDate.HasValue) a.PurchaseDate = req.PurchaseDate.Value.ToUniversalTime();
        if (req.PurchasePrice.HasValue) a.PurchasePrice = req.PurchasePrice;
        if (req.Condition.HasValue) a.Condition = req.Condition.Value;
        if (req.Status.HasValue)
        {
            a.Status = req.Status.Value;
            if (req.Status == AssetStatus.Assigned && req.AssignedToId.HasValue && a.AssignedToId != req.AssignedToId)
            {
                a.AssignedToId = req.AssignedToId.Value;
                a.AssignedAt = DateTime.UtcNow;
                a.ReturnedAt = null;
            }
            if (req.Status == AssetStatus.Available && a.AssignedToId.HasValue)
            {
                a.ReturnedAt = DateTime.UtcNow;
                a.AssignedToId = null;
                a.AssignedAt = null;
            }
        }
        if (req.AssignedToId.HasValue && !req.Status.HasValue)
        {
            a.AssignedToId = req.AssignedToId.Value;
            a.AssignedAt = DateTime.UtcNow;
            a.Status = AssetStatus.Assigned;
        }

        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok((await _db.Assets.FindAsync(id)).ToDto(CanManage));
    }

    [HttpDelete("{id:int}")]
    [Capability(Permissions.AssetsManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var a = await _db.Assets.FindAsync(id);
        if (a is null) return Missing();
        _db.Assets.Remove(a);
        await _db.SaveChangesAsync();
        return Ok();
    }
}
