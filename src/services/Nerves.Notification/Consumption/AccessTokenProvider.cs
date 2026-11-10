using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nerves.Core;
using Nerves.Notification.Options;

namespace Nerves.Notification.Consumption;

/// <summary>
/// Supplies the bearer token used against the identity API. A configured static
/// token wins; otherwise the OAuth client-credentials grant is executed and its
/// token cached until shortly before expiry.
/// </summary>
public sealed class AccessTokenProvider(
    IHttpClientFactory clients,
    IOptions<EventConsumerOptions> options,
    IClock clock) : IDisposable
{
    /// <summary>Name of the named client registered for identity calls.</summary>
    public const string HttpClientName = "nerves-identity";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _cached;
    private DateTimeOffset _expiresAt;

    public void Dispose()
    {
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task<string> GetAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!string.IsNullOrWhiteSpace(settings.BearerToken))
        {
            return settings.BearerToken;
        }

        var credentials = settings.ClientCredentials;
        if (!credentials.IsConfigured)
        {
            throw new InvalidOperationException(
                "Events:Consumer requires either BearerToken or a complete ClientCredentials block");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrEmpty(_cached) && clock.UtcNow < _expiresAt)
            {
                return _cached;
            }

            var (token, lifetime) = await RequestAsync(credentials, cancellationToken);
            _cached = token;
            _expiresAt = clock.UtcNow.AddSeconds(Math.Max(lifetime - 60, 30));
            return token;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<(string Token, int LifetimeSeconds)> RequestAsync(
        ClientCredentialsOptions credentials,
        CancellationToken cancellationToken)
    {
        using var client = clients.CreateClient(HttpClientName);

        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = credentials.ClientId!,
            ["client_secret"] = credentials.ClientSecret!,
        };

        if (!string.IsNullOrWhiteSpace(credentials.Scope))
        {
            form["scope"] = credentials.Scope;
        }

        using var content = new FormUrlEncodedContent(form);

        using var response = await client.PostAsync(
            new Uri(credentials.TokenEndpoint!, UriKind.Absolute),
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"identity token endpoint returned {(int)response.StatusCode}"),
                inner: null,
                response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("access_token", out var tokenElement)
            || tokenElement.GetString() is not { Length: > 0 } token)
        {
            throw new InvalidOperationException("identity token response did not contain access_token");
        }

        var lifetime = document.RootElement.TryGetProperty("expires_in", out var expiresElement)
                       && expiresElement.TryGetInt32(out var seconds)
            ? seconds
            : 300;

        return (token, lifetime);
    }
}
