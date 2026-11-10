using Microsoft.Extensions.Logging;

namespace Nerves.Notification.Delivery;

/// <summary>
/// Development sender: logs the short message and reports success. This is what
/// <c>Sms:Disabled=true</c> wires up, so a fresh checkout needs no gateway.
/// </summary>
public sealed class NoOpSmsSender(ILogger<NoOpSmsSender> logger) : ISmsSender
{
    private static readonly Action<ILogger, string, int, Exception?> Delivered =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(1, "SmsNoOpDelivered"),
            "sms (no-op sender): to={Recipient} characters={Length}");

    public Task SendAsync(SmsSendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Delivered(logger, request.To, request.Body.Length, null);
        return Task.CompletedTask;
    }
}
