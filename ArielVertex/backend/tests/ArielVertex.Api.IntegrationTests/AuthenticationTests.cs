using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ArielVertex.Api.IntegrationTests;

public sealed class AuthenticationTests : IClassFixture<TestApiFactory>
{
    private const string SeedPassword = TestApiFactory.SeedPassword;
    private readonly HttpClient _client;

    public AuthenticationTests(TestApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_is_available_without_authentication()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Me_rejects_anonymous_requests()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_rejects_invalid_credentials()
    {
        var response = await LoginAsync("rahul@arielsoftwares.in", "wrong-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_credentials", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_rejects_invalid_request_shape()
    {
        var response = await LoginAsync("not-an-email", "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Seeded_employee_can_login_and_read_own_identity()
    {
        var login = await LoginAsync("rahul@arielsoftwares.in", SeedPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var auth = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = auth.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal("rahul@arielsoftwares.in", auth.GetProperty("user").GetProperty("email").GetString());

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var currentUser = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("rahul@arielsoftwares.in", currentUser.GetProperty("email").GetString());
        Assert.Equal("employee", currentUser.GetProperty("dashboard").GetString());
    }

    [Fact]
    public async Task Employee_cannot_read_admin_audit_log()
    {
        var login = await LoginAsync("rahul@arielsoftwares.in", SeedPassword);
        var auth = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = auth.GetProperty("token").GetString();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/audit");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
}
