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
            // Read Graph before starting the transaction. A failed manager lookup cannot leave
            // partially-updated employee records behind.
            var graphUsers = await _graph.ListUsersAsync(ct);
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            var now = DateTime.UtcNow;
            var users = await _db.Users.ToListAsync(ct);
            var byEmail = users.ToDictionary(u => u.Email, u => u, StringComparer.OrdinalIgnoreCase);

            // First pass: upsert every user so all manager targets have local database ids.
            foreach (var g in graphUsers)
            {
                var email = g.Email.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(email)) { failed++; continue; }

                if (byEmail.TryGetValue(email, out var user))
                {
                    if (!user.ProfileManagedLocally)
                    {
                        if (!string.IsNullOrWhiteSpace(g.DisplayName)) user.Name = g.DisplayName.Trim();
                        user.Designation = g.JobTitle.Trim();
                    }
                    user.MicrosoftUserId = g.Id;
                    user.LastSyncedAt = now;
                    user.IsProvisionedFromEntra = true;
                    if (!g.AccountEnabled && user.Status == EmployeeStatus.Active)
                    {
                        user.Status = EmployeeStatus.Inactive;
                        deactivated++;
                    }
                    else updated++;
                }
                else
                {
                    user = new User
                    {
                        Name = string.IsNullOrWhiteSpace(g.DisplayName) ? email : g.DisplayName.Trim(),
                        Email = email,
                        Designation = g.JobTitle.Trim(),
                        Role = PortalRole.Employee,
                        MicrosoftUserId = g.Id,
                        IsProvisionedFromEntra = true,
                        LastSyncedAt = now,
                        Status = g.AccountEnabled ? EmployeeStatus.Active : EmployeeStatus.Inactive,
                        JoiningDate = now,
                        EmployeeCode = ""
                    };
                    _db.Users.Add(user);
                    byEmail[email] = user;
                    created++;
                }
            }
            await _db.SaveChangesAsync(ct);

            // Assign codes to freshly-created users after their database ids are available.
            foreach (var user in byEmail.Values.Where(u => string.IsNullOrWhiteSpace(u.EmployeeCode)))
                user.EmployeeCode = $"AV{user.Id:D4}";

            // Create missing department master records and map Entra's department text.
            var departments = await _db.Departments.ToListAsync(ct);
            var byDepartment = departments.ToDictionary(d => d.Name, d => d, StringComparer.OrdinalIgnoreCase);
            var usedCodes = departments.Select(d => d.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var departmentName in graphUsers.Select(g => g.Department.Trim())
                         .Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (byDepartment.ContainsKey(departmentName)) continue;
                var department = new Department { Name = departmentName, Code = NextDepartmentCode(departmentName, usedCodes) };
                _db.Departments.Add(department);
                byDepartment[departmentName] = department;
            }
            await _db.SaveChangesAsync(ct);

            var byMicrosoftId = byEmail.Values
                .Where(u => !string.IsNullOrWhiteSpace(u.MicrosoftUserId))
                .GroupBy(u => u.MicrosoftUserId!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var managerAssignments = 0;
            var departmentAssignments = 0;

            // Second pass: resolve relationships after every user and department has an id.
            foreach (var g in graphUsers)
            {
                var email = g.Email.Trim().ToLowerInvariant();
                if (!byEmail.TryGetValue(email, out var user)) continue;

                if (!user.ProfileManagedLocally)
                {
                    user.DepartmentId = !string.IsNullOrWhiteSpace(g.Department) &&
                                        byDepartment.TryGetValue(g.Department.Trim(), out var department)
                        ? department.Id
                        : null;
                    if (user.DepartmentId is not null) departmentAssignments++;
                }

                if (!user.ManagerManagedLocally)
                {
                    User? manager = null;
                    if (!string.IsNullOrWhiteSpace(g.ManagerMicrosoftUserId))
                    {
                        if (!byMicrosoftId.TryGetValue(g.ManagerMicrosoftUserId, out manager))
                            failed++;
                        else if (manager.Id == user.Id)
                            manager = null;
                    }
                    user.ManagerId = manager?.Id;
                    if (user.ManagerId is not null) managerAssignments++;
                }
            }

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var status = failed == 0 ? SyncStatus.Success : SyncStatus.Partial;
            return new DirectorySyncResult(created, updated, deactivated, failed, status,
                $"Synced {graphUsers.Count} directory users; assigned {managerAssignments} managers and mapped {departmentAssignments} departments.");
        }
        catch (Exception ex)
        {
            return new DirectorySyncResult(created, updated, deactivated, failed + 1, SyncStatus.Failed, $"Sync failed: {ex.Message}");
        }
    }

    private static string NextDepartmentCode(string name, ISet<string> usedCodes)
    {
        var root = new string(name.Where(char.IsLetterOrDigit).Take(8).ToArray()).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(root)) root = "DEPT";
        var candidate = root;
        var suffix = 2;
        while (!usedCodes.Add(candidate)) candidate = $"{root}{suffix++}";
        return candidate;
    }

}
