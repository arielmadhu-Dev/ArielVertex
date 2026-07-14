using System.Net.Http;
using System.Net.Http.Json;
using ArielVertex.Application.Abstractions;

namespace ArielVertex.Infrastructure.Integration;

/// <summary>
/// Posts an Adaptive Card to a Microsoft Teams channel Incoming Webhook. This is the app-only path
/// to "land in Teams" without a bot — the admin pastes the webhook URL in Expense Settings.
/// </summary>
public class TeamsWebhookSender : ITeamsWebhookSender
{
    private readonly IHttpClientFactory _http;
    public TeamsWebhookSender(IHttpClientFactory http) => _http = http;

    public async Task SendAsync(string webhookUrl, string title, string markdownBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl)) return;
        var card = new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = new object[]
                        {
                            new { type = "TextBlock", size = "Large", weight = "Bolder", text = title },
                            new { type = "TextBlock", wrap = true, text = markdownBody }
                        }
                    }
                }
            }
        };
        var client = _http.CreateClient("graph");
        using var resp = await client.PostAsJsonAsync(webhookUrl, card, ct);
        resp.EnsureSuccessStatusCode();
    }
}
