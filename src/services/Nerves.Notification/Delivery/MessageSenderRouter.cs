using Nerves.Notification.Domain;

namespace Nerves.Notification.Delivery;

/// <summary>
/// Resolves the transport for a message channel. The router is the only place
/// that knows which channels are configured, so the delivery runner stays
/// channel-agnostic and testable with fake senders.
/// </summary>
public sealed class MessageSenderRouter
{
    private readonly Dictionary<NotificationChannel, IMessageSender> _senders;

    public MessageSenderRouter(IEnumerable<IMessageSender> senders)
    {
        ArgumentNullException.ThrowIfNull(senders);

        _senders = senders.ToDictionary(sender => sender.Channel);
    }

    /// <summary>Whether a transport is configured for the channel.</summary>
    public bool CanSend(NotificationChannel channel) => _senders.ContainsKey(channel);

    /// <summary>Sends one message through the transport registered for its channel.</summary>
    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_senders.TryGetValue(message.Channel, out var sender))
        {
            throw new InvalidOperationException(
                $"no sender is configured for channel '{message.Channel.ToWire()}'");
        }

        return sender.SendAsync(
            new MessageSendRequest(message.Channel, message.To, message.Subject, message.Body),
            cancellationToken);
    }
}
