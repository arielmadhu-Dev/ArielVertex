using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArielVertex.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArielVertex.Api.IntegrationTests;

public sealed class WorkflowTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public WorkflowTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Feedback_moves_from_submitted_to_approved_to_published()
    {
        var ids = await _factory.WithDbAsync(async db => new
        {
            Rahul = await db.Users.Where(u => u.Email == "rahul@arielsoftwares.in").Select(u => u.Id).SingleAsync(),
            Project = await db.Projects.Where(p => p.Code == "MIB").Select(p => p.Id).SingleAsync()
        });
        using var manager = await _factory.CreateAuthenticatedClientAsync("shepherd@arielsoftwares.in");
        var created = await manager.PostAsJsonAsync("/api/v1/feedback", new
        {
            subjectUserId = ids.Rahul, projectId = ids.Project, period = "TEST-Q1",
            constructiveSummary = "Solid delivery", strengths = "Ownership", improvementAreas = "Testing",
            actionPlan = "Add tests", internalNotes = "Management only",
            categoryScores = new[] { new { category = "technical", score = 82, notApplicable = false, comment = "Good" } },
            submitNow = true
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var feedbackId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        using var hr = await _factory.CreateAuthenticatedClientAsync("mareena@arielsoftwares.in");
        Assert.Equal(HttpStatusCode.OK, (await hr.PostAsync($"/api/v1/feedback/{feedbackId}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await hr.PostAsync($"/api/v1/feedback/{feedbackId}/publish", null)).StatusCode);

        var status = await _factory.WithDbAsync(db => db.Feedbacks.Where(f => f.Id == feedbackId).Select(f => f.Status).SingleAsync());
        Assert.Equal(FeedbackStatus.Published, status);
    }

    [Fact]
    public async Task Expense_requires_approval_before_payment()
    {
        using var frontdesk = await _factory.CreateAuthenticatedClientAsync("amandeep@arielsoftwares.in");
        var created = await frontdesk.PostAsJsonAsync("/api/v1/expenses", new
        {
            title = "Integration test purchase", description = "Test", category = "OfficeSupplies",
            amount = 1250.50, vendor = "Test Vendor", expenseDate = DateTime.UtcNow,
            paymentMethod = "UPI", invoiceNumber = "TEST-1", approvalRequired = true
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.BadRequest, (await frontdesk.PostAsync($"/api/v1/expenses/{id}/pay", null)).StatusCode);
        using var accountant = await _factory.CreateAuthenticatedClientAsync("akshay@arielsoftwares.in");
        Assert.Equal(HttpStatusCode.OK, (await accountant.PostAsJsonAsync($"/api/v1/expenses/{id}/approve", new { note = "Approved" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await frontdesk.PostAsync($"/api/v1/expenses/{id}/pay", null)).StatusCode);
        var status = await _factory.WithDbAsync(db => db.Expenses.Where(e => e.Id == id).Select(e => e.Status).SingleAsync());
        Assert.Equal(ExpenseStatus.Paid, status);
    }

    [Fact]
    public async Task Review_request_can_be_scheduled_and_completed_by_assigned_reviewer()
    {
        var ids = await _factory.WithDbAsync(async db => new
        {
            Project = await db.Projects.Where(p => p.Code == "MIB").Select(p => p.Id).SingleAsync(),
            Rahul = await db.Users.Where(u => u.Email == "rahul@arielsoftwares.in").Select(u => u.Id).SingleAsync(),
            Nikhil = await db.Users.Where(u => u.Email == "nikhil@arielsoftwares.in").Select(u => u.Id).SingleAsync()
        });
        using var hr = await _factory.CreateAuthenticatedClientAsync("mareena@arielsoftwares.in");
        var created = await hr.PostAsJsonAsync("/api/v1/review-requests", new
        { projectId = ids.Project, subjectUserId = ids.Rahul, assignedToId = ids.Nikhil, reviewType = "CodeReview", notes = "Test" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        using var pm = await _factory.CreateAuthenticatedClientAsync("shepherd@arielsoftwares.in");
        var scheduled = await pm.PostAsJsonAsync($"/api/v1/review-requests/{id}/schedule", new
        { title = "Integration Review", description = "Test", scheduledAt = DateTime.UtcNow.AddDays(2), durationMinutes = 30, attendees = "", assignedToId = ids.Nikhil });
        Assert.Equal(HttpStatusCode.OK, scheduled.StatusCode);

        using var reviewer = await _factory.CreateAuthenticatedClientAsync("nikhil@arielsoftwares.in");
        var completed = await reviewer.PostAsJsonAsync($"/api/v1/review-requests/{id}/code-review", new
        { codeQualityRating = 4, architectureRating = 4, testingRating = 3, strengths = "Clear", observations = "More tests", actionItems = "Add coverage" });
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var status = await _factory.WithDbAsync(db => db.ReviewRequests.Where(r => r.Id == id).Select(r => r.Status).SingleAsync());
        Assert.Equal(ReviewRequestStatus.Completed, status);
    }

    [Fact]
    public async Task Minutes_follow_generate_approve_send_sequence_offline()
    {
        using var coordinator = await _factory.CreateAuthenticatedClientAsync("maria@arielsoftwares.in");
        var created = await coordinator.PostAsJsonAsync("/api/v1/minutes", new
        {
            title = "Integration Meeting", meetingDate = DateTime.UtcNow, location = "Teams",
            attendees = "rahul@arielsoftwares.in", rawNotes = "api done\nteam agreed to test\nrahul to verify by friday"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var generated = await coordinator.PostAsync($"/api/v1/minutes/{id}/generate", null);
        Assert.Equal(HttpStatusCode.OK, generated.StatusCode);
        var body = await generated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("generatedByAi").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("minutesText").GetString()));
        Assert.Equal(HttpStatusCode.OK, (await coordinator.PostAsync($"/api/v1/minutes/{id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await coordinator.PostAsync($"/api/v1/minutes/{id}/send", null)).StatusCode);
    }
}
