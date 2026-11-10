using Nerves.Notification.Domain;

namespace Nerves.Notification.Delivery;

/// <summary>
/// Transport abstraction for one notification, independent of its channel.
/// Implementations must not swallow transport errors: the delivery runner uses
/// them to schedule retries.
/// </summary>
public interface IMessageSender
{
    /// <summary>Channel this transport delivers.</summary>
    NotificationChannel Channel { get; }

    /// <summary>Delivers the message or throws.</summary>
    Task SendAsync(MessageSendRequest request, CancellationToken cancellationToken);
}

/// <summary>A fully rendered message ready for the transport.</summary>
public sealed record MessageSendRequest(NotificationChannel Channel, string To, string Subject, string Body);
