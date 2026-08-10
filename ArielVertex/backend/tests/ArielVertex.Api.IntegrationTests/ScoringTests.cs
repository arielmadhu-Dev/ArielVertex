using ArielVertex.Application.Performance;
using ArielVertex.Domain.Enums;
using Xunit;

namespace ArielVertex.Api.IntegrationTests;

public sealed class ScoringTests
{
    [Theory]
    [InlineData(100, PerformanceRating.Outstanding)]
    [InlineData(90, PerformanceRating.Outstanding)]
    [InlineData(80, PerformanceRating.ExceedsExpectations)]
    [InlineData(70, PerformanceRating.MeetsExpectations)]
    [InlineData(60, PerformanceRating.NeedsImprovement)]
    [InlineData(59.9, PerformanceRating.PerformanceAttentionRequired)]
    public void Rating_bands_have_correct_boundaries(decimal score, PerformanceRating expected) =>
        Assert.Equal(expected, PerformanceModel.RatingFor(score));

    [Fact]
    public void Weighted_score_redistributes_not_applicable_category()
    {
        var scores = PerformanceModel.Categories.ToDictionary(c => c.Key, _ => 80);
        scores["client"] = 0;
        Assert.Equal(80m, PerformanceModel.Weighted(scores, new HashSet<string> { "client" }));
    }

    [Fact]
    public void Appraisal_score_ignores_not_applicable_and_invalid_rows()
    {
        var rows = new[]
        {
            new AppraisalScoring.Row(5, 50, false), new AppraisalScoring.Row(1, 50, true),
            new AppraisalScoring.Row(null, 50, false)
        };
        Assert.Equal(100, AppraisalScoring.Score(rows, weighted: true));
    }

    [Theory]
    [InlineData(80, 60, 50, 70)]
    [InlineData(80, 60, 100, 80)]
    [InlineData(80, 60, 0, 60)]
    public void Appraisal_blend_respects_configured_split(int self, int manager, int selfWeight, int expected) =>
        Assert.Equal(expected, AppraisalScoring.Blend(self, manager, selfWeight));

    [Fact]
    public void Appraisal_score_returns_null_without_usable_ratings() =>
        Assert.Null(AppraisalScoring.Score([new(null, 100, false), new(5, 100, true)], weighted: true));
}
