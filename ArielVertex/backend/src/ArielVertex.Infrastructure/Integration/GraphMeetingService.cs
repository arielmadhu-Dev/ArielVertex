using ArielVertex.Application.Abstractions;
using ArielVertex.Infrastructure.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArielVertex.Infrastructure.Integration;

/// <summary>
/// Microsoft Graph meeting boundary (spec 6.5/6.7). Off by default -> deterministic placeholder
/// ids/urls so the full schedule flow is exercisable locally. Set Integration:GraphMeetingsLive
/// (with AzureAd configured) to create real Outlook events with Teams links.
/// </summary>
public class GraphMeetingService : IGraphMeetingService
{
    private readonly IntegrationSettings _settings;
    private readonly AzureAdSettings _azure;
    private readonly MicrosoftGraphClient _graph;
    private readonly ILogger<GraphMeetingService> _log;

    public GraphMeetingService(IOptions<IntegrationSettings> settings, IOptions<AzureAdSettings> azure,
        MicrosoftGraphClient graph, ILogger<GraphMeetingService> log)
    { _settings = settings.Value; _azure = azure.Value; _graph = graph; _log = log; }

    public bool IsLive => _settings.GraphMeetingsLive && _azure.IsConfigured;

    public async Task<(string outlookEventId, string teamsJoinUrl)> CreateMeetingAsync(
        string title, string description, DateTime scheduledAt, int durationMinutes,
        IEnumerable<string> attendeeEmails, CancellationToken ct = default)
    {
        if (IsLive)
        {
            try
            {
                return await _graph.CreateEventAsync(title, description, scheduledAt.ToUniversalTime(), durationMinutes, attendeeEmails, ct);
            }
            catch (Exception ex)
            {
                // Calendar creation is best-effort: a Graph outage or missing consent (e.g. 403) must
                // not fail the review workflow. Fall through to a placeholder link so the review record
                // and portal notifications are still created; the invite can be re-sent once Graph is fixed.
                _log.LogWarning(ex, "Graph meeting creation failed for '{Title}'; falling back to placeholder link.", title);
            }
        }

        // Deterministic placeholders, stable per title+time so re-runs don't churn.
        var slug = new string(title.ToLowerInvariant().Where(char.IsLetterOrDigit).Take(16).ToArray());
        var key = $"{slug}-{scheduledAt:yyyyMMddHHmm}";
        return ($"AV-EVT-{key}", $"https://teams.microsoft.com/l/meetup-join/av-placeholder/{key}");
    }
}