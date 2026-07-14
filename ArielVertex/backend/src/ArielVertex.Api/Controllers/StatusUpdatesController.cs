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
[Route("api/v1/status-updates")]
public class StatusUpdatesController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IProjectAccessService _access;
    public StatusUpdatesController(AppDbContext db, ICurrentUser me, IProjectAccessService access)
    { _db = db; _me = me; _access = access; }

    /// <summary>The caller's own recent updates, plus the projects they may submit against.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var mine = await _db.StatusUpdates.Include(s => s.Project).Include(s => s.User)
            .Where(s => s.UserId == _me.Id).AsNoTracking()
            .OrderByDescending(s => s.UpdateDate).Take(30).ToListAsync();

        var assignable = await _db.ProjectMembers.Include(m => m.Project)
            .Where(m => m.UserId == _me.Id && m.IsActive)
            .Select(m => new { m.ProjectId, Name = m.Project!.Name }).ToListAsync();

        return Ok(new { updates = mine.Select(s => s.ToDto(true)), assignableProjects = assignable });
    }

    [HttpPost]
    [Capability(Permissions.StatusSubmit)]
    public async Task<IActionResult> Create([FromBody] CreateStatusUpdateRequest req)
    {
        // Validation: may only submit for a project the caller is actively assigned to (spec 6.6).
        var assigned = await _db.ProjectMembers
            .AnyAsync(m => m.ProjectId == req.ProjectId && m.UserId == _me.Id && m.IsActive);
        if (!assigned) return Denied("You can only submit updates for projects assigned to you.");

        var s = new StatusUpdate
        {
            ProjectId = req.ProjectId, UserId = _me.Id, UpdateDate = req.UpdateDate == default ? DateTime.UtcNow.Date : req.UpdateDate.ToUniversalTime().Date,
            WorkCompleted = req.WorkCompleted.Trim(), NextPlannedWork = req.NextPlannedWork?.Trim() ?? "",
            Blockers = req.Blockers?.Trim() ?? "", Dependencies = req.Dependencies?.Trim() ?? "",
            HoursSpent = req.HoursSpent, Status = req.Status, InternalNote = req.InternalNote?.Trim() ?? "",
            ClientShareableSummary = req.ClientShareableSummary?.Trim() ?? ""
        };
        _db.StatusUpdates.Add(s);
        await _db.SaveChangesAsync();
        return Ok(new { s.Id });
    }

    /// <summary>
    /// Consolidated business-ready summary for a project (spec 6.6). Splits client-shareable
    /// content from internal detail and highlights members who have not reported in the window.
    /// </summary>
    [HttpGet("consolidated/{projectId:int}")]
    public async Task<IActionResult> Consolidated(int projectId, [FromQuery] int days = 7)
    {
        if (!await _access.CanViewAsync(projectId)) return Denied();
        var project = await _db.Projects.FindAsync(projectId);
        if (project is null) return Missing("Project not found.");

        var from = DateTime.UtcNow.Date.AddDays(-Math.Clamp(days, 1, 60));
        var updates = await _db.StatusUpdates.Include(s => s.User).Include(s => s.Project)
            .Where(s => s.ProjectId == projectId && s.UpdateDate >= from).AsNoTracking().ToListAsync();

        var expected = await _db.ProjectMembers.Include(m => m.User)
            .Where(m => m.ProjectId == projectId && m.IsActive &&
                        (m.RoleOnProject == ProjectRole.Developer || m.RoleOnProject == ProjectRole.QA))
            .ToListAsync();
        var reportedUserIds = updates.Select(u => u.UserId).ToHashSet();
        var missing = expected.Where(m => !reportedUserIds.Contains(m.UserId)).Select(m => m.User!.Name).ToArray();

        var dto = new ConsolidatedSummaryDto(
            projectId, project.Name, from, DateTime.UtcNow.Date, updates.Count,
            reportedUserIds.Count, expected.Count, missing,
            updates.OrderByDescending(u => u.UpdateDate).Select(u => u.ToBusinessDto()).ToList());
        return Ok(dto);
    }
}
