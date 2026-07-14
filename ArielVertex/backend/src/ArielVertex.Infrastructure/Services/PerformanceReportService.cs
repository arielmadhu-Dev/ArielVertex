using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Performance;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Infrastructure.Services;

/// <summary>
/// Transparent performance report generation (spec section 7). Each category is derived from
/// real evidence in the period; categories with no evidence fall back to a neutral 72 and say so
/// in DataSources. Client &amp; Stakeholder Communication is marked N/A (weight redistributed) when
/// there is no client-facing evidence, so non-client roles aren't penalised.
/// </summary>
public class PerformanceReportService : IPerformanceReportService
{
    private readonly AppDbContext _db;
    public PerformanceReportService(AppDbContext db) => _db = db;

    public async Task<int> GenerateAsync(int subjectUserId, PerformancePeriodType periodType, string period, CancellationToken ct = default)
    {
        var (start, end) = Range(periodType, period);

        // ---- Evidence ----
        var feedbackScores = await _db.FeedbackCategoryScores
            .Include(c => c.Feedback)
            .Where(c => c.Feedback!.SubjectUserId == subjectUserId &&
                        (c.Feedback.Period == period || (c.Feedback.CreatedAt >= start && c.Feedback.CreatedAt <= end)))
            .AsNoTracking().ToListAsync(ct);

        var codeReviews = await _db.CodeReviews.Include(c => c.ReviewRequest)
            .Where(c => c.ReviewRequest!.SubjectUserId == subjectUserId && c.SubmittedAt >= start && c.SubmittedAt <= end)
            .AsNoTracking().ToListAsync(ct);
        var projReviews = await _db.ProjectReviews.Include(p => p.ReviewRequest)
            .Where(p => p.ReviewRequest!.SubjectUserId == subjectUserId && p.SubmittedAt >= start && p.SubmittedAt <= end)
            .AsNoTracking().ToListAsync(ct);

        var updates = await _db.StatusUpdates
            .Where(s => s.UserId == subjectUserId && s.UpdateDate >= start && s.UpdateDate <= end)
            .AsNoTracking().ToListAsync(ct);

        var reportedDays = updates.Select(u => u.UpdateDate.Date).Distinct().Count();
        var expectedDays = Math.Max(1, WeekdaysBetween(start, end) / 2); // expect ~2 updates/week
        var statusConsistency = Math.Clamp((decimal)reportedDays / expectedDays * 100m, 0, 100);
        var blockerRate = updates.Count == 0 ? 0 : (decimal)updates.Count(u => u.Status == UpdateStatus.Blocked) / updates.Count;

        decimal Scale5(int r) => Math.Clamp(r, 1, 5) / 5m * 100m;
        decimal? FeedbackAvg(string key)
        {
            var vals = feedbackScores.Where(c => c.Category == key && !c.NotApplicable).Select(c => (decimal)c.Score).ToList();
            return vals.Count > 0 ? vals.Average() : (decimal?)null;
        }
        // Average only the signals that actually exist for a category.
        int Combine(params decimal?[] signals)
        {
            var present = signals.Where(s => s.HasValue).Select(s => s!.Value).ToList();
            return present.Count > 0 ? (int)Math.Round(present.Average()) : 72; // 72 = neutral fallback
        }

        var codeAvg = codeReviews.Count > 0 ? (decimal?)codeReviews.Average(c => (Scale5(c.CodeQualityRating) + Scale5(c.ArchitectureRating) + Scale5(c.TestingRating)) / 3m) : null;
        var projDelivery = projReviews.Count > 0 ? (decimal?)projReviews.Average(p => Scale5(p.DeliveryRating)) : null;
        var projOwnership = projReviews.Count > 0 ? (decimal?)projReviews.Average(p => Scale5(p.OwnershipRating)) : null;
        var projCollab = projReviews.Count > 0 ? (decimal?)projReviews.Average(p => Scale5(p.CollaborationRating)) : null;

        var scores = new Dictionary<string, int>
        {
            ["technical"] = Combine(codeAvg, FeedbackAvg("technical")),
            ["delivery"] = Combine(projDelivery, FeedbackAvg("delivery"), updates.Count > 0 ? statusConsistency : (decimal?)null),
            ["collaboration"] = Combine(projCollab, FeedbackAvg("collaboration")),
            ["accountability"] = Combine(projOwnership, FeedbackAvg("accountability"), updates.Count > 0 ? (100m - blockerRate * 40m) : (decimal?)null),
            ["availability"] = Combine(FeedbackAvg("availability"), updates.Count > 0 ? statusConsistency : (decimal?)null),
        };

        var na = new HashSet<string>();
        var clientSignal = FeedbackAvg("client");
        var clientNa = feedbackScores.Any(c => c.Category == "client" && c.NotApplicable) || clientSignal is null;
        if (clientNa) na.Add("client"); else scores["client"] = Combine(clientSignal);

        var overall = PerformanceModel.Weighted(scores, na);
        var rating = PerformanceModel.RatingFor(overall);

        var sources = new List<string>();
        if (feedbackScores.Select(c => c.FeedbackId).Distinct().Any()) sources.Add($"Feedback ({feedbackScores.Select(c => c.FeedbackId).Distinct().Count()})");
        if (codeReviews.Count > 0) sources.Add($"Code review ({codeReviews.Count})");
        if (projReviews.Count > 0) sources.Add($"Project review ({projReviews.Count})");
        sources.Add($"Status updates ({reportedDays}/{expectedDays} expected days)");
        if (sources.Count == 1) sources.Insert(0, "Limited evidence — neutral baseline applied");

        // ---- Upsert (idempotent per subject + period) ----
        var existing = await _db.PerformanceReports.Include(r => r.CategoryScores)
            .FirstOrDefaultAsync(r => r.SubjectUserId == subjectUserId && r.Period == period, ct);
        if (existing is not null)
        {
            _db.PerformanceCategoryScores.RemoveRange(existing.CategoryScores);
            existing.OverallScore = overall; existing.Rating = rating; existing.PeriodType = periodType;
            existing.DataSources = string.Join(", ", sources);
            existing.Strengths = StrengthText(scores, na); existing.ImprovementAreas = ImproveText(scores, na);
            existing.RecommendedActions = ActionText(scores, na);
            existing.IsPublished = false; existing.PublishedAt = null; existing.ApprovedById = null;
            AttachCategories(existing, scores, na);
            await _db.SaveChangesAsync(ct);
            return existing.Id;
        }

        var report = new PerformanceReport
        {
            SubjectUserId = subjectUserId, PeriodType = periodType, Period = period,
            OverallScore = overall, Rating = rating, DataSources = string.Join(", ", sources),
            Strengths = StrengthText(scores, na), ImprovementAreas = ImproveText(scores, na),
            RecommendedActions = ActionText(scores, na), IsPublished = false,
        };
        _db.PerformanceReports.Add(report);
        await _db.SaveChangesAsync(ct);
        AttachCategories(report, scores, na);
        await _db.SaveChangesAsync(ct);
        return report.Id;
    }

    private void AttachCategories(PerformanceReport report, Dictionary<string, int> scores, HashSet<string> na)
    {
        foreach (var c in PerformanceModel.Categories)
        {
            var isNa = na.Contains(c.Key);
            _db.PerformanceCategoryScores.Add(new PerformanceCategoryScore
            {
                PerformanceReportId = report.Id, Category = c.Key,
                Score = isNa ? 0 : scores.GetValueOrDefault(c.Key, 72),
                Weight = c.DefaultWeight, NotApplicable = isNa
            });
        }
    }

    private static string TopCategory(Dictionary<string, int> s, HashSet<string> na, bool highest)
    {
        var active = s.Where(kv => !na.Contains(kv.Key)).ToList();
        if (active.Count == 0) return "";
        var pick = highest ? active.OrderByDescending(kv => kv.Value).First() : active.OrderBy(kv => kv.Value).First();
        return PerformanceModel.Categories.First(c => c.Key == pick.Key).Name;
    }
    private static string StrengthText(Dictionary<string, int> s, HashSet<string> na) => $"Strongest in {TopCategory(s, na, true)}, backed by consistent delivery signals.";
    private static string ImproveText(Dictionary<string, int> s, HashSet<string> na) => $"Focus area: {TopCategory(s, na, false)}.";
    private static string ActionText(Dictionary<string, int> s, HashSet<string> na) => $"Set a concrete goal around {TopCategory(s, na, false)} and review progress next cycle.";

    private static (DateTime, DateTime) Range(PerformancePeriodType t, string period)
    {
        if (t == PerformancePeriodType.Quarterly)
        {
            var parts = period.Split('-'); // 2026-Q3
            var year = int.Parse(parts[0]);
            var q = int.Parse(parts[1].TrimStart('Q', 'q'));
            var startMonth = (q - 1) * 3 + 1;
            var start = new DateTime(year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
            return (start, start.AddMonths(3).AddDays(-1));
        }
        else
        {
            var parts = period.Split('-'); // 2026-07
            var start = new DateTime(int.Parse(parts[0]), int.Parse(parts[1]), 1, 0, 0, 0, DateTimeKind.Utc);
            return (start, start.AddMonths(1).AddDays(-1));
        }
    }

    private static int WeekdaysBetween(DateTime a, DateTime b)
    {
        int n = 0;
        for (var d = a.Date; d <= b.Date; d = d.AddDays(1))
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday) n++;
        return n;
    }
}
