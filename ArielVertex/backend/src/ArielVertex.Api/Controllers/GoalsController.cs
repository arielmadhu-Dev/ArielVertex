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

/// <summary>Goals / KRAs (ported from PMS): managers &amp; HR assign; owners track progress.</summary>
[Authorize]
[Route("api/v1/goals")]
public class GoalsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly INotificationService _notify;
    private readonly IAuditService _audit;
    public GoalsController(AppDbContext db, ICurrentUser me, INotificationService notify, IAuditService audit)
    { _db = db; _me = me; _notify = notify; _audit = audit; }

    private bool SeesAll => _me.Has(Permissions.GoalsViewAll);
    private IQueryable<Goal> Base() => _db.Goals.Include(g => g.Employee).Include(g => g.AssignedBy);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? employeeId)
    {
        var q = Base().AsNoTracking().AsQueryable();
        if (!SeesAll) q = q.Where(g => g.EmployeeId == _me.Id || g.Employee!.ManagerId == _me.Id);
        if (employeeId is int e) q = q.Where(g => g.EmployeeId == e);
        var list = await q.OrderByDescending(g => g.CreatedAt).ToListAsync();
        return Ok(list.Select(g => g.ToDto()));
    }

    [HttpPost]
    [Capability(Permissions.GoalsAssign)]
    public async Task<IActionResult> Create([FromBody] CreateGoalRequest req)
    {
        var emp = await _db.Users.FindAsync(req.EmployeeId);
        if (emp is null) return BadInput("Unknown employee.");
        // Managers may only assign goals to their own reports; HR/leadership (GoalsViewAll) to anyone.
        if (!SeesAll && emp.ManagerId != _me.Id) return Denied("You can only assign goals to your direct reports.");

        var g = new Goal
        {
            EmployeeId = req.EmployeeId, Title = req.Title.Trim(), Description = req.Description?.Trim() ?? "",
            Category = req.Category?.Trim() ?? "", Weightage = req.Weightage, Progress = 0,
            TargetDate = req.TargetDate.ToUniversalTime(), Status = GoalStatus.NotStarted,
            AssignedById = _me.Id, CycleId = req.CycleId
        };
        _db.Goals.Add(g);
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(emp.Id, NotificationType.General, "New goal assigned",
            $"A new goal was assigned to you: {g.Title}.", "/goals");
        await _audit.WriteAsync(AuditAction.GoalAssigned, "Goal", g.Id, $"Goal assigned to {emp.Name}: {g.Title}.");
        return Ok(g.ToDto());
    }

    /// <summary>Owner updates progress/status; managers &amp; HR may also update.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateGoalRequest req)
    {
        var g = await _db.Goals.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id);
        if (g is null) return Missing("Goal not found.");
        var isOwner = g.EmployeeId == _me.Id;
        var isManager = g.Employee?.ManagerId == _me.Id;
        if (!isOwner && !isManager && !SeesAll && !_me.Has(Permissions.GoalsAssign))
            return Denied("You cannot update this goal.");

        g.Progress = Math.Clamp(req.Progress, 0, 100);
        g.Status = req.Status;
        if (g.Progress >= 100 && g.Status != GoalStatus.Cancelled) g.Status = GoalStatus.Completed;
        await _db.SaveChangesAsync();
        return Ok(g.ToDto());
    }

    [HttpDelete("{id:int}")]
    [Capability(Permissions.GoalsAssign)]
    public async Task<IActionResult> Delete(int id)
    {
        var g = await _db.Goals.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id);
        if (g is null) return Missing("Goal not found.");
        if (!SeesAll && g.Employee?.ManagerId != _me.Id) return Denied("You can only remove goals for your reports.");
        _db.Goals.Remove(g);
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
