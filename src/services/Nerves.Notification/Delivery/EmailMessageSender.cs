using Nerves.Notification.Domain;

namespace Nerves.Notification.Delivery;

/// <summary>
/// Adapts the SMTP transport to the channel-neutral <see cref="IMessageSender"/>
/// contract used by the dispatcher.
/// </summary>
public sealed class EmailMessageSender(IEmailSender email) : IMessageSender
{
    public NotificationChannel Channel => NotificationChannel.Email;

    public Task SendAsync(MessageSendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return email.SendAsync(new EmailSendRequest(request.To, request.Subject, request.Body), cancellationToken);
    }
}
