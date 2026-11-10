namespace Nerves.Notification.Delivery;

/// <summary>
/// Transport abstraction for one short message. Implementations must not swallow
/// transport errors: the caller uses them to schedule retries.
/// </summary>
public interface ISmsSender
{
    /// <summary>Delivers the message or throws.</summary>
    Task SendAsync(SmsSendRequest request, CancellationToken cancellationToken);
}

/// <summary>A rendered short message ready for the gateway.</summary>
public sealed record SmsSendRequest(string To, string Body);
