using System.Text;
using ArielVertex.Api.Security;
using ArielVertex.Application.Security;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Api.Controllers;

/// <summary>Talent analytics: appraisal completion, rating distribution, department performance,
/// promotion &amp; training reports, a 9-box talent matrix, and a payroll CSV export. HR / leadership.</summary>
[Authorize]
[Route("api/v1/analytics")]
public class AnalyticsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    public AnalyticsController(AppDbContext db) => _db = db;

    [HttpGet]
    [Capability(Permissions.AnalyticsView)]
    public async Task<IActionResult> Get()
    {
        var appraisals = await _db.Appraisals.Include(a => a.Employee).ThenInclude(e => e!.Department).AsNoTracking().ToListAsync();
        var goals = await _db.Goals.AsNoTracking().ToListAsync();
        var promos = await _db.Promotions.AsNoTracking().ToListAsync();
        var trainings = await _db.TrainingRecommendations.AsNoTracking().ToListAsync();

        var total = appraisals.Count;
        var completed = appraisals.Count(a => a.Stage is AppraisalStage.ManagerCompleted or AppraisalStage.Released);
        var progressByEmp = goals.GroupBy(g => g.EmployeeId).ToDictionary(g => g.Key, g => g.Average(x => x.Progress));

        var talentMatrix = appraisals.Where(a => a.FinalRating != null).Select(a => new
        {
            name = a.Employee?.Name,
            department = a.Employee?.Department?.Name ?? "Unassigned",
            performance = Math.Round(a.FinalRating!.Value, 2),
            potential = Math.Round(progressByEmp.GetValueOrDefault(a.EmployeeId, 0), 0),
            quadrant = Quadrant((double)a.FinalRating!.Value, progressByEmp.GetValueOrDefault(a.EmployeeId, 0))
        });

        return Ok(new
        {
            pmsCompletion = new
            {
                total, completed, pending = total - completed,
                completionRate = total == 0 ? 0 : (int)Math.Round(100.0 * completed / total)
            },
            ratingDistribution = RatingBuckets(appraisals.Where(a => a.FinalRating != null).Select(a => a.FinalRating!.Value)),
            departmentPerformance = appraisals
                .Where(a => a.FinalRating != null && a.Employee?.Department != null)
                .GroupBy(a => a.Employee!.Department!.Name)
                .Select(g => new { department = g.Key, rating = Math.Round(g.Average(x => x.FinalRating!.Value), 2), count = g.Count() }),
            promotionReport = Enum.GetValues<PromotionStage>()
                .Select(s => new { stage = s.ToString(), count = promos.Count(p => p.Stage == s) }),
            trainingNeeds = trainings.GroupBy(t => t.Status.ToString()).Select(g => new { status = g.Key, count = g.Count() }),
            trainingByGap = trainings.GroupBy(t => t.SkillGap).Select(g => new { skillGap = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count).Take(8),
            talentMatrix
        });
    }

    /// <summary>Payroll export — CSV of finalized promotions with computed increment %.</summary>
    [HttpGet("payroll-export")]
    [Capability(Permissions.AnalyticsView)]
    public async Task<IActionResult> PayrollExport()
    {
        var promos = await _db.Promotions.Include(p => p.Employee)
            .Where(p => p.Stage == PromotionStage.Completed).AsNoTracking().ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("EmployeeId,Employee,NewDesignation,CurrentSalary,RevisedSalary,IncrementPct,EffectiveDate");
        foreach (var p in promos)
        {
            var pct = (p.CurrentSalary is null or 0 || p.ProposedSalary is null) ? 0
                : Math.Round((p.ProposedSalary.Value - p.CurrentSalary.Value) / p.CurrentSalary.Value * 100, 1);
            var date = p.CompletedAt?.ToString("yyyy-MM-dd") ?? "";
            sb.AppendLine($"{p.EmployeeId},{Csv(p.Employee?.Name)},{Csv(p.ProposedDesignation)},{p.CurrentSalary},{p.ProposedSalary},{pct},{date}");
        }
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "payroll-revisions.csv");
    }

    private static string Csv(string? s) => s is null ? "" : s.Contains(',') ? $"\"{s}\"" : s;

    private static string Quadrant(double rating, double potential)
    {
        var hiPerf = rating >= 3.5; var hiPot = potential >= 60;
        return (hiPerf, hiPot) switch
        {
            (true, true) => "Star",
            (true, false) => "Core Performer",
            (false, true) => "High Potential",
            _ => "Needs Attention"
        };
    }

    private static IEnumerable<object> RatingBuckets(IEnumerable<decimal> ratings)
    {
        var list = ratings.ToList();
        object B(string label, Func<decimal, bool> p) => new { label, count = list.Count(p) };
        return new[]
        {
            B("Outstanding (4.5-5)", r => r >= 4.5m),
            B("Excellent (4-4.5)", r => r >= 4m && r < 4.5m),
            B("Good (3-4)", r => r >= 3m && r < 4m),
            B("Needs Improvement (<3)", r => r < 3m),
        };
    }
}
