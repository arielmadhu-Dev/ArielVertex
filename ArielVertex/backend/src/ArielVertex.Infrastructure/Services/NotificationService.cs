using ArielVertex.Application.Abstractions;
using ArielVertex.Domain.Entities;
using ArielVertex.Domain.Enums;
using ArielVertex.Infrastructure.Auth;
using ArielVertex.Infrastructure.Integration;
using ArielVertex.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArielVertex.Infrastructure.Services;

/// <summary>
/// Portal is the source of truth for notifications (spec 6.12). When Outlook delivery is enabled
/// (AzureAd configured + Integration:OutlookNotificationsLive), high-signal notifications are also
/// emailed via Graph. Delivery is best-effort — an email failure never blocks the portal record.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly IntegrationSettings _integration;
    private readonly AzureAdSettings _azure;
    private readonly MicrosoftGraphClient _graph;
    private readonly ILogger<NotificationService> _log;

    public NotificationService(AppDbContext db, IOptions<IntegrationSettings> integration,
        IOptions<AzureAdSettings> azure, MicrosoftGraphClient graph, ILogger<NotificationService> log)
    { _db = db; _integration = integration.Value; _azure = azure.Value; _graph = graph; _log = log; }

    private bool EmailEnabled => _integration.OutlookNotificationsLive && _azure.IsConfigured;

    public Task NotifyAsync(int recipientId, NotificationType type, string title, string message, string? link = null, CancellationToken ct = default)
        => NotifyManyAsync(new[] { recipientId }, type, title, message, link, ct);

    public async Task NotifyManyAsync(IEnumerable<int> recipientIds, NotificationType type, string title, string message, string? link = null, CancellationToken ct = default)
    {
        var ids = recipientIds.Distinct().ToList();
        var channels = EmailEnabled ? $"{NotificationChannel.Portal},{NotificationChannel.Outlook}" : NotificationChannel.Portal.ToString();
        foreach (var id in ids)
            _db.Notifications.Add(new Notification { RecipientId = id, Type = type, Title = title, Message = message, Link = link, DeliveredChannels = channels });
        await _db.SaveChangesAsync(ct);

        if (EmailEnabled)
        {
            var emails = await _db.Users.Where(u => ids.Contains(u.Id)).Select(u => u.Email).ToListAsync(ct);
            var html = $"<p><strong>{System.Net.WebUtility.HtmlEncode(title)}</strong></p><p>{System.Net.WebUtility.HtmlEncode(message)}</p><p style='color:#64748b'>Sent by Ariel Vertex.</p>";
            foreach (var email in emails)
            {
                try { await _graph.SendMailAsync(email, title, html, ct); }
                catch (Exception ex) { _log.LogWarning(ex, "Outlook delivery failed for {Email}", email); }
            }
        }
    }
}
