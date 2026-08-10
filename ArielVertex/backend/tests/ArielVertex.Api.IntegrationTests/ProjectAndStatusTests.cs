using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArielVertex.Api.IntegrationTests;

public sealed class ProjectAndStatusTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public ProjectAndStatusTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Employee_only_sees_assigned_projects_and_cannot_open_unassigned_project()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync("rahul@arielsoftwares.in");
        var ids = await _factory.WithDbAsync(async db => new
        {
            Assigned = await db.ProjectMembers.Where(m => m.User!.Email == "rahul@arielsoftwares.in" && m.IsActive).Select(m => m.ProjectId).ToListAsync(),
            Unassigned = await db.Projects.Where(p => !p.Members.Any(m => m.User!.Email == "rahul@arielsoftwares.in" && m.IsActive)).Select(p => p.Id).FirstAsync()
        });

        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/projects?pageSize=100");
        var visibleIds = list.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToArray();
        Assert.NotEmpty(ids.Assigned);
        Assert.All(visibleIds, id => Assert.Contains(id, ids.Assigned));

        var denied = await client.GetAsync($"/api/v1/projects/{ids.Unassigned}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Employee_can_submit_assigned_status_but_not_unassigned_status()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync("rahul@arielsoftwares.in");
        var projectIds = await _factory.WithDbAsync(async db => new
        {
            Assigned = await db.ProjectMembers.Where(m => m.User!.Email == "rahul@arielsoftwares.in" && m.IsActive).Select(m => m.ProjectId).FirstAsync(),
            Unassigned = await db.Projects.Where(p => !p.Members.Any(m => m.User!.Email == "rahul@arielsoftwares.in" && m.IsActive)).Select(p => p.Id).FirstAsync()
        });
        object Payload(int projectId) => new
        {
            projectId, updateDate = DateTime.UtcNow, workCompleted = "Integration test work",
            nextPlannedWork = "Next", blockers = "Internal blocker", dependencies = "None",
            hoursSpent = 7.5, status = "OnTrack", internalNote = "Private", clientShareableSummary = "Progressing"
        };

        var accepted = await client.PostAsJsonAsync("/api/v1/status-updates", Payload(projectIds.Assigned));
        var denied = await client.PostAsJsonAsync("/api/v1/status-updates", Payload(projectIds.Unassigned));

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Status_validation_rejects_empty_work_and_out_of_range_hours()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync("rahul@arielsoftwares.in");
        var projectId = await _factory.WithDbAsync(db => db.ProjectMembers
            .Where(m => m.User!.Email == "rahul@arielsoftwares.in" && m.IsActive).Select(m => m.ProjectId).FirstAsync());

        var response = await client.PostAsJsonAsync("/api/v1/status-updates", new
        { projectId, updateDate = DateTime.UtcNow, workCompleted = "", hoursSpent = 25, status = "OnTrack" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Employee_cannot_create_projects()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync("rahul@arielsoftwares.in");
        var response = await client.PostAsJsonAsync("/api/v1/projects", new
        { code = "NOPE", name = "Denied", status = "Active", priority = "Medium", startDate = DateTime.UtcNow });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Blocked_document_extension_is_rejected()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync("shepherd@arielsoftwares.in");
        var projectId = await _factory.WithDbAsync(db => db.Projects.Where(p => p.Code == "MIB").Select(p => p.Id).SingleAsync());
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Danger", Encoding.UTF8), "Title");
        form.Add(new StringContent("General"), "Category");
        form.Add(new StringContent("Internal"), "Visibility");
        form.Add(new ByteArrayContent([0x4d, 0x5a]), "File", "payload.exe");

        var response = await client.PostAsync($"/api/v1/projects/{projectId}/documents/upload", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
