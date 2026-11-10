using Microsoft.Extensions.Logging;

namespace Nerves.Notification.Delivery;

/// <summary>
/// Development sender: logs the message and reports success. This is what
/// <c>Smtp:Disabled=true</c> wires up, so a fresh checkout needs no mail server.
/// </summary>
public sealed class NoOpEmailSender(ILogger<NoOpEmailSender> logger) : IEmailSender
{
    private static readonly Action<ILogger, string, string, Exception?> Delivered =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(1, "EmailNoOpDelivered"),
            "email (no-op sender): to={Recipient} subject={Subject}");

    public Task SendAsync(EmailSendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Delivered(logger, request.To, request.Subject, null);
        return Task.CompletedTask;
    }
}
