using ArielVertex.Domain.Common;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// A single admin-editable configuration value (feature flags, thresholds, toggles). Read at
/// runtime so changes take effect without a code change or redeploy.
/// </summary>
public class PlatformSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;       // e.g. "pip.thresholdPercent"
    public string Value { get; set; } = string.Empty;
    public string Group { get; set; } = "General";        // UI grouping
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text";            // bool | number | text | textarea | csv
    public string Description { get; set; } = string.Empty;
    public bool Editable { get; set; } = true;
}

/// <summary>An admin-editable email/notification template (subject + body with {placeholders}).</summary>
public class NotificationTemplate : BaseEntity
{
    public string Key { get; set; } = string.Empty;       // e.g. "pip.combined"
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Placeholders { get; set; } = string.Empty; // comma-separated {tokens} for the UI hint
}
