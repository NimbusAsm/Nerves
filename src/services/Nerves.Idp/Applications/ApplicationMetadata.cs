using System.Globalization;
using System.Text.Json;
using OpenIddict.Abstractions;

namespace Nerves.Idp.Applications;

/// <summary>Delivery state stored on the OpenIddict application properties.</summary>
public enum ApplicationStatus
{
    Active = 1,
    Disabled = 2,
}

/// <summary>
/// Extra fields the console needs that OpenIddict does not model (description,
/// logo, disabled flag and timestamps). They live in the application properties
/// JSON so no schema change is required.
/// </summary>
public static class ApplicationMetadata
{
    public const string DescriptionKey = "nerves:description";
    public const string LogoKey = "nerves:logo";
    public const string StatusKey = "nerves:status";
    public const string CreatedAtKey = "nerves:createdAt";
    public const string UpdatedAtKey = "nerves:updatedAt";

    /// <summary>Clients provisioned from configuration; they must never be deleted.</summary>
    public static readonly IReadOnlyList<string> BuiltInClientIds = ["nerves-admin", "nerves-demo"];

    public static bool IsBuiltIn(string? clientId) =>
        !string.IsNullOrWhiteSpace(clientId)
        && BuiltInClientIds.Contains(clientId, StringComparer.Ordinal);

    /// <summary>Writes a string property, removing it when the value is empty.</summary>
    public static void SetString(
        this OpenIddictApplicationDescriptor descriptor,
        string key,
        string? value)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (string.IsNullOrEmpty(value))
        {
            descriptor.Properties.Remove(key);
            return;
        }

        descriptor.Properties[key] = JsonSerializer.SerializeToElement(value);
    }

    public static string? ReadString(
        this IReadOnlyDictionary<string, JsonElement>? properties,
        string key)
    {
        if (properties is null || !properties.TryGetValue(key, out var element))
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
    }

    public static ApplicationStatus ReadStatus(OpenIddictApplicationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return ReadStatus(descriptor.Properties);
    }

    public static ApplicationStatus ReadStatus(IReadOnlyDictionary<string, JsonElement>? properties) =>
        string.Equals(properties.ReadString(StatusKey), "disabled", StringComparison.OrdinalIgnoreCase)
            ? ApplicationStatus.Disabled
            : ApplicationStatus.Active;

    public static string ToWire(this ApplicationStatus status) =>
        status == ApplicationStatus.Disabled ? "disabled" : "active";

    public static DateTimeOffset ReadTimestamp(
        IReadOnlyDictionary<string, JsonElement>? properties,
        string key)
    {
        var raw = properties.ReadString(key);
        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : DateTimeOffset.UnixEpoch;
    }

    public static string FormatTimestamp(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    public static bool Equals(this IReadOnlyDictionary<string, JsonElement>? properties, string key, string value) =>
        string.Equals(properties.ReadString(key), value, StringComparison.Ordinal);
}
