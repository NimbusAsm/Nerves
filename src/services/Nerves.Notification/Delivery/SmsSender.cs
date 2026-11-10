using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nerves.Notification.Options;

namespace Nerves.Notification.Delivery;

/// <summary>
/// Sends short messages through an HTTP gateway. Two request shapes are
/// supported, selected by <c>Sms:Provider</c>:
/// <list type="bullet">
///   <item><c>webhook</c>: <c>POST</c> a small JSON body to a gateway you own.</item>
///   <item><c>twilio</c>: the Twilio Messages API form encoding, usable against any
///   Twilio-compatible endpoint.</item>
/// </list>
/// Failures are surfaced as <see cref="HttpRequestException"/> so the delivery
/// runner can schedule a retry.
/// </summary>
public sealed class SmsSender(
    IHttpClientFactory clients,
    IOptions<SmsOptions> options,
    ILogger<SmsSender> logger) : ISmsSender
{
    /// <summary>Name of the named client registered for SMS delivery.</summary>
    public const string HttpClientName = "nerves-sms";

    private static readonly Action<ILogger, string, int, Exception?> GatewayRejected =
        LoggerMessage.Define<string, int>(
            LogLevel.Warning,
            new EventId(1, "SmsGatewayRejected"),
            "sms gateway rejected the message: to={Recipient} status={StatusCode}");

    private readonly SmsOptions _options = options.Value;

    public async Task SendAsync(SmsSendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var client = clients.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 1, 120));

        using var message = _options.Provider.Trim().ToLowerInvariant() switch
        {
            "twilio" => BuildTwilioRequest(request),
            "webhook" => BuildWebhookRequest(request),
            _ => throw new InvalidOperationException($"unknown Sms:Provider '{_options.Provider}'"),
        };

        using var response = await client.SendAsync(message, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        GatewayRejected(logger, request.To, (int)response.StatusCode, null);

        throw new HttpRequestException(
            $"SMS gateway returned {(int)response.StatusCode}: {Truncate(detail, 200)}",
            inner: null,
            response.StatusCode);
    }

    private HttpRequestMessage BuildWebhookRequest(SmsSendRequest request)
    {
        var payload = JsonSerializer.Serialize(new { to = request.To, body = request.Body });
        var message = new HttpRequestMessage(HttpMethod.Post, RequireUri(_options.WebhookUrl, "Sms:WebhookUrl"))
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        if (!string.IsNullOrWhiteSpace(_options.WebhookToken))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.WebhookToken);
        }

        return message;
    }

    private HttpRequestMessage BuildTwilioRequest(SmsSendRequest request)
    {
        var baseUrl = _options.TwilioBaseUrl.TrimEnd('/');
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"{baseUrl}/2010-04-01/Accounts/{_options.AccountSid}/Messages.json");

        var message = new HttpRequestMessage(HttpMethod.Post, RequireUri(url, "Sms:TwilioBaseUrl"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["To"] = request.To,
                ["From"] = _options.From ?? string.Empty,
                ["Body"] = request.Body,
            }),
        };

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(string.Create(
                CultureInfo.InvariantCulture,
                $"{_options.AccountSid}:{_options.AuthToken}")));
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        return message;
    }

    private static Uri RequireUri(string? value, string setting) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            ? uri
            : throw new InvalidOperationException($"{setting} must be an absolute URL when the SMS sender is enabled.");

    private static string Truncate(string value, int max) =>
        string.IsNullOrWhiteSpace(value) ? "no body" : value.Length <= max ? value : value[..max];
}
