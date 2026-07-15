using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArielVertex.Infrastructure.Auth;
using Microsoft.Identity.Client;
using Microsoft.Extensions.Options;

namespace ArielVertex.Infrastructure.Integration;

public record GraphUser(
    string Id, string DisplayName, string Email, string JobTitle, string Department,
    bool AccountEnabled, string? ManagerMicrosoftUserId);

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
        var rawUsers = new List<GraphUserRaw>();
        while (url is not null)
        {
            using var resp = await http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            var page = await resp.Content.ReadFromJsonAsync<GraphList<GraphUserRaw>>(J, ct);
            rawUsers.AddRange(page?.Value ?? new());
            url = page?.NextLink;
        }

        // The /users collection does not contain the manager relationship. Resolve it explicitly
        // using Graph batches (maximum 20 requests per batch) so a large directory does not cause
        // one network round trip per employee.
        var managers = await ListManagerIdsAsync(http, rawUsers, ct);
        return rawUsers.Select(u => new GraphUser(
            u.Id ?? "", u.DisplayName ?? "", u.Mail ?? u.UserPrincipalName ?? "",
            u.JobTitle ?? "", u.Department ?? "", u.AccountEnabled ?? true,
            u.Id is not null && managers.TryGetValue(u.Id, out var managerId) ? managerId : null)).ToList();
    }

    private async Task<IReadOnlyDictionary<string, string?>> ListManagerIdsAsync(
        HttpClient http, IReadOnlyCollection<GraphUserRaw> users, CancellationToken ct)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var identities = users.Where(u => !string.IsNullOrWhiteSpace(u.Id)).ToArray();

        foreach (var chunk in identities.Chunk(20))
        {
            var requestMap = chunk.Select((u, index) => new { RequestId = index.ToString(), UserId = u.Id! })
                .ToDictionary(x => x.RequestId, x => x.UserId);
            var payload = new
            {
                requests = requestMap.Select(x => new
                {
                    id = x.Key,
                    method = "GET",
                    url = $"/users/{Uri.EscapeDataString(x.Value)}/manager?$select=id"
                }).ToArray()
            };

            using var resp = await http.PostAsJsonAsync($"{_cfg.GraphBaseUrl}/$batch", payload, J, ct);
            resp.EnsureSuccessStatusCode();
            var batch = await resp.Content.ReadFromJsonAsync<GraphBatchResponse>(J, ct)
                ?? throw new InvalidOperationException("Microsoft Graph returned an empty manager batch response.");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in batch.Responses)
            {
                if (item.Id is null || !requestMap.TryGetValue(item.Id, out var userId)) continue;
                seen.Add(item.Id);

                if (item.Status == 404)
                {
                    result[userId] = null;
                    continue;
                }

                if (item.Status != 200)
                    throw new InvalidOperationException(
                        $"Microsoft Graph manager lookup failed with status {item.Status}. Ensure the app's User.Read.All application permission has admin consent.");

                result[userId] = item.Body?.Id;
            }

            if (seen.Count != requestMap.Count)
                throw new InvalidOperationException("Microsoft Graph omitted one or more manager lookup responses.");
        }

        return result;
    }

    // ---- Online meeting via calendar event with Teams (spec 6.5/6.7) ----
    public async Task<(string eventId, string joinUrl)> CreateEventAsync(string subject, string bodyHtml,
        DateTime startUtc, int durationMinutes, IEnumerable<string> attendeeEmails, CancellationToken ct = default)
    {
        var http = await AuthedAsync(ct);
        var organizer = Uri.EscapeDataString(_cfg.ServiceUser);
        var payload = BuildCalendarEvent(subject, bodyHtml, startUtc, durationMinutes, attendeeEmails);

        using var resp = await http.PostAsJsonAsync(
            $"{_cfg.GraphBaseUrl}/users/{organizer}/calendar/events", payload, J, ct);
        resp.EnsureSuccessStatusCode();
        var ev = await resp.Content.ReadFromJsonAsync<GraphEvent>(J, ct);
        return RequireWorkingMeeting(ev);
    }

    public async Task<(string eventId, string joinUrl)> UpdateEventAsync(string eventId, string subject,
        string bodyHtml, DateTime startUtc, int durationMinutes, IEnumerable<string> attendeeEmails,
        CancellationToken ct = default)
    {
        var http = await AuthedAsync(ct);
        var organizer = Uri.EscapeDataString(_cfg.ServiceUser);
        var encodedEventId = Uri.EscapeDataString(eventId);
        var payload = BuildCalendarEvent(subject, bodyHtml, startUtc, durationMinutes, attendeeEmails);

        using var resp = await http.PatchAsJsonAsync(
            $"{_cfg.GraphBaseUrl}/users/{organizer}/events/{encodedEventId}", payload, J, ct);
        resp.EnsureSuccessStatusCode();
        var ev = await resp.Content.ReadFromJsonAsync<GraphEvent>(J, ct);

        // Some Exchange responses omit the expanded onlineMeeting object after PATCH; read it once.
        if (string.IsNullOrWhiteSpace(ev?.OnlineMeeting?.JoinUrl))
        {
            using var get = await http.GetAsync(
                $"{_cfg.GraphBaseUrl}/users/{organizer}/events/{encodedEventId}?$select=id,onlineMeeting", ct);
            get.EnsureSuccessStatusCode();
            ev = await get.Content.ReadFromJsonAsync<GraphEvent>(J, ct);
        }
        return RequireWorkingMeeting(ev);
    }

    private static object BuildCalendarEvent(string subject, string bodyHtml, DateTime startUtc,
        int durationMinutes, IEnumerable<string> attendeeEmails)
    {
        var attendees = attendeeEmails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(e => new { emailAddress = new { address = e }, type = "required" })
            .ToArray();

        return new
        {
            subject,
            body = new { contentType = "HTML", content = bodyHtml },
            start = new { dateTime = startUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" },
            end = new { dateTime = startUtc.ToUniversalTime().AddMinutes(durationMinutes).ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" },
            attendees,
            responseRequested = true,
            allowNewTimeProposals = true,
            isOnlineMeeting = true,
            onlineMeetingProvider = "teamsForBusiness"
        };
    }

    private static (string eventId, string joinUrl) RequireWorkingMeeting(GraphEvent? ev)
    {
        var eventId = ev?.Id;
        var joinUrl = ev?.OnlineMeeting?.JoinUrl;
        if (string.IsNullOrWhiteSpace(eventId))
            throw new InvalidOperationException("Microsoft Graph created no calendar event id.");
        if (!Uri.TryCreate(joinUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("teams.microsoft.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Microsoft Graph did not return a valid Teams join URL.");
        return (eventId, joinUrl!);
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
    private class GraphBatchResponse { public List<GraphBatchItem> Responses { get; set; } = new(); }
    private class GraphBatchItem { public string? Id { get; set; } public int Status { get; set; } public GraphManagerRaw? Body { get; set; } }
    private class GraphManagerRaw { public string? Id { get; set; } }
    private class GraphEvent { public string? Id { get; set; } public GraphOnlineMeeting? OnlineMeeting { get; set; } }
    private class GraphOnlineMeeting { public string? JoinUrl { get; set; } }
}
