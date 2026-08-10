using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using Xunit;

namespace ArielVertex.Api.IntegrationTests;

public sealed class ConfidentialityAndPermissionsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public ConfidentialityAndPermissionsTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Published_feedback_never_exposes_internal_notes()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync("rahul@arielsoftwares.in");
        var response = await client.GetAsync("/api/v1/feedback/my-published");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("internalNotes", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Employee_cannot_fetch_draft_performance_report()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync("rahul@arielsoftwares.in");
        var draftId = await _factory.WithDbAsync(async db =>
        {
            var userId = await db.Users.Where(u => u.Email == "rahul@arielsoftwares.in").Select(u => u.Id).SingleAsync();
            var draft = new PerformanceReport
            {
                SubjectUserId = userId, PeriodType = PerformancePeriodType.Quarterly, Period = "TEST-DRAFT",
                OverallScore = 75, Rating = PerformanceRating.MeetsExpectations, IsPublished = false
            };
            db.PerformanceReports.Add(draft);
            await db.SaveChangesAsync();
            return draft.Id;
        });
        var response = await client.GetAsync($"/api/v1/performance/report/{draftId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Employee_cannot_use_management_endpoints()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync("rahul@arielsoftwares.in");
        var paths = new[]
        {
            "/api/v1/admin/audit", "/api/v1/performance/reports", "/api/v1/resources/occupancy",
            "/api/v1/analytics", "/api/v1/admin/config"
        };
        foreach (var path in paths)
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Business_project_status_projection_omits_internal_fields()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync("arveen@arielsoftwares.in");
        var projectId = await _factory.WithDbAsync(db => db.ProjectMembers
            .Where(m => m.User!.Email == "arveen@arielsoftwares.in" && m.IsActive).Select(m => m.ProjectId).FirstAsync());
        var response = await client.GetAsync($"/api/v1/projects/{projectId}/status-updates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("internalNote", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blockers", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dependencies", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_or_expired_style_token_is_rejected()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not-a-valid-jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/dashboard")).StatusCode);
    }
}
