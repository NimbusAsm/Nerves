using Nerves.Core;

namespace Nerves.Notification.Options;

/// <summary>
/// Identity event consumer settings (<c>Events:Consumer:*</c>). Off by default:
/// a deployment opts in once it has a token that may read the identity event
/// stream.
/// </summary>
public sealed class EventConsumerOptions
{
    public const string SectionName = "Events:Consumer";

    /// <summary>When false the consumer is not registered at all.</summary>
    public bool Enabled { get; set; }

    /// <summary>Base URL of the identity service, for example <c>http://127.0.0.1:5102</c>.</summary>
    public string IdentityBaseUrl { get; set; } = "http://127.0.0.1:5102";

    /// <summary>
    /// Static bearer token sent to the identity API. Use this when an administrator
    /// token is provisioned by hand; leave empty to use <see cref="ClientCredentials"/>.
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>OAuth client credentials used when no static token is configured.</summary>
    public ClientCredentialsOptions ClientCredentials { get; set; } = new();

    /// <summary>How often the consumer polls the event stream.</summary>
    public int PollSeconds { get; set; } = 30;

    /// <summary>Maximum number of events fetched per poll.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Seconds subtracted from the stored cursor before polling. The event stream
    /// is ordered by occurrence time only, so a small overlap keeps events with an
    /// equal timestamp from being skipped; the unique event id makes the re-read
    /// harmless.
    /// </summary>
    public int LookbackSeconds { get; set; } = 2;

    /// <summary>Issuer shown in the generated emails.</summary>
    public string Issuer { get; set; } = "https://nerves.local/";

    public Result Validate()
    {
        if (!Enabled)
        {
            return Result.Success();
        }

        if (!Uri.TryCreate(IdentityBaseUrl?.Trim(), UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return Result.Failure(Reason.Validation("Events:Consumer:IdentityBaseUrl must be an absolute http(s) URL"));
        }

        if (string.IsNullOrWhiteSpace(BearerToken) && !ClientCredentials.IsConfigured)
        {
            return Result.Failure(Reason.Validation(
                "Events:Consumer requires either BearerToken or a complete ClientCredentials block"));
        }

        if (PollSeconds is < 5 or > 3600)
        {
            return Result.Failure(Reason.Validation("Events:Consumer:PollSeconds must be between 5 and 3600"));
        }

        if (BatchSize is < 1 or > 200)
        {
            return Result.Failure(Reason.Validation("Events:Consumer:BatchSize must be between 1 and 200"));
        }

        if (LookbackSeconds is < 0 or > 300)
        {
            return Result.Failure(Reason.Validation("Events:Consumer:LookbackSeconds must be between 0 and 300"));
        }

        return Result.Success();
    }
}

/// <summary>OAuth client credentials used to read the identity event stream.</summary>
public sealed class ClientCredentialsOptions
{
    public string? TokenEndpoint { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string? Scope { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TokenEndpoint)
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret);
}
