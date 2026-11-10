namespace Nerves.Notification.Domain;

/// <summary>Transport used to deliver a notification.</summary>
public enum NotificationChannel
{
    /// <summary>Plain-text email through SMTP (or the no-op sender in development).</summary>
    Email = 1,

    /// <summary>Short text message through an HTTP gateway.</summary>
    Sms = 2,
}

/// <summary>Wire representation of <see cref="NotificationChannel"/>.</summary>
public static class NotificationChannelWire
{
    public static string ToWire(this NotificationChannel channel) => channel switch
    {
        NotificationChannel.Email => "email",
        NotificationChannel.Sms => "sms",
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "unknown notification channel"),
    };

    public static bool TryParse(string? value, out NotificationChannel channel)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "email":
                channel = NotificationChannel.Email;
                return true;
            case "sms":
                channel = NotificationChannel.Sms;
                return true;
            default:
                channel = default;
                return false;
        }
    }
}
