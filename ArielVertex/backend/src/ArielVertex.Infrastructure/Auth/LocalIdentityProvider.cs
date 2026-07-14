using ArielVertex.Application.Abstractions;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Infrastructure.Auth;

/// <summary>
/// Local credential validation for instant-run/dev. When the Entra app registration is ready,
/// register an EntraIdentityProvider implementing the same <see cref="IIdentityProvider"/> —
/// controllers and services stay untouched (spec 6.1: pluggable auth boundary).
/// </summary>
public class LocalIdentityProvider : IIdentityProvider
{
    private readonly AppDbContext _db;
    public LocalIdentityProvider(AppDbContext db) => _db = db;

    public string Mode => "Local";

    public async Task<User?> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Email == normalized, ct);

        if (user is null || user.Status == EmployeeStatus.Inactive) return null;
        if (string.IsNullOrEmpty(user.PasswordHash)) return null;
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return null;
        return user;
    }
}
