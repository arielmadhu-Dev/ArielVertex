using ArielVertex.Domain.Common;
using ArielVertex.Domain.Enums;

namespace ArielVertex.Domain.Entities;

/// <summary>
/// Portal notification record and delivery state (spec 6.12). Portal is the source of
/// truth; Teams/Outlook are additional delivery channels toggled per integration settings.
/// </summary>
public class Notification : BaseEntity
{
    public int RecipientId { get; set; }
    public User? Recipient { get; set; }

    public NotificationType Type { get; set; } = NotificationType.General;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Link { get; set; }
    public bool IsRead { get; set; }

    /// <summary>Channels this notification was (or would be) delivered on, comma-separated.</summary>
    public string DeliveredChannels { get; set; } = NotificationChannel.Portal.ToString();
}
