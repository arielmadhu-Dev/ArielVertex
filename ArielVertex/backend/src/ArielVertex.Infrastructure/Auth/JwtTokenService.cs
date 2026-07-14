using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Security;
using ArielVertex.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ArielVertex.Infrastructure.Auth;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _s;
    public JwtTokenService(IOptions<JwtSettings> s) => _s = s.Value;

    public (string token, DateTime expiresAt) Create(User user)
    {
        var expires = DateTime.UtcNow.AddHours(_s.ExpiryHours);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.Name),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("role_value", ((int)user.Role).ToString())
        };
        // Embed capabilities so the API can authorize without a per-request DB hit.
        foreach (var p in Permissions.For(user.Role))
            claims.Add(new Claim("perm", p));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_s.Secret));
        var jwt = new JwtSecurityToken(
            issuer: _s.Issuer, audience: _s.Audience, claims: claims,
            expires: expires, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}
