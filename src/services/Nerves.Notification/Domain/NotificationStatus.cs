namespace Nerves.Notification.Domain;

/// <summary>Delivery lifecycle of one notification.</summary>
public enum NotificationStatus
{
    /// <summary>Queued and waiting for the delivery worker.</summary>
    Pending = 1,

    /// <summary>The sender accepted the message.</summary>
    Sent = 2,

    /// <summary>Every retry was exhausted; the message will not be sent again.</summary>
    Failed = 3,
}

/// <summary>Wire representation of <see cref="NotificationStatus"/>.</summary>
public static class NotificationStatusWire
{
    public static string ToWire(this NotificationStatus status) => status switch
    {
        NotificationStatus.Pending => "pending",
        NotificationStatus.Sent => "sent",
        NotificationStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "unknown notification status"),
    };
}
