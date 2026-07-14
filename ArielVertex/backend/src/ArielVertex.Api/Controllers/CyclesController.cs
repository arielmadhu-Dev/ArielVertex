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

/// <summary>Appraisal cycles (ported from PMS): HR creates, activates (fans out appraisals) and closes.</summary>
[Authorize]
[Route("api/v1/cycles")]
public class CyclesController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly INotificationService _notify;
    private readonly IAuditService _audit;
    public CyclesController(AppDbContext db, ICurrentUser me, INotificationService notify, IAuditService audit)
    { _db = db; _me = me; _notify = notify; _audit = audit; }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var list = await _db.AppraisalCycles.Include(c => c.Appraisals).AsNoTracking()
            .OrderByDescending(c => c.StartDate).ToListAsync();
        return Ok(list.Select(c => c.ToDto()));
    }

    [HttpPost]
    [Capability(Permissions.CyclesManage)]
    public async Task<IActionResult> Create([FromBody] CreateCycleRequest req)
    {
        if (req.EndDate < req.StartDate) return BadInput("End date must be after the start date.");
        var cycle = new AppraisalCycle
        {
            Name = req.Name.Trim(), StartDate = req.StartDate.ToUniversalTime(),
            EndDate = req.EndDate.ToUniversalTime(), Status = CycleStatus.Draft, CreatedById = _me.Id
        };
        _db.AppraisalCycles.Add(cycle);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.CycleCreated, "AppraisalCycle", cycle.Id, $"Cycle created: {cycle.Name}.");
        return Ok(cycle.ToDto());
    }

    /// <summary>Activate a cycle: generate a SelfPending appraisal for every active employee (idempotent).</summary>
    [HttpPost("{id:int}/activate")]
    [Capability(Permissions.CyclesManage)]
    public async Task<IActionResult> Activate(int id)
    {
        var cycle = await _db.AppraisalCycles.Include(c => c.Appraisals).FirstOrDefaultAsync(c => c.Id == id);
        if (cycle is null) return Missing("Cycle not found.");

        var existing = cycle.Appraisals.Select(a => a.EmployeeId).ToHashSet();
        var employees = await _db.Users
            .Where(u => u.Status == EmployeeStatus.Active && !existing.Contains(u.Id))
            .Select(u => new { u.Id, u.ManagerId })
            .ToListAsync();

        foreach (var e in employees)
        {
            _db.Appraisals.Add(new Appraisal
            {
                CycleId = cycle.Id, EmployeeId = e.Id, ManagerId = e.ManagerId, Stage = AppraisalStage.SelfPending
            });
        }
        cycle.Status = CycleStatus.Active;
        await _db.SaveChangesAsync();

        if (employees.Count > 0)
            await _notify.NotifyManyAsync(employees.Select(e => e.Id).ToArray(), NotificationType.General,
                "Self assessment pending", $"'{cycle.Name}' is open — please submit your self assessment.", "/appraisals");
        await _audit.WriteAsync(AuditAction.CycleActivated, "AppraisalCycle", cycle.Id,
            $"Cycle activated: {cycle.Name} (+{employees.Count} appraisals).");
        return Ok(cycle.ToDto());
    }

    [HttpPost("{id:int}/close")]
    [Capability(Permissions.CyclesManage)]
    public async Task<IActionResult> Close(int id)
    {
        var cycle = await _db.AppraisalCycles.FindAsync(id);
        if (cycle is null) return Missing("Cycle not found.");
        cycle.Status = CycleStatus.Closed;
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.CycleClosed, "AppraisalCycle", cycle.Id, $"Cycle closed: {cycle.Name}.");
        return Ok(cycle.ToDto());
    }
}
