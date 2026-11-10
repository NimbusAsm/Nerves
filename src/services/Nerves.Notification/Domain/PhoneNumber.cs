using System.Text.RegularExpressions;
using Nerves.Core;

namespace Nerves.Notification.Domain;

/// <summary>
/// A normalised phone number used as the recipient of SMS messages. Country codes
/// are kept as written; the value is stored without spaces or separators so the
/// gateway receives exactly what was validated.
/// </summary>
public readonly partial record struct PhoneNumber
{
    private PhoneNumber(string value) => Value = value;

    /// <summary>Digits with an optional leading <c>+</c>, spaces and dashes removed.</summary>
    public string Value { get; }

    public static Result<PhoneNumber> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result.Failure<PhoneNumber>(Reason.Validation("recipient phone number is required"));
        }

        var trimmed = raw.Trim();
        if (trimmed.Length > 32)
        {
            return Result.Failure<PhoneNumber>(Reason.Validation("recipient phone number is longer than 32 characters"));
        }

        var normalized = trimmed
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal);

        if (!PhonePattern().IsMatch(normalized))
        {
            return Result.Failure<PhoneNumber>(
                Reason.Validation("recipient phone number must be an international number such as +8613800138000"));
        }

        return Result.Success(new PhoneNumber(normalized));
    }

    [GeneratedRegex(@"^\+?[1-9]\d{4,18}$", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();
}
