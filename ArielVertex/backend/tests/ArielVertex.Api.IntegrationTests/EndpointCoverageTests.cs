using System.Net;
using Xunit;

namespace ArielVertex.Api.IntegrationTests;

public sealed class EndpointCoverageTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public EndpointCoverageTests(TestApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/v1/dashboard")]
    [InlineData("/api/v1/projects")]
    [InlineData("/api/v1/status-updates/mine")]
    [InlineData("/api/v1/review-requests")]
    [InlineData("/api/v1/feedback")]
    [InlineData("/api/v1/performance/my")]
    [InlineData("/api/v1/resource-requests")]
    [InlineData("/api/v1/resources/occupancy")]
    [InlineData("/api/v1/notifications")]
    [InlineData("/api/v1/employees")]
    [InlineData("/api/v1/admin/audit")]
    [InlineData("/api/v1/admin/config")]
    [InlineData("/api/v1/expense-settings")]
    [InlineData("/api/v1/expenses")]
    [InlineData("/api/v1/pip/mine")]
    [InlineData("/api/v1/minutes")]
    [InlineData("/api/v1/cycles")]
    [InlineData("/api/v1/appraisals")]
    [InlineData("/api/v1/appraisal-forms/roles")]
    [InlineData("/api/v1/goals")]
    [InlineData("/api/v1/promotions")]
    [InlineData("/api/v1/learning")]
    [InlineData("/api/v1/analytics")]
    [InlineData("/api/v1/search?q=rahul")]
    [InlineData("/api/v1/meta/enums")]
    public async Task Protected_endpoint_rejects_anonymous_callers(string path)
    {
        using var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Hr_manager_can_query_all_hr_owned_modules()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync("mareena@arielsoftwares.in");
        var paths = new[]
        {
            "/api/v1/dashboard", "/api/v1/projects", "/api/v1/review-requests", "/api/v1/feedback",
            "/api/v1/performance/reports", "/api/v1/resource-requests", "/api/v1/resources/occupancy",
            "/api/v1/employees", "/api/v1/pip", "/api/v1/minutes", "/api/v1/cycles",
            "/api/v1/appraisals", "/api/v1/appraisal-forms/roles", "/api/v1/goals",
            "/api/v1/promotions", "/api/v1/learning", "/api/v1/analytics"
        };
        foreach (var path in paths)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task System_admin_can_query_configuration_and_audit_modules()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync("rajat@arielsoftwares.in");
        foreach (var path in new[] { "/api/v1/admin/audit", "/api/v1/admin/sync-logs", "/api/v1/admin/config", "/api/v1/admin/templates", "/api/v1/admin/integration-status" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Employee_notification_operations_are_scoped_and_functional()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync("rahul@arielsoftwares.in");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/v1/notifications/read-all", null)).StatusCode);
    }
}
