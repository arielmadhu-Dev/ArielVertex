using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArielVertex.Infrastructure.Auth;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Options;

namespace ArielVertex.Infrastructure.Integration;

public record GraphUser(string Id, string DisplayName, string Email, string JobTitle, string Department, bool AccountEnabled);

/// <summary>
/// Thin Microsoft Graph client using client-credentials (app-only) auth via MSAL, then REST calls
/// with HttpClient. Chosen over the full Graph SDK to keep the dependency surface small and the
/// request shapes explicit. Only instantiated/used when Azure AD is configured and a feature flag
/// is on — see GraphMeetingService / DirectorySyncService / NotificationService.
/// </summary>
public class MicrosoftGraphClient
{
    private readonly AzureAdSettings _cfg;
    private readonly IConfidentialClientApplication? _app;
    private readonly IHttpClientFactory _httpFactory;
    private static readonly JsonSerializerOptions J = new() { PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public MicrosoftGraphClient(IOptions<AzureAdSettings> cfg, IHttpClientFactory httpFactory)
    {
        _cfg = cfg.Value;
        _httpFactory = httpFactory;
        // Only build the MSAL app when Azure AD is configured (local/dev leaves this null).
        if (_cfg.IsConfigured)
            _app = ConfidentialClientApplicationBuilder.Create(_cfg.ClientId)
                .WithClientSecret(_cfg.ClientSecret)
                .WithAuthority(_cfg.Authority)
                .Build();
    }

    private async Task<HttpClient> AuthedAsync(CancellationToken ct)
    {
        if (_app is null) throw new InvalidOperationException("Azure AD is not configured (set AzureAd:TenantId/ClientId/ClientSecret).");
        var http = _httpFactory.CreateClient("graph");
        var result = await _app.AcquireTokenForClient(new[] { "https://graph.microsoft.com/.default" }).ExecuteAsync(ct);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
        return http;
    }

    // ---- Directory sync (spec 6.2) ----
    public async Task<IReadOnlyList<GraphUser>> ListUsersAsync(CancellationToken ct = default)
    {
        var http = await AuthedAsync(ct);
        var url = $"{_cfg.GraphBaseUrl}/users?$select=id,displayName,mail,userPrincipalName,jobTitle,department,accountEnabled&$top=999";
        var users = new List<GraphUser>();
        while (url is not null)
        {
            using var resp = await http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            var page = await resp.Content.ReadFromJsonAsync<GraphList<GraphUserRaw>>(J, ct);
            foreach (var u in page?.Value ?? new())
                users.Add(new GraphUser(u.Id ?? "", u.DisplayName ?? "", u.Mail ?? u.UserPrincipalName ?? "",
                    u.JobTitle ?? "", u.Department ?? "", u.AccountEnabled ?? true));
            url = page?.NextLink;
        }
        return users;
    }

    // ---- Online meeting via calendar event with Teams (spec 6.5/6.7) ----
    public async Task<(string eventId, string joinUrl)> CreateEventAsync(string subject, string bodyHtml,
        DateTime startUtc, int durationMinutes, IEnumerable<string> attendeeEmails, CancellationToken ct = default)
    {
        var http = await AuthedAsync(ct);
        var organizer = _cfg.ServiceUser;
        var payload = new
        {
            subject,
            body = new { contentType = "HTML", content = bodyHtml },
            start = new { dateTime = startUtc.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" },
            end = new { dateTime = startUtc.AddMinutes(durationMinutes).ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" },
            isOnlineMeeting = true,
            onlineMeetingProvider = "teamsForBusiness",
            attendees = attendeeEmails.Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => new { emailAddress = new { address = e }, type = "required" }).ToArray()
        };
        using var resp = await http.PostAsJsonAsync($"{_cfg.GraphBaseUrl}/users/{organizer}/events", payload, J, ct);
        resp.EnsureSuccessStatusCode();
        var ev = await resp.Content.ReadFromJsonAsync<GraphEvent>(J, ct);
        return (ev?.Id ?? "", ev?.OnlineMeeting?.JoinUrl ?? "");
    }

    // ---- Outlook email delivery (spec 6.12) ----
    public async Task SendMailAsync(string toEmail, string subject, string html, CancellationToken ct = default)
    {
        var http = await AuthedAsync(ct);
        var payload = new
        {
            message = new
            {
                subject,
                body = new { contentType = "HTML", content = html },
                toRecipients = new[] { new { emailAddress = new { address = toEmail } } }
            },
            saveToSentItems = false
        };
        using var resp = await http.PostAsJsonAsync($"{_cfg.GraphBaseUrl}/users/{_cfg.ServiceUser}/sendMail", payload, J, ct);
        resp.EnsureSuccessStatusCode();
    }

    // ---- Graph JSON shapes ----
    private class GraphList<T> { [JsonPropertyName("value")] public List<T> Value { get; set; } = new(); [JsonPropertyName("@odata.nextLink")] public string? NextLink { get; set; } }
    private class GraphUserRaw { public string? Id { get; set; } public string? DisplayName { get; set; } public string? Mail { get; set; } public string? UserPrincipalName { get; set; } public string? JobTitle { get; set; } public string? Department { get; set; } public bool? AccountEnabled { get; set; } }
    private class GraphEvent { public string? Id { get; set; } public GraphOnlineMeeting? OnlineMeeting { get; set; } }
    private class GraphOnlineMeeting { public string? JoinUrl { get; set; } }
}
