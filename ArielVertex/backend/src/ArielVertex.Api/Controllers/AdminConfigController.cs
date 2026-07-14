using ArielVertex.Api.Security;
using ArielVertex.Application.Abstractions;
using ArielVertex.Application.Contracts;
using ArielVertex.Application.Security;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Api.Controllers;

[Authorize]
[Route("api/v1")]
public class AdminConfigController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly IPlatformConfig _config;
    public AdminConfigController(AppDbContext db, IAuditService audit, IPlatformConfig config) { _db = db; _audit = audit; _config = config; }

    /// <summary>Feature flags for the current user's SPA (which modules are enabled). Any signed-in user.</summary>
    [HttpGet("config/features")]
    public async Task<IActionResult> Features() => Ok(await _config.GetFeaturesAsync());

    // ---------- Admin: settings ----------
    [HttpGet("admin/config")]
    [Capability(Permissions.ConfigManage)]
    public async Task<IActionResult> GetConfig()
    {
        var settings = await _db.PlatformSettings.AsNoTracking().OrderBy(s => s.Group).ThenBy(s => s.Label).ToListAsync();
        return Ok(settings.Select(s => new PlatformSettingDto(s.Key, s.Value, s.Group, s.Label, s.Type, s.Description, s.Editable)));
    }

    [HttpPut("admin/config")]
    [Capability(Permissions.ConfigManage)]
    public async Task<IActionResult> UpdateConfig([FromBody] UpdateSettingsRequest req)
    {
        var byKey = await _db.PlatformSettings.ToDictionaryAsync(s => s.Key);
        foreach (var item in req.Settings)
            if (byKey.TryGetValue(item.Key, out var s) && s.Editable) s.Value = item.Value?.Trim() ?? "";
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.ConfigChanged, "PlatformSetting", null, $"{req.Settings.Count} setting(s) updated.");
        return Ok(new { ok = true });
    }

    // ---------- Admin: notification templates ----------
    [HttpGet("admin/templates")]
    [Capability(Permissions.ConfigManage)]
    public async Task<IActionResult> GetTemplates()
    {
        var t = await _db.NotificationTemplates.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        return Ok(t.Select(x => new TemplateDto(x.Key, x.Name, x.Subject, x.Body, x.Placeholders)));
    }

    [HttpPut("admin/templates/{key}")]
    [Capability(Permissions.ConfigManage)]
    public async Task<IActionResult> UpdateTemplate(string key, [FromBody] UpdateTemplateRequest req)
    {
        var t = await _db.NotificationTemplates.FirstOrDefaultAsync(x => x.Key == key);
        if (t is null) return Missing("Template not found.");
        t.Subject = req.Subject.Trim(); t.Body = req.Body.Trim();
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(AuditAction.TemplateChanged, "NotificationTemplate", t.Id, $"Template '{key}' updated.");
        return Ok(new { ok = true });
    }
}
