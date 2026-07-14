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

/// <summary>Per-employee appraisals: Self assessment → Manager evaluation → Final rating release.</summary>
[Authorize]
[Route("api/v1/appraisals")]
public class AppraisalsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly INotificationService _notify;
    private readonly IAuditService _audit;
    public AppraisalsController(AppDbContext db, ICurrentUser me, INotificationService notify, IAuditService audit)
    { _db = db; _me = me; _notify = notify; _audit = audit; }

    private IQueryable<Appraisal> Base() => _db.Appraisals
        .Include(a => a.Employee).Include(a => a.Manager).Include(a => a.Cycle);

    // HR / leadership see every appraisal; managers see their reports + own; everyone else sees their own.
    private bool SeesAll => _me.Has(Permissions.AppraisalsRelease) || _me.Has(Permissions.PerformanceViewAll);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? cycleId)
    {
        var q = Base().AsNoTracking().AsQueryable();
        if (!SeesAll)
        {
            if (_me.Has(Permissions.AppraisalsManage))
                q = q.Where(a => a.ManagerId == _me.Id || a.EmployeeId == _me.Id);
            else
                q = q.Where(a => a.EmployeeId == _me.Id);
        }
        if (cycleId is int c) q = q.Where(a => a.CycleId == c);
        var list = await q.OrderByDescending(a => a.CreatedAt).ToListAsync();
        return Ok(list.Select(a => a.ToDto()));
    }

    /// <summary>Submit self assessment — only the appraisal's own employee, while still SelfPending.</summary>
    [HttpPost("{id:int}/self")]
    public async Task<IActionResult> SubmitSelf(int id, [FromBody] SelfAppraisalRequest req)
    {
        var a = await _db.Appraisals.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return Missing("Appraisal not found.");
        if (a.EmployeeId != _me.Id) return Denied("You can only submit your own self assessment.");
        if (a.Stage != AppraisalStage.SelfPending) return Conflict409("Self assessment has already been submitted.");

        a.SelfRating = req.SelfRating; a.SelfComments = req.SelfComments?.Trim();
        a.SelfSubmittedAt = DateTime.UtcNow; a.Stage = AppraisalStage.SelfSubmitted;
        await _db.SaveChangesAsync();
        if (a.ManagerId is int m)
            await _notify.NotifyAsync(m, NotificationType.General, "Manager evaluation required",
                "A team member submitted their self assessment — please evaluate.", "/appraisals");
        return Ok((await Base().AsNoTracking().FirstAsync(x => x.Id == id)).ToDto());
    }

    /// <summary>Manager evaluation — the assigned manager (or an HR/release-capable user), after self is submitted.</summary>
    [HttpPost("{id:int}/manager")]
    [Capability(Permissions.AppraisalsManage)]
    public async Task<IActionResult> SubmitManager(int id, [FromBody] ManagerAppraisalRequest req)
    {
        var a = await _db.Appraisals.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return Missing("Appraisal not found.");
        if (a.ManagerId != _me.Id && !SeesAll) return Denied("Only the assigned manager can evaluate this appraisal.");
        if (a.Stage == AppraisalStage.SelfPending) return Conflict409("Employee has not submitted their self assessment yet.");
        if (a.Stage == AppraisalStage.Released) return Conflict409("This appraisal has already been released.");

        a.ManagerRating = req.ManagerRating; a.ManagerComments = req.ManagerComments?.Trim();
        a.ManagerReviewedAt = DateTime.UtcNow; a.ManagerId ??= _me.Id; a.Stage = AppraisalStage.ManagerCompleted;
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(a.EmployeeId, NotificationType.General, "Manager evaluation completed",
            "Your manager has completed their evaluation. Awaiting final release.", "/appraisals");
        return Ok((await Base().AsNoTracking().FirstAsync(x => x.Id == id)).ToDto());
    }

    /// <summary>Release the final rating — HR / leadership only, after manager evaluation.</summary>
    [HttpPost("{id:int}/release")]
    [Capability(Permissions.AppraisalsRelease)]
    public async Task<IActionResult> Release(int id, [FromBody] ReleaseAppraisalRequest req)
    {
        var a = await _db.Appraisals.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return Missing("Appraisal not found.");
        if (a.Stage != AppraisalStage.ManagerCompleted) return Conflict409("Manager evaluation must be completed before release.");

        a.FinalRating = req.FinalRating; a.ReleasedAt = DateTime.UtcNow; a.Stage = AppraisalStage.Released;
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(a.EmployeeId, NotificationType.General, "Your rating has been released",
            $"Final rating: {req.FinalRating:0.0} / 5. Open your appraisals to see details.", "/appraisals");
        await _audit.WriteAsync(AuditAction.AppraisalReleased, "Appraisal", a.Id, $"Final rating released: {req.FinalRating:0.0}.");
        return Ok((await Base().AsNoTracking().FirstAsync(x => x.Id == id)).ToDto());
    }
}
