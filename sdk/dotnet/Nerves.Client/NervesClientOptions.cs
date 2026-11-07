namespace Nerves.Client;

/// <summary>
/// Everything <see cref="NervesClient"/> needs: where the deployment lives, how to
/// authenticate, and which <see cref="HttpClient"/> to reuse.
/// </summary>
public sealed class NervesClientOptions
{
    /// <summary>Origin of the deployment, e.g. <c>https://accounts.example.com</c>.</summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>Personal access token sent as a bearer token. Optional.</summary>
    public string? Token { get; init; }

    /// <summary>
    /// Caller-owned <see cref="HttpClient"/>. Inject one to share handlers,
    /// instrumentation, or a proxy; the client is then not disposed by the SDK.
    /// </summary>
    public HttpClient? HttpClient { get; init; }

    /// <summary>Retries for 429, 5xx, and connection failures. Defaults to two.</summary>
    public int MaxRetries { get; init; } = 2;

    /// <summary>Base delay for exponential backoff. Defaults to 200 ms.</summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);
}
