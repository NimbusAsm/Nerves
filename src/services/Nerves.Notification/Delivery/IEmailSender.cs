namespace Nerves.Notification.Delivery;

/// <summary>
/// Transport abstraction for one email. Implementations must not swallow
/// transport errors: the caller uses them to schedule retries.
/// </summary>
public interface IEmailSender
{
    /// <summary>Delivers the message or throws.</summary>
    Task SendAsync(EmailSendRequest request, CancellationToken cancellationToken);
}

/// <summary>A fully rendered message ready for the transport.</summary>
public sealed record EmailSendRequest(string To, string Subject, string Body);
