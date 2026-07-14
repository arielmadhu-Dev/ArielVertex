using ArielVertex.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ArielVertex.Api.Security;

/// <summary>
/// Blocks a controller/action when its module feature flag is turned off in the admin configuration
/// — so disabling a module from the portal actually disables its API, not just its nav.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireFeatureAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _feature;
    public RequireFeatureAttribute(string feature) => _feature = feature;

    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var cfg = ctx.HttpContext.RequestServices.GetRequiredService<IPlatformConfig>();
        if (!await cfg.IsFeatureEnabledAsync(_feature))
        {
            ctx.Result = new ObjectResult(new { error = new { code = "feature_disabled", message = "This module is turned off." } }) { StatusCode = 404 };
            return;
        }
        await next();
    }
}
