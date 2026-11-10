using System.Net.Mail;
using Nerves.Core;

namespace Nerves.Notification.Domain;

/// <summary>
/// A normalised recipient address. Only a plain address is accepted: display
/// names and CR/LF characters are rejected so a value can never inject extra
/// headers when it reaches the SMTP stack.
/// </summary>
public readonly record struct EmailAddress
{
    private EmailAddress(string value) => Value = value;

    /// <summary>Lower-cased address, as stored in the delivery log.</summary>
    public string Value { get; }

    public static Result<EmailAddress> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result.Failure<EmailAddress>(Reason.Validation("recipient address is required"));
        }

        var trimmed = raw.Trim();

        if (trimmed.Length > 320)
        {
            return Result.Failure<EmailAddress>(Reason.Validation("recipient address is longer than 320 characters"));
        }

        if (trimmed.Contains('\r', StringComparison.Ordinal) || trimmed.Contains('\n', StringComparison.Ordinal))
        {
            return Result.Failure<EmailAddress>(Reason.Validation("recipient address must not contain line breaks"));
        }

        if (!MailAddress.TryCreate(trimmed, out var parsed) || parsed is null)
        {
            return Result.Failure<EmailAddress>(Reason.Validation("recipient address is not a valid email address"));
        }

        if (!string.IsNullOrEmpty(parsed.DisplayName))
        {
            return Result.Failure<EmailAddress>(Reason.Validation("recipient address must not include a display name"));
        }

        if (!string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<EmailAddress>(Reason.Validation("recipient address must be a plain email address"));
        }

        return Result.Success(new EmailAddress(trimmed.ToLowerInvariant()));
    }
}
