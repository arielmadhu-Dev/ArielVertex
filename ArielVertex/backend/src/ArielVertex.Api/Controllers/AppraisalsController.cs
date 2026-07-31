using ArielVertex.Api.Common;
using ArielVertex.Api.Security;
using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Contracts;
using ArielVertex.Application.Performance;
using ArielVertex.Application.Security;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

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
    private readonly IPlatformConfig _config;
    public AppraisalsController(AppDbContext db, ICurrentUser me, INotificationService notify, IAuditService audit, IPlatformConfig config)
    { _db = db; _me = me; _notify = notify; _audit = audit; _config = config; }

    /// <summary>Non-empty token value, or a safe fallback so a template never renders "{employee}" literally.</summary>
    private static string Tok(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    /// <summary>Render an admin-editable template; if it's missing/blank, fall back to built-in wording.</summary>
    private async Task NotifyTemplatedAsync(int recipientId, string templateKey,
        Dictionary<string, string> tokens, string fallbackTitle, string fallbackBody, CancellationToken ct)
    {
        var (subject, body) = await _config.RenderAsync(templateKey, tokens, ct);
        if (string.IsNullOrWhiteSpace(subject)) subject = fallbackTitle;
        if (string.IsNullOrWhiteSpace(body)) body = fallbackBody;
        await _notify.NotifyAsync(recipientId, NotificationType.General, subject, body, "/appraisals", ct);
    }

    private record AppraisalMeta(string? Employee, string? Role, string? Cycle);
    private Task<AppraisalMeta> MetaAsync(int id, CancellationToken ct) => _db.Appraisals.AsNoTracking()
        .Where(x => x.Id == id)
        .Select(x => new AppraisalMeta(x.Employee!.Name, x.Employee!.Designation, x.Cycle!.Name))
        .FirstAsync(ct);

    private IQueryable<Appraisal> Base() => _db.Appraisals
        .Include(a => a.Employee).Include(a => a.Manager).Include(a => a.Cycle);

    /// <summary>
    /// Replace one stage's per-area answers and return the resulting 0-100 score. The role's saved
    /// form (or the built-in default) supplies the weights, so a rater can't invent their own.
    /// </summary>
    private async Task<int?> ApplyAreaScoresAsync(Appraisal a, AppraisalFormVariant stage,
        List<AreaScoreInput>? areas, CancellationToken ct)
    {
        if (areas is null || areas.Count == 0) return null;

        var role = await _db.Users.AsNoTracking().Where(u => u.Id == a.EmployeeId)
            .Select(u => u.Designation).FirstOrDefaultAsync(ct) ?? string.Empty;

        var template = await _db.AppraisalFormTemplates.Include(t => t.Areas).AsNoTracking()
            .FirstOrDefaultAsync(t => t.Role == role && t.Variant == stage, ct);

        bool weighted;
        Dictionary<string, (int Weight, bool AllowNa, AppraisalAreaType Type)> defs;
        if (template is not null)
        {
            weighted = template.Weighted;
            defs = template.Areas.ToDictionary(x => x.Name, x => (x.Weight, x.AllowNa, x.Type), StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            var (w, list) = AppraisalFormCatalog.Default(role, stage);
            weighted = w;
            defs = list.ToDictionary(x => x.Name, x => (x.Weight, x.AllowNa, x.Type), StringComparer.OrdinalIgnoreCase);
        }

        var existing = await _db.AppraisalAreaScores.Where(s => s.AppraisalId == a.Id && s.Stage == stage).ToListAsync(ct);
        if (existing.Count > 0) _db.AppraisalAreaScores.RemoveRange(existing);

        var rows = new List<AppraisalScoring.Row>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in areas)
        {
            if (input is null) continue;
            var name = input.AreaName?.Trim() ?? string.Empty;
            // Ignore blank/unknown areas, and guard against a duplicate area in one payload
            // (the unique index would otherwise reject the whole save).
            if (name.Length == 0 || !defs.TryGetValue(name, out var def) || !seen.Add(name)) continue;

            var na = def.AllowNa && input.NotApplicable;
            _db.AppraisalAreaScores.Add(new AppraisalAreaScore
            {
                AppraisalId = a.Id, Stage = stage, AreaName = name,
                Rating = def.Type == AppraisalAreaType.Rating && !na ? input.Rating : null,
                Comment = input.Comment?.Trim(),
                NotApplicable = na,
            });

            if (def.Type == AppraisalAreaType.Rating)
                rows.Add(new AppraisalScoring.Row(input.Rating, def.Weight, na));
        }

        return AppraisalScoring.Score(rows, weighted);
    }

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

        // When the role's Self form is used the score is computed server-side from the areas;
        // the flat rating in the request is only a fallback for the legacy single-rating client.
        var selfScore = await ApplyAreaScoresAsync(a, AppraisalFormVariant.Self, req.Areas, HttpContext.RequestAborted);
        a.SelfRating = selfScore is int s ? AppraisalScoring.ToFivePoint(s) : req.SelfRating;
        a.SelfComments = req.SelfComments?.Trim();
        a.SelfSubmittedAt = DateTime.UtcNow; a.Stage = AppraisalStage.SelfSubmitted;
        await _db.SaveChangesAsync();
        if (a.ManagerId is int m)
        {
            var ct = HttpContext.RequestAborted;
            var meta = await MetaAsync(id, ct);
            var employee = Tok(meta.Employee, "A team member");
            await NotifyTemplatedAsync(m, "appraisal.selfSubmitted", new()
                {
                    ["employee"] = employee,
                    ["role"] = Tok(meta.Role, "their role"),
                    ["cycle"] = Tok(meta.Cycle, "the current cycle"),
                },
                "Manager evaluation required",
                $"{employee} submitted their self assessment — please evaluate.", ct);
        }
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

        var managerScore = await ApplyAreaScoresAsync(a, AppraisalFormVariant.Manager, req.Areas, HttpContext.RequestAborted);
        a.ManagerRating = managerScore is int m ? AppraisalScoring.ToFivePoint(m) : req.ManagerRating;
        a.ManagerComments = req.ManagerComments?.Trim();
        a.ManagerReviewedAt = DateTime.UtcNow; a.ManagerId ??= _me.Id; a.Stage = AppraisalStage.ManagerCompleted;
        await _db.SaveChangesAsync();
        {
            var ct = HttpContext.RequestAborted;
            var meta = await MetaAsync(id, ct);
            await NotifyTemplatedAsync(a.EmployeeId, "appraisal.managerCompleted", new()
                {
                    ["employee"] = Tok(meta.Employee, "there"),
                    ["manager"] = Tok(_me.Name, "Your manager"),
                    ["cycle"] = Tok(meta.Cycle, "the current cycle"),
                },
                "Manager evaluation completed",
                "Your manager has completed their evaluation. Awaiting final release.", ct);
        }
        return Ok((await Base().AsNoTracking().FirstAsync(x => x.Id == id)).ToDto());
    }

    /// <summary>
    /// Both stages' scores plus the blended result HR sees before releasing. Visible to the
    /// appraisal's employee, their manager, and anyone who can release.
    /// </summary>
    [HttpGet("{id:int}/scores")]
    public async Task<IActionResult> Scores(int id, CancellationToken ct)
    {
        var a = await _db.Appraisals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return Missing("Appraisal not found.");
        if (a.EmployeeId != _me.Id && a.ManagerId != _me.Id && !SeesAll) return Denied();

        var scores = await _db.AppraisalAreaScores.AsNoTracking()
            .Where(s => s.AppraisalId == id).OrderBy(s => s.Id).ToListAsync(ct);

        // Stored ratings are 0-5; the UI works in 0-100.
        int? selfScore = a.SelfRating is decimal sr ? (int)Math.Round(sr * 20m) : null;
        int? managerScore = a.ManagerRating is decimal mr ? (int)Math.Round(mr * 20m) : null;
        var selfWeight = await AppraisalFormsController.SelfWeightAsync(_db, ct);

        return Ok(new AppraisalScoreSummaryDto(
            selfScore, managerScore,
            AppraisalScoring.Blend(selfScore, managerScore, selfWeight),
            selfWeight, 100 - selfWeight,
            scores.Select(s => new AppraisalAreaScoreDto(s.AreaName, s.Stage, s.Rating, s.Comment, s.NotApplicable)).ToList()));
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
        {
            var ct = HttpContext.RequestAborted;
            var meta = await MetaAsync(id, ct);
            var band = PerformanceModel.RatingLabel(PerformanceModel.RatingFor(req.FinalRating * 20m));
            var rating = req.FinalRating.ToString("0.0", CultureInfo.InvariantCulture);
            await NotifyTemplatedAsync(a.EmployeeId, "appraisal.released", new()
                {
                    ["employee"] = Tok(meta.Employee, "there"),
                    ["cycle"] = Tok(meta.Cycle, "your latest cycle"),
                    ["role"] = Tok(meta.Role, "your role"),
                    ["rating"] = rating,
                    ["band"] = band,
                },
                "Your rating has been released",
                $"Final rating: {rating} / 5 — {band}. Open My Performance to see details.", ct);
        }
        await _audit.WriteAsync(AuditAction.AppraisalReleased, "Appraisal", a.Id, $"Final rating released: {req.FinalRating:0.0}.");
        return Ok((await Base().AsNoTracking().FirstAsync(x => x.Id == id)).ToDto());
    }
}
