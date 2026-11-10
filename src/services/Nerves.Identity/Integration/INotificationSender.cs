namespace Nerves.Identity.Integration;

/// <summary>Outbound notification port, replaceable in tests.</summary>
public interface INotificationSender
{
    /// <summary>Queues the password reset email. Returns false when it could not be queued.</summary>
    Task<bool> SendPasswordResetAsync(
        string recipient,
        string displayName,
        string resetUrl,
        int minutes,
        CancellationToken cancellationToken);
}
