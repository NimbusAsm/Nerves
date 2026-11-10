using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Nerves.Notification.Options;

namespace Nerves.Notification.Delivery;

/// <summary>
/// Sends through a plain SMTP server. Kept intentionally small: the notification
/// service owns retries, so this class maps configuration to one attempt and lets
/// failures propagate.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender, IDisposable
{
    private readonly SmtpOptions _options;
    private readonly SmtpClient _client;
    private readonly string _from;
    private bool _disposed;

    public SmtpEmailSender(IOptions<SmtpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _from = _options.From;

        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new InvalidOperationException("Smtp:Host is required when Smtp:Disabled is false.");
        }

        _client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.Tls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Timeout = (int)Math.Clamp(_options.TimeoutSeconds * 1000, 1000, 120_000),
        };

        if (!string.IsNullOrWhiteSpace(_options.User))
        {
            _client.Credentials = new NetworkCredential(_options.User, _options.Password);
        }
    }

    public async Task SendAsync(EmailSendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var message = new MailMessage(_from, request.To, request.Subject, request.Body)
        {
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8,
            IsBodyHtml = false,
        };

        await _client.SendMailAsync(message, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _client.Dispose();
    }
}
