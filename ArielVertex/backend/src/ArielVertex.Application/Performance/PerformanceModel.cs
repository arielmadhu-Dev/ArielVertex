using ArielVertex.Domain.Enums;

namespace ArielVertex.Application.Performance;

public record PerformanceCategory(string Key, string Name, string Factors, decimal DefaultWeight, bool CanBeNa);

/// <summary>
/// Transparent, configurable weighted scoring model (spec section 7). Weights redistribute
/// automatically when a category is marked Not Applicable, so a non-client-facing employee
/// isn't penalised for the Client Communication dimension.
/// </summary>
public static class PerformanceModel
{
    public static readonly IReadOnlyList<PerformanceCategory> Categories = new List<PerformanceCategory>
    {
        new("technical",     "Technical Competency",              "Logical thinking, problem-solving, code quality, debugging, architecture, testing awareness", 30m, false),
        new("delivery",      "Task Ownership & Delivery",         "Planning, estimation, timely delivery, requirement understanding, consistency",              20m, false),
        new("collaboration", "Team Collaboration",                "Communication, participation, knowledge sharing, feedback acceptance, supportiveness",       15m, false),
        new("client",        "Client & Stakeholder Communication","Clarity, professional tone, responsiveness, status explanation, requirement clarification",  10m, true),
        new("accountability","Accountability & Responsibility",   "Ownership, reliability, follow-through, escalation, attention to detail",                    15m, false),
        new("availability",  "Availability & Responsiveness",     "Availability, meeting discipline, blocker communication, status update discipline",          10m, false),
    };

    /// <summary>Map a 0-100 overall score to the organizational rating band (spec section 7).</summary>
    public static PerformanceRating RatingFor(decimal score) => score switch
    {
        >= 90 => PerformanceRating.Outstanding,
        >= 80 => PerformanceRating.ExceedsExpectations,
        >= 70 => PerformanceRating.MeetsExpectations,
        >= 60 => PerformanceRating.NeedsImprovement,
        _     => PerformanceRating.PerformanceAttentionRequired
    };

    public static string RatingLabel(PerformanceRating r) => r switch
    {
        PerformanceRating.Outstanding => "Outstanding",
        PerformanceRating.ExceedsExpectations => "Exceeds Expectations",
        PerformanceRating.MeetsExpectations => "Meets Expectations",
        PerformanceRating.NeedsImprovement => "Needs Improvement",
        _ => "Performance Attention Required"
    };

    /// <summary>
    /// Compute the weighted overall out of 100. <paramref name="scores"/> maps category key to a
    /// 0-100 value; keys present in <paramref name="notApplicable"/> are dropped and their weight
    /// is redistributed proportionally across the remaining categories.
    /// </summary>
    public static decimal Weighted(IReadOnlyDictionary<string, int> scores, ISet<string>? notApplicable = null)
    {
        notApplicable ??= new HashSet<string>();
        var active = Categories.Where(c => !notApplicable.Contains(c.Key)).ToList();
        var totalWeight = active.Sum(c => c.DefaultWeight);
        if (totalWeight <= 0) return 0m;

        decimal acc = 0m;
        foreach (var c in active)
        {
            if (!scores.TryGetValue(c.Key, out var s)) continue;
            var effectiveWeight = c.DefaultWeight / totalWeight; // redistributed
            acc += s * effectiveWeight;
        }
        return Math.Round(acc, 1);
    }
}
