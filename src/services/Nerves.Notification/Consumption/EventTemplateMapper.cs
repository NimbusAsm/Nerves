using System.Globalization;
using System.Text.Json;
using Nerves.Core;
using Nerves.Notification.Domain;

namespace Nerves.Notification.Consumption;

/// <summary>Template and values produced from one identity event.</summary>
public sealed record MappedNotification(
    NotificationChannel Channel,
    string Recipient,
    string Template,
    IReadOnlyDictionary<string, string> Values);

/// <summary>
/// Maps identity domain events to notification templates. Pure: the consumer
/// resolves the account and renders the template, so the mapping rules stay unit
/// testable without a database or an HTTP server.
/// </summary>
public static class EventTemplateMapper
{
    /// <summary>Event types this service knows how to turn into a message.</summary>
    public const string AccountCreated = "account.created";

    public const string RealnameChanged = "realname.changed";

    /// <summary>Reason code for an event type that has no template: safe to skip.</summary>
    public const string UnsupportedEventCode = "event.unsupported";

    /// <summary>Reason code for an account without a usable address: safe to skip.</summary>
    public const string MissingRecipientCode = "recipient.missing";

    public static Result<MappedNotification> Map(
        EventEnvelope envelope,
        AccountSnapshot? account,
        DateTimeOffset now,
        string issuer)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.Type is not (AccountCreated or RealnameChanged))
        {
            return Result.Failure<MappedNotification>(
                new Reason(UnsupportedEventCode, $"no notification template for event type '{envelope.Type}'"));
        }

        if (account is null || string.IsNullOrWhiteSpace(account.Email))
        {
            return Result.Failure<MappedNotification>(
                new Reason(MissingRecipientCode, "the account has no email address to notify"));
        }

        var displayName = FirstNonEmpty(account.DisplayName, account.Username, account.Email);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["displayName"] = displayName,
            ["username"] = account.Username,
            ["issuer"] = issuer,
            ["occurredAt"] = now.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture),
        };

        if (envelope.Type == RealnameChanged)
        {
            values["state"] = ReadState(envelope.Payload);

            return Result.Success(new MappedNotification(
                NotificationChannel.Email,
                account.Email,
                "account.realname-changed",
                values));
        }

        return Result.Success(new MappedNotification(
            NotificationChannel.Email,
            account.Email,
            "account.welcome",
            values));
    }

    private static string ReadState(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return "unknown";
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("state", out var state)
                && state.ValueKind == JsonValueKind.String
                && state.GetString() is { Length: > 0 } value)
            {
                return value;
            }
        }
        catch (JsonException)
        {
            // A malformed payload must not block the stream: the message still
            // tells the user that their real-name data changed.
        }

        return "unknown";
    }

    private static string FirstNonEmpty(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return "用户";
    }
}
