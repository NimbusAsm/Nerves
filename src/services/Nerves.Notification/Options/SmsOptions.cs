using Nerves.Core;

namespace Nerves.Notification.Options;

/// <summary>
/// SMS gateway settings (<c>Sms:*</c>). Disabled by default so a fresh checkout
/// needs no credentials; enabling it requires a complete gateway configuration.
/// </summary>
public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>When true the service uses the no-op sender and never opens a socket.</summary>
    public bool Disabled { get; set; } = true;

    /// <summary>Gateway shape: <c>webhook</c> (own JSON endpoint) or <c>twilio</c>.</summary>
    public string Provider { get; set; } = "webhook";

    /// <summary>Absolute URL of the JSON webhook. Required when <see cref="Provider"/> is <c>webhook</c>.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Optional bearer token sent to the webhook.</summary>
    public string? WebhookToken { get; set; }

    /// <summary>Twilio account SID. Required when <see cref="Provider"/> is <c>twilio</c>.</summary>
    public string? AccountSid { get; set; }

    /// <summary>Twilio auth token. Required when <see cref="Provider"/> is <c>twilio</c>.</summary>
    public string? AuthToken { get; set; }

    /// <summary>Sender number or messaging service number, required for <c>twilio</c>.</summary>
    public string? From { get; set; }

    /// <summary>Base URL of the Twilio API; overridable for compatible gateways and tests.</summary>
    public string TwilioBaseUrl { get; set; } = "https://api.twilio.com";

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Startup validation. Refuses to boot with an SMS sender that cannot work,
    /// instead of failing on every queueing request.
    /// </summary>
    public Result Validate()
    {
        if (Disabled)
        {
            return Result.Success();
        }

        var provider = Provider.Trim().ToLowerInvariant();
        if (provider is not ("webhook" or "twilio"))
        {
            return Result.Failure(Reason.Validation("Sms:Provider must be either 'webhook' or 'twilio'"));
        }

        if (provider == "webhook" && !IsAbsoluteHttpUrl(WebhookUrl))
        {
            return Result.Failure(Reason.Validation("Sms:WebhookUrl must be an absolute http(s) URL"));
        }

        if (provider == "twilio")
        {
            if (string.IsNullOrWhiteSpace(AccountSid) || string.IsNullOrWhiteSpace(AuthToken))
            {
                return Result.Failure(Reason.Validation("Sms:AccountSid and Sms:AuthToken are required for the twilio provider"));
            }

            if (string.IsNullOrWhiteSpace(From))
            {
                return Result.Failure(Reason.Validation("Sms:From is required for the twilio provider"));
            }

            if (!IsAbsoluteHttpUrl(TwilioBaseUrl))
            {
                return Result.Failure(Reason.Validation("Sms:TwilioBaseUrl must be an absolute http(s) URL"));
            }
        }

        if (TimeoutSeconds is < 1 or > 600)
        {
            return Result.Failure(Reason.Validation("Sms:TimeoutSeconds must be between 1 and 600"));
        }

        return Result.Success();
    }

    private static bool IsAbsoluteHttpUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
