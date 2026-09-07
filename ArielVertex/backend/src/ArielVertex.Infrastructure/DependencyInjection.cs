using ArielVertex.Application.Abstractions;
using ArielVertex.Infrastructure.Auth;
using ArielVertex.Infrastructure.Integration;
using ArielVertex.Infrastructure.Persistence;
using ArielVertex.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArielVertex.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // DB provider switch: SQLite for zero-setup dev, PostgreSQL for production (config-driven).
        var provider = (config["Database:Provider"] ?? "Sqlite").Trim();
        var conn = config.GetConnectionString("Default");
        services.AddDbContext<AppDbContext>(o =>
        {
            if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) || provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                o.UseNpgsql(conn ?? "Host=localhost;Database=arielvertex;Username=postgres;Password=postgres");
            else
                o.UseSqlite(conn ?? "Data Source=arielvertex.db");
        });

        services.Configure<JwtSettings>(config.GetSection("Jwt"));
        services.Configure<AuthSettings>(config.GetSection("Auth"));
        services.Configure<IntegrationSettings>(config.GetSection("Integration"));
        services.Configure<AzureAdSettings>(config.GetSection("AzureAd"));
        services.Configure<Integration.AiSettings>(config.GetSection("Ai"));
        services.Configure<Storage.FileStorageSettings>(config.GetSection("Storage"));

        // Local credential auth + inbound Microsoft (Entra) login (config-gated).
        services.AddScoped<IIdentityProvider, LocalIdentityProvider>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<EntraTokenValidator>();
        services.AddScoped<IMicrosoftAuthService, MicrosoftAuthService>();
        services.AddHttpClient("graph");
        services.AddSingleton<Integration.MicrosoftGraphClient>();

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IProjectAccessService, ProjectAccessService>();
        services.AddScoped<IPerformanceReportService, PerformanceReportService>();
        services.AddScoped<IPlatformConfig, PlatformConfigService>();
        services.AddScoped<IPipService, PipService>();
        services.AddScoped<IJobService, Jobs.JobService>();

        // Meeting-minutes generation: built-in offline generator + config-gated AI (Claude) drop-in.
        services.AddHttpClient("anthropic");
        services.AddScoped<IMinutesGenerator, MinutesGenerator>();

        // AI bill extraction (same Anthropic/Claude backend, vision-capable).
        services.AddScoped<IBillExtractor, BillExtractor>();

        // Operational automation scheduler (spec 6.6/6.8/6.9).
        services.Configure<Jobs.AutomationSettings>(config.GetSection("Automation"));
        services.AddHostedService<Jobs.AutomationBackgroundService>();

        // Graph boundaries — disabled locally unless explicitly enabled and configured.
        services.AddSingleton<IGraphMeetingService, GraphMeetingService>();
        services.AddScoped<IDirectorySyncService, DirectorySyncService>(); // scoped: upserts via DbContext

        // Private file storage (swap for Azure Blob / S3 in production).
        services.AddSingleton<IFileStorage, Storage.LocalFileStorage>();
        services.AddSingleton<ITeamsWebhookSender, Integration.TeamsWebhookSender>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auth = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthSettings>>().Value;
        await DataSeeder.SeedAsync(db, auth);
    }
}
