namespace ArielVertex.Infrastructure.Auth;

public class JwtSettings
{
    public string Issuer { get; set; } = "ariel-vertex";
    public string Audience { get; set; } = "ariel-vertex-client";
    public string Secret { get; set; } = "dev-only-change-me-super-secret-signing-key-0123456789";
    public int ExpiryHours { get; set; } = 8;
}

/// <summary>Auth mode + allowed domain (spec 6.1). "Local" today, "Entra" once wired.</summary>
public class AuthSettings
{
    public string Mode { get; set; } = "Local";           // Local | Entra
    public string AllowedDomain { get; set; } = "arielsoftwares.in";
    public string SeedPassword { get; set; } = "Ariel@123";
}

/// <summary>Toggles for the Microsoft Graph boundaries (spec section 10).</summary>
public class IntegrationSettings
{
    public bool GraphMeetingsLive { get; set; }
    public bool DirectorySyncLive { get; set; }
    public bool OutlookNotificationsLive { get; set; }
}

/// <summary>
/// Microsoft Entra app-registration values (spec: Graph setup guide). Populate these + set the
/// Integration flags to activate live Microsoft login, directory sync, Teams/Outlook meetings and
/// email delivery. Keep the secret in a secret store / environment variable — never in source.
/// </summary>
public class AzureAdSettings
{
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string GraphBaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";
    /// <summary>User (id or UPN) whose calendar/mailbox sends meetings &amp; notification emails.</summary>
    public string ServiceUser { get; set; } = "";

    public string Authority => $"{Instance.TrimEnd('/')}/{TenantId}";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
