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

namespace ArielVertex.Api.Controllers;

/// <summary>
/// HR-configured, role-based appraisal forms. Each role has two independent forms — the employee's
/// Self assessment and the reporting manager's evaluation — plus the split used to blend their scores.
/// </summary>
[Authorize]
[Route("api/v1/appraisal-forms")]
public class AppraisalFormsController : ApiControllerBase
{
    /// <summary>Percentage of the final score contributed by the employee's self assessment.</summary>
    public const string SelfWeightKey = "appraisal.selfWeightPct";
    private const int DefaultSelfWeight = 30;

    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    public AppraisalFormsController(AppDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    internal static async Task<int> SelfWeightAsync(AppDbContext db, CancellationToken ct = default)
    {
        var raw = await db.PlatformSettings.AsNoTracking()
            .Where(s => s.Key == SelfWeightKey).Select(s => s.Value).FirstOrDefaultAsync(ct);
        return int.TryParse(raw, out var pct) ? Math.Clamp(pct, 0, 100) : DefaultSelfWeight;
    }

    private static AppraisalFormDto ToDto(AppraisalFormTemplate t) => new(
        t.Id, t.Role, t.Variant, t.Weighted,
        t.Areas.OrderBy(a => a.SortOrder)
            .Select(a => new AppraisalFormAreaDto(a.Name, a.Type, a.Weight, a.AllowNa)).ToList());

    private static AppraisalFormDto DefaultDto(string role, AppraisalFormVariant variant)
    {
        var (weighted, areas) = AppraisalFormCatalog.Default(role, variant);
        return new AppraisalFormDto(0, role, variant, weighted,
            areas.Select(a => new AppraisalFormAreaDto(a.Name, a.Type, a.Weight, a.AllowNa)).ToList());
    }

    /// <summary>Roles that have a form — built-in roles plus any custom role HR has saved.</summary>
    [HttpGet("roles")]
    public async Task<IActionResult> Roles(CancellationToken ct)
    {
        var saved = await _db.AppraisalFormTemplates.AsNoTracking().Select(t => t.Role).Distinct().ToListAsync(ct);
        var roles = saved.Concat(AppraisalFormCatalog.Roles)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(r => r).ToList();
        return Ok(new { roles, selfWeightPct = await SelfWeightAsync(_db, ct) });
    }

    /// <summary>The saved form for a role + stage, or the built-in default when none is saved yet.</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string role, [FromQuery] AppraisalFormVariant variant, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(role)) return BadInput("A role is required.");
        var t = await _db.AppraisalFormTemplates.Include(x => x.Areas).AsNoTracking()
            .FirstOrDefaultAsync(x => x.Role == role && x.Variant == variant, ct);
        return Ok(t is null ? DefaultDto(role.Trim(), variant) : ToDto(t));
    }

    /// <summary>Create or replace the form for a role + stage. Areas are stored in the order supplied.</summary>
    [HttpPut]
    [Capability(Permissions.ConfigManage)]
    public async Task<IActionResult> Save([FromBody] SaveAppraisalFormRequest req, CancellationToken ct)
    {
        var role = req.Role.Trim();
        if (role.Length == 0) return BadInput("A role is required.");
        if (req.Areas.Count == 0) return BadInput("Add at least one area before saving.");
        if (req.Areas.Any(a => string.IsNullOrWhiteSpace(a.Name)))
            return BadInput("Every area needs a name.");

        // A weighted manager form must add up, otherwise the resulting score is meaningless.
        if (req.Weighted && req.Variant == AppraisalFormVariant.Manager)
        {
            var total = req.Areas.Where(a => a.Type == AppraisalAreaType.Rating).Sum(a => a.Weight);
            if (total != 100) return BadInput($"Weighted areas must total 100% (currently {total}%).");
        }

        var t = await _db.AppraisalFormTemplates.Include(x => x.Areas)
            .FirstOrDefaultAsync(x => x.Role == role && x.Variant == req.Variant, ct);

        if (t is null)
        {
            t = new AppraisalFormTemplate { Role = role, Variant = req.Variant };
            _db.AppraisalFormTemplates.Add(t);
        }
        else
        {
            _db.AppraisalFormAreas.RemoveRange(t.Areas);
            t.Areas.Clear();
        }

        t.Weighted = req.Weighted;
        t.IsActive = true;
        var order = 0;
        foreach (var a in req.Areas)
            t.Areas.Add(new AppraisalFormArea
            {
                Name = a.Name.Trim(),
                Type = a.Type,
                Weight = a.Type == AppraisalAreaType.Rating ? Math.Clamp(a.Weight, 0, 100) : 0,
                AllowNa = a.Type == AppraisalAreaType.Rating && a.AllowNa,
                SortOrder = order++,
            });

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(AuditAction.ConfigChanged, "AppraisalFormTemplate", t.Id,
            $"{role} {req.Variant} appraisal form saved ({req.Areas.Count} areas).", ct);
        return Ok(ToDto(t));
    }

    /// <summary>Set how much the employee's self assessment contributes to the final score.</summary>
    [HttpPut("self-weight")]
    [Capability(Permissions.ConfigManage)]
    public async Task<IActionResult> SetSelfWeight([FromBody] SelfWeightRequest req, CancellationToken ct)
    {
        var pct = Math.Clamp(req.SelfWeightPct, 0, 100);
        var setting = await _db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == SelfWeightKey, ct);
        if (setting is null)
        {
            setting = new PlatformSetting
            {
                Key = SelfWeightKey, Group = "Appraisals", Type = "number",
                Label = "Self assessment weight (%)",
                Description = "Share of the final appraisal score contributed by the employee's self assessment; the manager contributes the rest.",
            };
            _db.PlatformSettings.Add(setting);
        }
        setting.Value = pct.ToString();
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(AuditAction.ConfigChanged, "PlatformSetting", setting.Id,
            $"Appraisal self weight set to {pct}% (manager {100 - pct}%).", ct);
        return Ok(new { selfWeightPct = pct, managerWeightPct = 100 - pct });
    }
}

public record SelfWeightRequest(int SelfWeightPct);
