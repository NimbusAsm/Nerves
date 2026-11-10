using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nerves.Identity.Integration;

/// <summary>Settings for calling the notification service (<c>Notification:*</c>).</summary>
public sealed class NotificationClientOptions
{
    public const string SectionName = "Notification";

    /// <summary>Base address of the notification service, for example <c>http://127.0.0.1:5108</c>.</summary>
    public string? BaseAddress { get; set; }

    /// <summary>Admin bearer token used for the service-to-service call.</summary>
    public string? AdminToken { get; set; }

    /// <summary>
    /// File containing the admin token. Read per request when set, so a rotated
    /// token takes effect without restarting the identity service.
    /// </summary>
    public string? AdminTokenFile { get; set; }

    public int TimeoutSeconds { get; set; } = 5;
}

/// <summary>
/// Queues a templated email through the notification service. Delivery failures
/// never fail the caller: a password reset still succeeds when the mail cannot
/// be queued, and the notification service keeps its own retry log.
/// </summary>
public sealed class NotificationClient : INotificationSender
{
    public const string HttpClientName = "notification";
    public const string PasswordResetTemplate = "account.password-reset";

    private static readonly Action<ILogger, Exception?> NotConfigured =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, "NotificationNotConfigured"),
            "notification service is not configured; password reset email skipped");

    private static readonly Action<ILogger, int, Exception?> Rejected =
        LoggerMessage.Define<int>(LogLevel.Warning, new EventId(2, "NotificationRejected"),
            "notification service returned {StatusCode}");

    private static readonly Action<ILogger, Exception?> Unreachable =
        LoggerMessage.Define(LogLevel.Warning, new EventId(3, "NotificationUnreachable"),
            "notification service is unreachable");

    private readonly IHttpClientFactory _clients;
    private readonly NotificationClientOptions _options;
    private readonly ILogger<NotificationClient> _logger;

    public NotificationClient(
        IHttpClientFactory clients,
        IOptions<NotificationClientOptions> options,
        ILogger<NotificationClient> logger)
    {
        ArgumentNullException.ThrowIfNull(clients);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _clients = clients;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.BaseAddress) && !string.IsNullOrWhiteSpace(ResolveAdminToken());

    private string? ResolveAdminToken()
    {
        if (!string.IsNullOrWhiteSpace(_options.AdminTokenFile))
        {
            try
            {
                var fromFile = File.ReadAllText(_options.AdminTokenFile).Trim();
                if (!string.IsNullOrWhiteSpace(fromFile))
                {
                    return fromFile;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Fall back to the inline token; a missing file is a deployment
                // state, not a reason to fail the reset request.
            }
        }

        return _options.AdminToken;
    }

    public async Task<bool> SendPasswordResetAsync(
        string recipient,
        string displayName,
        string resetUrl,
        int minutes,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);

        if (!IsConfigured)
        {
            // No notification service in this deployment: the reset token is
            // still valid, the caller just cannot deliver the email.
            NotConfigured(_logger, null);
            return false;
        }

        return await SendAsync(
            recipient,
            PasswordResetTemplate,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["displayName"] = displayName,
                ["resetUrl"] = resetUrl,
                ["minutes"] = minutes.ToString(CultureInfo.InvariantCulture),
            },
            cancellationToken);
    }

    private async Task<bool> SendAsync(
        string to,
        string template,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        var client = _clients.CreateClient(HttpClientName);

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            _options.BaseAddress!.TrimEnd('/') + "/api/v1/notifications/email")
        {
            Content = JsonContent.Create(new { to, template, values }),
        };

        message.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ResolveAdminToken());

        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Rejected(_logger, (int)response.StatusCode, null);
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Unreachable(_logger, exception);
            return false;
        }
    }
}
