using ArielVertex.Application.Abstractions;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArielVertex.Infrastructure.Services;

/// <summary>Reads the admin-editable PlatformSetting store and renders NotificationTemplates.</summary>
public class PlatformConfigService : IPlatformConfig
{
    private readonly AppDbContext _db;
    public PlatformConfigService(AppDbContext db) => _db = db;

    private Task<string?> RawAsync(string key, CancellationToken ct) =>
        _db.PlatformSettings.Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(ct);

    public async Task<bool> GetBoolAsync(string key, bool fallback = false, CancellationToken ct = default)
    {
        var v = await RawAsync(key, ct);
        return v is null ? fallback : v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1";
    }

    public async Task<int> GetIntAsync(string key, int fallback = 0, CancellationToken ct = default)
    {
        var v = await RawAsync(key, ct);
        return int.TryParse(v, out var n) ? n : fallback;
    }

    public async Task<string> GetStringAsync(string key, string fallback = "", CancellationToken ct = default)
        => await RawAsync(key, ct) ?? fallback;

    public Task<bool> IsFeatureEnabledAsync(string feature, CancellationToken ct = default)
        => GetBoolAsync($"feature.{feature}", true, ct);

    public async Task<IReadOnlyDictionary<string, bool>> GetFeaturesAsync(CancellationToken ct = default)
    {
        var rows = await _db.PlatformSettings.Where(s => s.Key.StartsWith("feature."))
            .AsNoTracking().ToListAsync(ct);
        return rows.ToDictionary(
            r => r.Key.Substring("feature.".Length),
            r => r.Value.Equals("true", StringComparison.OrdinalIgnoreCase) || r.Value == "1");
    }

    public async Task<(string subject, string body)> RenderAsync(string templateKey, IReadOnlyDictionary<string, string> tokens, CancellationToken ct = default)
    {
        var t = await _db.NotificationTemplates.FirstOrDefaultAsync(x => x.Key == templateKey, ct);
        if (t is null) return (templateKey, "");
        string subject = t.Subject, body = t.Body;
        foreach (var kv in tokens)
        {
            subject = subject.Replace("{" + kv.Key + "}", kv.Value);
            body = body.Replace("{" + kv.Key + "}", kv.Value);
        }
        return (subject, body);
    }
}
