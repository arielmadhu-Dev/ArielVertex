using ArielVertex.Application.Abstractions;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Auth;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ArielVertex.Infrastructure.Integration;

/// <summary>
/// Employee sync boundary (spec 6.2). Off by default → no-op success. When live, enumerates Entra
/// users via Graph and upserts them (keyed on email): new users are provisioned (no local
/// password), existing users are updated, and users disabled in Entra are marked Inactive.
/// </summary>
public class DirectorySyncService : IDirectorySyncService
{
    private readonly IntegrationSettings _settings;
    private readonly AzureAdSettings _azure;
    private readonly AppDbContext _db;
    private readonly MicrosoftGraphClient _graph;

    public DirectorySyncService(IOptions<IntegrationSettings> settings, IOptions<AzureAdSettings> azure, AppDbContext db, MicrosoftGraphClient graph)
    { _settings = settings.Value; _azure = azure.Value; _db = db; _graph = graph; }

    public bool IsLive => _settings.DirectorySyncLive && _azure.IsConfigured;

    public async Task<DirectorySyncResult> RunAsync(bool manual, string triggeredBy, CancellationToken ct = default)
    {
        if (!IsLive)
            return new DirectorySyncResult(0, 0, 0, 0, SyncStatus.Success,
                "Directory sync is not enabled. Configure AzureAd and set Integration:DirectorySyncLive=true.");

        int created = 0, updated = 0, deactivated = 0, failed = 0;
        try
        {
            var graphUsers = await _graph.ListUsersAsync(ct);
            var byEmail = await _db.Users.ToDictionaryAsync(u => u.Email, u => u, StringComparer.OrdinalIgnoreCase, ct);

            foreach (var g in graphUsers)
            {
                var email = g.Email.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(email)) { failed++; continue; }

                if (byEmail.TryGetValue(email, out var user))
                {
                    user.Name = g.DisplayName; user.Designation = g.JobTitle;
                    user.MicrosoftUserId = g.Id; user.LastSyncedAt = DateTime.UtcNow; user.IsProvisionedFromEntra = true;
                    if (!g.AccountEnabled && user.Status == EmployeeStatus.Active) { user.Status = EmployeeStatus.Inactive; deactivated++; }
                    else updated++;
                }
                else
                {
                    _db.Users.Add(new User
                    {
                        Name = g.DisplayName, Email = email, Designation = g.JobTitle, Role = PortalRole.Employee,
                        MicrosoftUserId = g.Id, IsProvisionedFromEntra = true, LastSyncedAt = DateTime.UtcNow,
                        Status = g.AccountEnabled ? EmployeeStatus.Active : EmployeeStatus.Inactive,
                        JoiningDate = DateTime.UtcNow, EmployeeCode = ""
                    });
                    created++;
                }
            }
            await _db.SaveChangesAsync(ct);
            // Assign codes to any freshly-created users that lack one.
            foreach (var u in await _db.Users.Where(u => u.EmployeeCode == "").ToListAsync(ct)) u.EmployeeCode = $"AV{u.Id:D4}";
            await _db.SaveChangesAsync(ct);

            var status = failed == 0 ? SyncStatus.Success : SyncStatus.Partial;
            return new DirectorySyncResult(created, updated, deactivated, failed, status,
                $"Synced {graphUsers.Count} directory users.");
        }
        catch (Exception ex)
        {
            return new DirectorySyncResult(created, updated, deactivated, failed + 1, SyncStatus.Failed, $"Sync failed: {ex.Message}");
        }
    }
}
