using System.Net.Mail;
using Nerves.Core;
using Nerves.Notification.Domain;

namespace Nerves.Notification.Options;

/// <summary>SMTP transport settings (<c>Smtp:*</c>).</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>When true the service uses the no-op sender and never opens a socket.</summary>
    public bool Disabled { get; set; } = true;

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 587;

    public string? User { get; set; }

    public string? Password { get; set; }

    public string From { get; set; } = "noreply@nerves.local";

    /// <summary>Use STARTTLS/SMTPS when true.</summary>
    public bool Tls { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Startup validation. Refuses to boot with an SMTP sender that cannot work,
    /// instead of failing on every sweep after the first request arrives.
    /// </summary>
    public Result Validate()
    {
        if (Disabled)
        {
            return Result.Success();
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            return Result.Failure(Reason.Validation("Smtp:Host is required when Smtp:Disabled is false"));
        }

        if (Port is < 1 or > 65535)
        {
            return Result.Failure(Reason.Validation("Smtp:Port must be between 1 and 65535"));
        }

        if (!MailAddress.TryCreate(From, out _))
        {
            return Result.Failure(Reason.Validation("Smtp:From must be a valid email address"));
        }

        if (TimeoutSeconds is < 1 or > 600)
        {
            return Result.Failure(Reason.Validation("Smtp:TimeoutSeconds must be between 1 and 600"));
        }

        return Result.Success();
    }
}

/// <summary>Delivery worker and retry settings (<c>Delivery:*</c>).</summary>
public sealed class DeliveryOptions
{
    public const string SectionName = "Delivery";

    /// <summary>How often the worker looks for due messages.</summary>
    public int SweepSeconds { get; set; } = 30;

    /// <summary>Maximum number of delivery attempts before the message fails.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Delay before the first retry.</summary>
    public int BaseDelaySeconds { get; set; } = 60;

    /// <summary>Cap for the exponential backoff.</summary>
    public int MaxDelaySeconds { get; set; } = 3600;

    /// <summary>How many due messages one sweep may claim.</summary>
    public int BatchSize { get; set; } = 20;

    public RetryPolicy ToRetryPolicy()
    {
        var attempts = Math.Clamp(MaxAttempts, 1, 20);
        var baseSeconds = Math.Clamp(BaseDelaySeconds, 1, 86_400);
        var maxSeconds = Math.Clamp(MaxDelaySeconds, baseSeconds, 604_800);

        return new RetryPolicy(attempts, TimeSpan.FromSeconds(baseSeconds), TimeSpan.FromSeconds(maxSeconds));
    }
}
