using ArielVertex.Application.Abstractions;
using ArielVertex.Infrastructure.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArielVertex.Infrastructure.Integration;

/// <summary>
/// Microsoft Graph meeting boundary (spec 6.5/6.7). When enabled, creates or updates a real
/// Outlook calendar event and returns its Teams join URL. When disabled, returns empty identifiers
/// so callers never expose a fake or unusable meeting link.
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
        IEnumerable<string> attendeeEmails, string? existingEventId = null, CancellationToken ct = default)
    {
        if (!IsLive) return ("", "");

        try
        {
            return string.IsNullOrWhiteSpace(existingEventId)
                ? await _graph.CreateEventAsync(title, description, scheduledAt.ToUniversalTime(), durationMinutes, attendeeEmails, ct)
                : await _graph.UpdateEventAsync(existingEventId, title, description, scheduledAt.ToUniversalTime(), durationMinutes, attendeeEmails, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Graph meeting creation/update failed for '{Title}'.", title);
            throw new MeetingIntegrationException(
                "The Teams calendar invite could not be created. Grant the Entra app the Calendars.ReadWrite application permission with admin consent, and verify the configured service user has an Exchange calendar.", ex);
        }
    }
}
