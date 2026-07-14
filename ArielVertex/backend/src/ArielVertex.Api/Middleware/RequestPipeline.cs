using System.Diagnostics;

namespace ArielVertex.Api.Middleware;

/// <summary>Consistent error envelope + correlation ids (spec section 9 audit/monitoring).</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _log;
    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log)
    { _next = next; _log = log; }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (NotImplementedException ex)
        {
            await Write(ctx, StatusCodes.Status501NotImplemented, "not_implemented", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            await Write(ctx, StatusCodes.Status403Forbidden, "forbidden", ex.Message);
        }
        catch (Exception ex)
        {
            var cid = ctx.Items.TryGetValue("CorrelationId", out var v) ? v?.ToString() : null;
            _log.LogError(ex, "Unhandled exception. CorrelationId={Cid}", cid);
            await Write(ctx, StatusCodes.Status500InternalServerError, "server_error",
                "An unexpected error occurred. Please try again.");
        }
    }

    private static async Task Write(HttpContext ctx, int status, string code, string message)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        var cid = ctx.Items.TryGetValue("CorrelationId", out var v) ? v?.ToString() : null;
        await ctx.Response.WriteAsJsonAsync(new { error = new { code, message, correlationId = cid } });
    }
}

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        var cid = ctx.Request.Headers.TryGetValue("X-Correlation-Id", out var h) && !string.IsNullOrWhiteSpace(h)
            ? h.ToString()
            : Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        ctx.Items["CorrelationId"] = cid;
        ctx.Response.Headers["X-Correlation-Id"] = cid;
        await _next(ctx);
    }
}
