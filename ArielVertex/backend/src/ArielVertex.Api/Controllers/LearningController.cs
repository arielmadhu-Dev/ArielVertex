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

/// <summary>Learning &amp; development: manual training assignment + a rule-based skill-gap recommender.</summary>
[Authorize]
[Route("api/v1/learning")]
public class LearningController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly INotificationService _notify;
    private readonly IAuditService _audit;
    public LearningController(AppDbContext db, ICurrentUser me, INotificationService notify, IAuditService audit)
    { _db = db; _me = me; _notify = notify; _audit = audit; }

    // KRA category → suggested training + typical duration (months). Keys match the goal categories.
    private static readonly Dictionary<string, (string training, int months)> CategoryTraining = new()
    {
        ["Project Delivery"] = ("Agile Delivery & Estimation Workshop", 2),
        ["Technical Growth"] = ("Advanced Engineering / Cloud Certification", 3),
        ["Quality & Ownership"] = ("Test Automation & Code Quality Bootcamp", 2),
        ["Collaboration"] = ("Stakeholder Communication Program", 1),
        ["Leadership"] = ("Leadership & Mentoring Program", 3),
        ["Process & Compliance"] = ("Process Excellence & Compliance Training", 1),
    };

    private bool SeesAll => _me.Has(Permissions.GoalsViewAll);
    private IQueryable<TrainingRecommendation> Scoped()
    {
        var q = _db.TrainingRecommendations.Include(t => t.Employee).AsNoTracking().AsQueryable();
        if (!SeesAll) q = q.Where(t => t.EmployeeId == _me.Id || t.Employee!.ManagerId == _me.Id);
        return q;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? employeeId)
    {
        var q = Scoped();
        if (employeeId is int e) q = q.Where(t => t.EmployeeId == e);
        var list = await q.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return Ok(list.Select(t => t.ToDto()));
    }

    [HttpPost]
    [Capability(Permissions.LearningManage)]
    public async Task<IActionResult> Create([FromBody] CreateTrainingRequest req)
    {
        var emp = await _db.Users.FindAsync(req.EmployeeId);
        if (emp is null) return BadInput("Unknown employee.");
        if (!SeesAll && emp.ManagerId != _me.Id) return Denied("You can only assign training to your reports.");

        var t = new TrainingRecommendation
        {
            EmployeeId = emp.Id, SkillGap = req.SkillGap.Trim(), RecommendedTraining = req.RecommendedTraining.Trim(),
            DurationMonths = req.DurationMonths <= 0 ? 3 : req.DurationMonths, Source = "Manual", CreatedById = _me.Id
        };
        _db.TrainingRecommendations.Add(t);
        await _db.SaveChangesAsync();
        await _notify.NotifyAsync(emp.Id, NotificationType.General, "New training assigned",
            $"{t.RecommendedTraining} ({t.DurationMonths} months)", "/learning");
        await _audit.WriteAsync(AuditAction.TrainingAssigned, "TrainingRecommendation", t.Id, $"{emp.Name}: {t.RecommendedTraining}.");
        return Ok((await Loaded(t.Id)).ToDto());
    }

    /// <summary>Rule-based recommender: weak KRA categories (avg goal progress &lt; 60%) → matching training.</summary>
    [HttpPost("recommend/{employeeId:int}")]
    [Capability(Permissions.LearningManage)]
    public async Task<IActionResult> Recommend(int employeeId)
    {
        var emp = await _db.Users.FindAsync(employeeId);
        if (emp is null) return Missing("Employee not found.");
        if (!SeesAll && emp.ManagerId != _me.Id) return Denied("You can only recommend training for your reports.");

        var goals = await _db.Goals.Where(g => g.EmployeeId == employeeId).AsNoTracking().ToListAsync();
        var weak = goals.Where(g => !string.IsNullOrWhiteSpace(g.Category))
            .GroupBy(g => g.Category).Where(grp => grp.Average(x => x.Progress) < 60).Select(grp => grp.Key).ToList();
        if (weak.Count == 0) weak.Add("Technical Growth");

        var existing = await _db.TrainingRecommendations
            .Where(t => t.EmployeeId == employeeId && t.Status != TrainingStatus.Completed)
            .Select(t => t.SkillGap).ToListAsync();

        var created = new List<TrainingRecommendation>();
        foreach (var cat in weak)
        {
            var gap = $"{cat} — below target";
            if (existing.Contains(gap)) continue;
            var (training, months) = CategoryTraining.TryGetValue(cat, out var v) ? v : ("Skill Development Program", 3);
            var t = new TrainingRecommendation
            {
                EmployeeId = employeeId, SkillGap = gap, RecommendedTraining = training,
                DurationMonths = months, Source = "AI", CreatedById = _me.Id
            };
            _db.TrainingRecommendations.Add(t);
            created.Add(t);
        }
        await _db.SaveChangesAsync();
        if (created.Count > 0)
            await _notify.NotifyAsync(employeeId, NotificationType.General, "Recommended learning added",
                $"{created.Count} training path(s) suggested based on your performance.", "/learning");

        var ids = created.Select(c => c.Id).ToList();
        var full = await _db.TrainingRecommendations.Include(t => t.Employee).AsNoTracking().Where(t => ids.Contains(t.Id)).ToListAsync();
        return Ok(full.Select(t => t.ToDto()));
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateTrainingStatusRequest req)
    {
        var t = await _db.TrainingRecommendations.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return Missing("Training not found.");
        var isOwner = t.EmployeeId == _me.Id;
        var isManager = t.Employee?.ManagerId == _me.Id;
        if (!isOwner && !isManager && !SeesAll && !_me.Has(Permissions.LearningManage))
            return Denied("You cannot update this training.");
        t.Status = req.Status;
        await _db.SaveChangesAsync();
        return Ok((await Loaded(t.Id)).ToDto());
    }

    [HttpDelete("{id:int}")]
    [Capability(Permissions.LearningManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var t = await _db.TrainingRecommendations.Include(x => x.Employee).FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return Missing("Training not found.");
        if (!SeesAll && t.Employee?.ManagerId != _me.Id) return Denied("You can only remove training for your reports.");
        _db.TrainingRecommendations.Remove(t);
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    private Task<TrainingRecommendation> Loaded(int id) =>
        _db.TrainingRecommendations.Include(t => t.Employee).AsNoTracking().FirstAsync(t => t.Id == id);
}
