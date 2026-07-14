using ArielVertex.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace ArielVertex.Api.Security;

/// <summary>
/// Registers one authorization policy per capability (spec section 9: policy-based authz on
/// every API). A policy simply requires the matching "perm" claim, which the token carries.
/// </summary>
public static class PolicyExtensions
{
    public static IServiceCollection AddCapabilityPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            foreach (var perm in Permissions.All)
                options.AddPolicy(perm, p => p.RequireClaim("perm", perm));
        });
        return services;
    }
}

/// <summary>Attribute sugar: [Capability(Permissions.ProjectsManage)].</summary>
public class CapabilityAttribute : Microsoft.AspNetCore.Authorization.AuthorizeAttribute
{
    public CapabilityAttribute(string permission) => Policy = permission;
}
