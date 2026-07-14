using ArielVertex.Api.Common;
using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Contracts;
using ArielVertex.Application.Security;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Api.Controllers;

[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ApiControllerBase
{
    private readonly IIdentityProvider _identity;
    private readonly IJwtTokenService _tokens;
    private readonly IAuditService _audit;
    private readonly AppDbContext _db;
    private readonly ICurrentUser _me;
    private readonly IMicrosoftAuthService _msauth;

    public AuthController(IIdentityProvider identity, IJwtTokenService tokens, IAuditService audit, AppDbContext db, ICurrentUser me, IMicrosoftAuthService msauth)
    { _identity = identity; _tokens = tokens; _audit = audit; _db = db; _me = me; _msauth = msauth; }

    /// <summary>
    /// Local credential login. When Entra is enabled this endpoint is replaced by the OIDC
    /// redirect flow; the SPA and downstream authorization are otherwise unchanged (spec 6.1).
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _identity.ValidateCredentialsAsync(req.Email, req.Password);
        if (user is null)
        {
            await _audit.WriteAsync(AuditAction.LoginFailed, "Auth", null, $"Failed login for {req.Email}");
            return Fail(401, "invalid_credentials", "Invalid email or password.");
        }

        var (token, expiresAt) = _tokens.Create(user);
        await _audit.WriteAsync(AuditAction.Login, "Auth", user.Id, $"{user.Name} signed in ({_identity.Mode}).");

        var dto = user.ToCurrentUserDto(Permissions.For(user.Role).ToArray(), RoleGroups.DashboardFor(user.Role));
        return Ok(new AuthResponse(token, expiresAt, dto));
    }

    /// <summary>
    /// Microsoft (Entra) login. The SPA obtains an Entra token via MSAL and posts it here; we
    /// validate it against the tenant's keys, map/provision the user, and issue the app's own JWT —
    /// so RBAC and every downstream endpoint are unchanged (spec 6.1).
    /// </summary>
    [HttpPost("microsoft")]
    [AllowAnonymous]
    public async Task<IActionResult> Microsoft([FromBody] MicrosoftLoginRequest req)
    {
        if (!_msauth.IsEnabled) return Fail(400, "not_configured", "Microsoft login is not enabled on this server.");
        var user = await _msauth.ValidateAndProvisionAsync(req.Token);
        if (user is null)
        {
            await _audit.WriteAsync(AuditAction.LoginFailed, "Auth", null, "Failed Microsoft login (invalid token or domain).");
            return Fail(401, "invalid_token", "Microsoft sign-in could not be verified.");
        }
        var (token, expiresAt) = _tokens.Create(user);
        await _audit.WriteAsync(AuditAction.Login, "Auth", user.Id, $"{user.Name} signed in (Microsoft).");
        var dto = user.ToCurrentUserDto(Permissions.For(user.Role).ToArray(), RoleGroups.DashboardFor(user.Role));
        return Ok(new AuthResponse(token, expiresAt, dto));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await _db.Users.Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == _me.Id);
        if (user is null) return Missing("User not found.");
        var dto = user.ToCurrentUserDto(Permissions.For(user.Role).ToArray(), RoleGroups.DashboardFor(user.Role));
        return Ok(dto);
    }
}
