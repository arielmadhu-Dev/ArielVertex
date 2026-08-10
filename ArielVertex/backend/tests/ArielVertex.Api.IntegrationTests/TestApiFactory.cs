using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ArielVertex.Infrastructure.Persistence;

namespace ArielVertex.Api.IntegrationTests;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    public const string SeedPassword = "IntegrationTest@123";
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(), $"arielvertex-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting is applied before the top-level Program reads JWT configuration.
        // The in-memory provider below also supplies these values to options-bound services.
        builder.UseEnvironment("Testing")
            .UseSetting("Jwt:Issuer", "ariel-vertex-tests")
            .UseSetting("Jwt:Audience", "ariel-vertex-tests-client")
            .UseSetting("Jwt:Secret", "integration-test-signing-key-with-at-least-32-bytes");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:Default"] = $"Data Source={_databasePath}",
                ["Auth:Mode"] = "Local",
                ["Auth:AllowedDomain"] = "arielsoftwares.in",
                ["Auth:SeedPassword"] = SeedPassword,
                ["Jwt:Issuer"] = "ariel-vertex-tests",
                ["Jwt:Audience"] = "ariel-vertex-tests-client",
                ["Jwt:Secret"] = "integration-test-signing-key-with-at-least-32-bytes",
                ["Integration:GraphMeetingsLive"] = "false",
                ["Integration:DirectorySyncLive"] = "false",
                ["Integration:OutlookNotificationsLive"] = "false",
                ["Ai:Enabled"] = "false",
                ["Automation:Enabled"] = "false",
                ["Storage:Root"] = Path.Combine(Path.GetTempPath(), $"arielvertex-files-{Guid.NewGuid():N}")
            });
        });
        builder.ConfigureServices(services =>
            services.AddDataProtection().UseEphemeralDataProtectionProvider());
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var client = CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = SeedPassword });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return client;
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
    }
}
