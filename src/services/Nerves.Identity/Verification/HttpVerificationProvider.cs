using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nerves.Identity.Verification;

/// <summary>Settings for the outbound HTTP verification channel (<c>Verification:Http:*</c>).</summary>
public sealed class HttpVerificationOptions
{
    public const string SectionName = "Verification:Http";

    /// <summary>Base address of the provider. Required when the provider is <c>http</c>.</summary>
    public string? BaseAddress { get; set; }

    /// <summary>Optional bearer token; the raw document number is never sent.</summary>
    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// Forwards a verification start to an external HTTP provider. The provider is
/// expected to answer with <c>{ "outcome": "processing|verified|rejected",
/// "reference": "...", "message": "..." }</c>; anything else is treated as a
/// failure and leaves the entity pending.
/// </summary>
public sealed class HttpVerificationProvider : IVerificationProvider
{
    public const string HttpClientName = "verification";

    private static readonly Action<ILogger, int, Exception?> NonSuccess =
        LoggerMessage.Define<int>(LogLevel.Warning, new EventId(1, "VerificationProviderStatus"),
            "verification provider returned {StatusCode}");

    private static readonly Action<ILogger, Exception?> Unreachable =
        LoggerMessage.Define(LogLevel.Warning, new EventId(2, "VerificationProviderUnreachable"),
            "verification provider request failed");

    private readonly IHttpClientFactory _clients;
    private readonly HttpVerificationOptions _options;
    private readonly ILogger<HttpVerificationProvider> _logger;

    public HttpVerificationProvider(
        IHttpClientFactory clients,
        IOptions<HttpVerificationOptions> options,
        ILogger<HttpVerificationProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(clients);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _clients = clients;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "http";

    public async Task<VerificationProviderResponse> StartAsync(
        VerificationProviderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_options.BaseAddress))
        {
            return new VerificationProviderResponse(
                VerificationProviderOutcome.Failed, null, "Verification:Http:BaseAddress is not configured.");
        }

        var client = _clients.CreateClient(HttpClientName);

        using var message = new HttpRequestMessage(HttpMethod.Post, _options.BaseAddress.TrimEnd('/') + "/verify")
        {
            Content = JsonContent.Create(new
            {
                legalEntityId = request.LegalEntityId,
                entityType = request.LegalEntityType,
                documentType = request.DocumentType,
                documentHash = request.DocumentHash,
                country = request.Country,
                displayName = request.DisplayName,
                callbackUrl = request.CallbackUrl,
                requestedAt = request.RequestedAt,
            }),
        };

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            message.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        try
        {
            using var response = await client.SendAsync(message, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                NonSuccess(_logger, (int)response.StatusCode, null);
                return new VerificationProviderResponse(
                    VerificationProviderOutcome.Failed, null, "Verification provider rejected the request.");
            }

            var payload = await response.Content.ReadFromJsonAsync<ProviderReply>(cancellationToken);
            if (payload is null)
            {
                return new VerificationProviderResponse(
                    VerificationProviderOutcome.Failed, null, "Verification provider returned an empty body.");
            }

            return new VerificationProviderResponse(MapOutcome(payload.Outcome), payload.Reference, payload.Message);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Unreachable(_logger, exception);
            return new VerificationProviderResponse(
                VerificationProviderOutcome.Failed, null, "Verification provider is unreachable.");
        }
    }

    private static VerificationProviderOutcome MapOutcome(string? outcome) => outcome?.Trim().ToLowerInvariant() switch
    {
        "verified" or "approved" => VerificationProviderOutcome.Verified,
        "rejected" or "declined" => VerificationProviderOutcome.Rejected,
        "processing" or "pending" => VerificationProviderOutcome.Processing,
        _ => VerificationProviderOutcome.Failed,
    };

    private sealed record ProviderReply(string? Outcome, string? Reference, string? Message);
}
