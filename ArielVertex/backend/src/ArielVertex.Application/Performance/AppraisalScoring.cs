namespace ArielVertex.Application.Performance;

/// <summary>
/// Turns 1-5 per-area ratings into a 0-100 score. Areas marked Not Applicable drop out and their
/// weight is redistributed across the rest, so nobody is penalised for a dimension that does not
/// apply to them (same principle as <see cref="PerformanceModel"/>).
/// </summary>
public static class AppraisalScoring
{
    public readonly record struct Row(int? Rating, int Weight, bool NotApplicable);

    /// <summary>0-100, or null when nothing usable was rated.</summary>
    public static int? Score(IEnumerable<Row> rows, bool weighted)
    {
        var usable = rows.Where(r => !r.NotApplicable && r.Rating is >= 1 and <= 5).ToList();
        if (usable.Count == 0) return null;

        if (!weighted)
            return (int)Math.Round((decimal)usable.Average(r => r.Rating!.Value) / 5m * 100m, MidpointRounding.AwayFromZero);

        var totalWeight = usable.Sum(r => r.Weight);
        if (totalWeight <= 0)   // weighted template but no weights set — fall back to a plain average
            return (int)Math.Round((decimal)usable.Average(r => r.Rating!.Value) / 5m * 100m, MidpointRounding.AwayFromZero);

        decimal acc = 0m;
        foreach (var r in usable)
            acc += r.Rating!.Value / 5m * 100m * (r.Weight / (decimal)totalWeight);
        return (int)Math.Round(acc, MidpointRounding.AwayFromZero);
    }

    /// <summary>Blend the employee's self score and the manager's score using the HR-configured split.</summary>
    public static int? Blend(int? selfScore, int? managerScore, int selfWeightPct)
    {
        if (selfScore is null && managerScore is null) return null;
        if (selfScore is null) return managerScore;
        if (managerScore is null) return selfScore;

        var s = Math.Clamp(selfWeightPct, 0, 100);
        return (int)Math.Round(selfScore.Value * s / 100m + managerScore.Value * (100 - s) / 100m,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>0-100 back to the 0-5 scale the Appraisal entity stores.</summary>
    public static decimal ToFivePoint(int score) => Math.Round(score / 20m, 2);
}
