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

/// <summary>Promotion &amp; increment workflow: Manager recommend → HR validate → Leadership approve → HR complete.</summary>
[Authorize]
[Route("api/v1/promotions")]
public class PromotionsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly INotificationService _notify;
    private readonly IAuditService _audit;
    public PromotionsController(AppDbContext db, ICurrentUser me, INotificationService notify, IAuditService audit)
    { _db = db; _me = me; _notify = notify; _audit = audit; }

    private bool SeesAll => _me.Has(Permissions.PromotionsManage) || _me.Has(Permissions.PromotionsApprove);

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var q = _db.Promotions.Include(p => p.Employee).Include(p => p.RecommendedBy).AsNoTracking().AsQueryable();
        if (!SeesAll)
        {
            if (_me.Has(Permissions.PromotionsRecommend))
                q = q.Where(p => p.RecommendedById == _me.Id || p.Employee!.ManagerId == _me.Id || p.EmployeeId == _me.Id);
            else
                q = q.Where(p => p.EmployeeId == _me.Id);
        }
        var list = await q.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return Ok(list.Select(p => p.ToDto()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePromotionRequest req)
    {
        if (!_me.Has(Permissions.PromotionsRecommend) && !_me.Has(Permissions.PromotionsManage))
            return Denied("You cannot recommend promotions.");
        var emp = await _db.Users.FindAsync(req.EmployeeId);
        if (emp is null) return BadInput("Unknown employee.");
        if (!_me.Has(Permissions.PromotionsManage) && emp.ManagerId != _me.Id)
            return Denied("You can only recommend promotions for your direct reports.");

        var p = new Promotion
        {
            EmployeeId = emp.Id, CurrentDesignation = emp.Designation,
            ProposedDesignation = req.ProposedDesignation.Trim(), ProposedSalary = req.ProposedSalary,
            Justification = req.Justification?.Trim() ?? "", Stage = PromotionStage.ManagerRecommended,
            RecommendationType = req.RecommendationType,
            RecommendedById = _me.Id
        };
        _db.Promotions.Add(p);
        await _db.SaveChangesAsync();
        try { await _notify.NotifyAsync(emp.Id, NotificationType.General, "Promotion recommended", $"You have been recommended for {p.ProposedDesignation}.", "/promotions"); } catch { }
        try { await _audit.WriteAsync(AuditAction.PromotionRecommended, "Promotion", p.Id, $"{emp.Name} → {p.ProposedDesignation}."); } catch { }
        return Ok((await Loaded(p.Id)).ToDto());
    }

    [HttpPost("{id:int}/validate")]
    [Capability(Permissions.PromotionsManage)]
    public Task<IActionResult> Validate(int id, [FromBody] PromotionDecisionRequest req)
        => Advance(id, PromotionStage.ManagerRecommended, PromotionStage.HrValidated, req.Note, p => p.ValidatedAt = DateTime.UtcNow);

    [HttpPost("{id:int}/approve")]
    [Capability(Permissions.PromotionsApprove)]
    public Task<IActionResult> Approve(int id, [FromBody] PromotionDecisionRequest req)
        => Advance(id, PromotionStage.HrValidated, PromotionStage.LeadershipApproved, req.Note, p => p.ApprovedAt = DateTime.UtcNow);

    /// <summary>Finalize: apply the designation revision to the employee.</summary>
    [HttpPost("{id:int}/complete")]
    [Capability(Permissions.PromotionsManage)]
    public async Task<IActionResult> Complete(int id, [FromBody] PromotionDecisionRequest req)
    {
        var p = await _db.Promotions.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return Missing("Promotion not found.");
        if (p.Stage != PromotionStage.LeadershipApproved) return Conflict409("Leadership approval is required before completion.");

        p.Stage = PromotionStage.Completed; p.CompletedAt = DateTime.UtcNow; p.DecisionNote = req.Note?.Trim();
        if (p.Employee is not null) p.Employee.Designation = p.ProposedDesignation;
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(p.EmployeeId, NotificationType.General, "Promotion finalized 🎉",
            $"Congratulations! You are now {p.ProposedDesignation}.", "/promotions");
        await _audit.WriteAsync(AuditAction.PromotionDecided, "Promotion", p.Id, $"Completed → {p.ProposedDesignation}.");
        return Ok((await Loaded(p.Id)).ToDto());
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] PromotionDecisionRequest req)
    {
        if (!_me.Has(Permissions.PromotionsManage) && !_me.Has(Permissions.PromotionsApprove))
            return Denied("You cannot decide promotions.");
        var p = await _db.Promotions.FindAsync(id);
        if (p is null) return Missing("Promotion not found.");
        if (p.Stage is PromotionStage.Completed or PromotionStage.Rejected) return Conflict409("This promotion is already closed.");

        p.Stage = PromotionStage.Rejected; p.DecisionNote = req.Note?.Trim();
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(p.EmployeeId, NotificationType.General, "Promotion not approved",
            req.Note?.Trim() ?? "The promotion request was not approved at this time.", "/promotions");
        await _audit.WriteAsync(AuditAction.PromotionDecided, "Promotion", p.Id, "Rejected.");
        return Ok((await Loaded(p.Id)).ToDto());
    }

    private Task<Promotion> Loaded(int id) => _db.Promotions.Include(p => p.Employee).Include(p => p.RecommendedBy)
        .AsNoTracking().FirstAsync(p => p.Id == id);

    private async Task<IActionResult> Advance(int id, PromotionStage from, PromotionStage to, string? note, Action<Promotion> stamp)
    {
        var p = await _db.Promotions.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return Missing("Promotion not found.");
        if (p.Stage != from) return Conflict409($"Expected stage {from}, but promotion is at {p.Stage}.");
        p.Stage = to; p.DecisionNote = note?.Trim(); stamp(p);
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(p.EmployeeId, NotificationType.General, "Promotion update",
            $"Your promotion to {p.ProposedDesignation} advanced to: {to}.", "/promotions");
        await _audit.WriteAsync(AuditAction.PromotionDecided, "Promotion", p.Id, $"{from} → {to}.");
        return Ok((await Loaded(p.Id)).ToDto());
    }
}
