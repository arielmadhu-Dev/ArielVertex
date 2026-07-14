using Microsoft.AspNetCore.Mvc;

namespace ArielVertex.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Consistent error envelope matching the exception middleware.</summary>
    protected IActionResult Fail(int status, string code, string message)
    {
        var cid = HttpContext.Items.TryGetValue("CorrelationId", out var v) ? v?.ToString() : null;
        return StatusCode(status, new { error = new { code, message, correlationId = cid } });
    }

    protected IActionResult BadInput(string message) => Fail(400, "bad_request", message);
    protected IActionResult Denied(string message = "You do not have access to this resource.") => Fail(403, "forbidden", message);
    protected IActionResult Missing(string message = "Not found.") => Fail(404, "not_found", message);
    protected IActionResult Conflict409(string message) => Fail(409, "conflict", message);
}
