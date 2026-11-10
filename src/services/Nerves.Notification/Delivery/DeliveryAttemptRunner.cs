using Microsoft.Extensions.Logging;
using Nerves.Notification.Domain;

namespace Nerves.Notification.Delivery;

/// <summary>Result of one delivery attempt.</summary>
public enum DeliveryOutcome
{
    /// <summary>The sender accepted the message.</summary>
    Sent = 1,

    /// <summary>The attempt failed and a retry was scheduled.</summary>
    RetryScheduled = 2,

    /// <summary>The attempt failed and the retry budget is exhausted.</summary>
    Failed = 3,
}

/// <summary>
/// Runs one delivery attempt and records its outcome on the message. Kept free
/// of persistence so it can be unit tested with a fake sender; the dispatcher
/// owns the database round trip.
/// </summary>
public sealed class DeliveryAttemptRunner(MessageSenderRouter senders, ILogger<DeliveryAttemptRunner> logger)
{
    private static readonly Action<ILogger, Guid, string, string, int, Exception?> AttemptFailed =
        LoggerMessage.Define<Guid, string, string, int>(
            LogLevel.Warning,
            new EventId(2, "NotificationDeliveryFailed"),
            "notification delivery failed: id={NotificationId} channel={Channel} error={Error} attempts={Attempts}");

    private static readonly Action<ILogger, Guid, string, Exception?> DeliveryExhausted =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Error,
            new EventId(3, "NotificationDeliveryExhausted"),
            "notification delivery exhausted its retries: id={NotificationId} channel={Channel}");

    public async Task<DeliveryOutcome> DeliverAsync(
        NotificationMessage message,
        RetryPolicy policy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(policy);

        try
        {
            await senders.SendAsync(message, cancellationToken);

            message.MarkSent(now);
            return DeliveryOutcome.Sent;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var error = Describe(exception);
            message.RecordFailure(error, now, policy);
            AttemptFailed(
                logger,
                message.Id,
                message.Channel.ToWire(),
                error,
                message.Attempts,
                exception);

            if (message.Status == NotificationStatus.Failed)
            {
                DeliveryExhausted(logger, message.Id, message.Channel.ToWire(), null);
                return DeliveryOutcome.Failed;
            }

            return DeliveryOutcome.RetryScheduled;
        }
    }

    /// <summary>
    /// Flattens the exception chain: <c>SmtpClient</c> reports "Failure sending
    /// mail." and hides the actual socket error in the inner exception, which is
    /// exactly the line an operator needs in the delivery log.
    /// </summary>
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message) && !messages.Contains(current.Message, StringComparer.Ordinal))
            {
                messages.Add(current.Message.Trim());
            }
        }

        return messages.Count == 0 ? "delivery failed" : string.Join(": ", messages);
    }
}
