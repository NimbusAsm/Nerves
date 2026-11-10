using Nerves.Notification.Domain;

namespace Nerves.Notification.Delivery;

/// <summary>
/// Adapts the SMS transport to the channel-neutral <see cref="IMessageSender"/>
/// contract used by the dispatcher.
/// </summary>
public sealed class SmsMessageSender(ISmsSender sms) : IMessageSender
{
    public NotificationChannel Channel => NotificationChannel.Sms;

    public Task SendAsync(MessageSendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return sms.SendAsync(new SmsSendRequest(request.To, request.Body), cancellationToken);
    }
}
