using System.Security.Claims;
using ArielVertex.Application.Abstractions;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Api.Security;

/// <summary>Projects the validated JWT + request context into the app's <see cref="ICurrentUser"/>.</summary>
public class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal _principal;
    private readonly HttpContext? _http;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _http = accessor.HttpContext;
        _principal = _http?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
    }

    public bool IsAuthenticated => _principal.Identity?.IsAuthenticated ?? false;

    public int Id => int.TryParse(_principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _principal.FindFirstValue("sub"), out var id) ? id : 0;

    public string Email => _principal.FindFirstValue(ClaimTypes.Email)
        ?? _principal.FindFirstValue("email") ?? string.Empty;

    public string Name => _principal.FindFirstValue("name") ?? string.Empty;

    public PortalRole Role =>
        int.TryParse(_principal.FindFirstValue("role_value"), out var r) ? (PortalRole)r : PortalRole.Employee;

    public IReadOnlyCollection<string> Permissions =>
        _principal.FindAll("perm").Select(c => c.Value).ToArray();

    public bool Has(string permission) => _principal.HasClaim("perm", permission);

    public string? IpAddress => _http?.Connection.RemoteIpAddress?.ToString();

    public string? CorrelationId => _http?.Items.TryGetValue("CorrelationId", out var v) == true ? v?.ToString() : null;
}
