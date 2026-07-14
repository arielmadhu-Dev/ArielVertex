using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ArielVertex.Application.Abstractions;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace ArielVertex.Infrastructure.Auth;

/// <summary>Validates Entra tokens against the tenant's published signing keys. Singleton — caches OIDC metadata.</summary>
public class EntraTokenValidator
{
    private readonly AzureAdSettings _cfg;
    private readonly AuthSettings _auth;
    private readonly ConfigurationManager<OpenIdConnectConfiguration>? _oidc;

    public EntraTokenValidator(IOptions<AzureAdSettings> cfg, IOptions<AuthSettings> auth)
    {
        _cfg = cfg.Value; _auth = auth.Value;
        if (_cfg.IsConfigured)
            _oidc = new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{_cfg.Authority}/v2.0/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever());
    }

    public async Task<ClaimsPrincipal?> ValidateAsync(string token, CancellationToken ct)
    {
        if (_oidc is null) return null;
        var config = await _oidc.GetConfigurationAsync(ct);
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[] { $"{_cfg.Authority}/v2.0", $"https://sts.windows.net/{_cfg.TenantId}/" },
            ValidateAudience = true,
            ValidAudiences = new[] { _cfg.ClientId, $"api://{_cfg.ClientId}" },
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
            return principal;
        }
        catch { return null; }
    }

    public string AllowedDomain => _auth.AllowedDomain;
}

public class MicrosoftAuthService : IMicrosoftAuthService
{
    private readonly EntraTokenValidator _validator;
    private readonly AppDbContext _db;
    private readonly AzureAdSettings _cfg;
    public MicrosoftAuthService(EntraTokenValidator validator, AppDbContext db, IOptions<AzureAdSettings> cfg)
    { _validator = validator; _db = db; _cfg = cfg.Value; }

    public bool IsEnabled => _cfg.IsConfigured;

    public async Task<User?> ValidateAndProvisionAsync(string token, CancellationToken ct = default)
    {
        var principal = await _validator.ValidateAsync(token, ct);
        if (principal is null) return null;

        var email = (principal.FindFirst("preferred_username")?.Value
                     ?? principal.FindFirst(ClaimTypes.Email)?.Value
                     ?? principal.FindFirst("upn")?.Value ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email)) return null;

        // Enforce the allowed organizational domain (spec 6.1 / §9).
        var domain = _validator.AllowedDomain;
        if (!string.IsNullOrWhiteSpace(domain) && !email.EndsWith("@" + domain, StringComparison.OrdinalIgnoreCase)) return null;

        var oid = principal.FindFirst("oid")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var name = principal.FindFirst("name")?.Value ?? email;

        var user = await _db.Users.Include(u => u.Department).FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            // Auto-provision from an approved-domain identity (spec 6.1). Role starts as Employee; HR elevates.
            user = new User { Name = name, Email = email, Role = PortalRole.Employee, MicrosoftUserId = oid, IsProvisionedFromEntra = true, JoiningDate = DateTime.UtcNow, Status = EmployeeStatus.Active, EmployeeCode = "" };
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
            user.EmployeeCode = $"AV{user.Id:D4}";
        }
        else { user.MicrosoftUserId = oid; user.IsProvisionedFromEntra = true; }
        user.LastSyncedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        if (user.Status == EmployeeStatus.Inactive) return null;
        return user;
    }
}
