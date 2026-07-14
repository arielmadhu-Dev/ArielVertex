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
[RequireFeature("pip")]
[Route("api/v1/pip")]
public class PipController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;
    private readonly INotificationService _notify;
    public PipController(AppDbContext db, ICurrentUser me, IAuditService audit, INotificationService notify)
    { _db = db; _me = me; _audit = audit; _notify = notify; }

    private bool CanView => _me.Has(Permissions.PipView);
    private bool CanManage => _me.Has(Permissions.PipManage);

    /// <summary>HR/management see all PIPs; employees see only their own (via /pip/mine).</summary>
    [HttpGet]
    [Capability(Permissions.PipView)]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var q = _db.Pips.Include(p => p.SubjectUser).Include(p => p.CreatedBy).AsNoTracking().AsQueryable();
        if (Enum.TryParse<PipStatus>(status, true, out var st)) q = q.Where(p => p.Status == st);
        var list = await q.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return Ok(list.Select(p => p.ToDto()));
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var list = await _db.Pips.Include(p => p.SubjectUser).Include(p => p.CreatedBy)
            .Where(p => p.SubjectUserId == _me.Id).AsNoTracking().OrderByDescending(p => p.CreatedAt).ToListAsync();
        return Ok(list.Select(p => p.ToDto()));
    }

    [HttpPost]
    [Capability(Permissions.PipManage)]
    public async Task<IActionResult> Create([FromBody] CreatePipRequest req)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == req.SubjectUserId)) return BadInput("Unknown employee.");
        var p = new Pip
        {
            SubjectUserId = req.SubjectUserId, Reason = req.Reason.Trim(),
            ExpectedImprovement = req.ExpectedImprovement?.Trim() ?? "", SupportProvided = req.SupportProvided?.Trim() ?? "",
            IsAuto = false, CreatedById = _me.Id, Status = PipStatus.Open
        };
        _db.Pips.Add(p);
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(req.SubjectUserId, NotificationType.General, "A Performance Improvement Plan was created", p.Reason, "/pip");
        await _audit.WriteAsync(AuditAction.PipTriggered, "Pip", p.Id, "PIP created manually by HR.");
        return Ok(new { p.Id });
    }

    [HttpPut("{id:int}")]
    [Capability(Permissions.PipManage)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePipRequest req)
    {
        var p = await _db.Pips.FindAsync(id);
        if (p is null) return Missing();
        p.Status = req.Status; p.Outcome = req.Outcome; p.ReviewNotes = req.ReviewNotes?.Trim();
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.PipUpdated, "Pip", p.Id, $"PIP updated → {req.Status} / {req.Outcome}.");
        return Ok(new { ok = true });
    }
}
