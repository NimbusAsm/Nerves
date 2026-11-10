using Nerves.Core;

namespace Nerves.Notification.Domain;

/// <summary>
/// One queued notification and its delivery log. The rendered subject and body
/// are stored with the record so a retry sends exactly what was accepted, and so
/// the log stays readable after the template changes.
/// </summary>
public sealed class NotificationMessage
{
    /// <summary>Upper bound for the persisted error column.</summary>
    public const int MaxErrorLength = 1024;

    /// <summary>Upper bound for the persisted recipient column.</summary>
    public const int MaxRecipientLength = 320;

    private NotificationMessage()
    {
        To = string.Empty;
        Template = string.Empty;
        Subject = string.Empty;
        Body = string.Empty;
    }

    private NotificationMessage(
        Guid id,
        NotificationChannel channel,
        string recipient,
        string template,
        string subject,
        string body,
        DateTimeOffset now,
        Guid? sourceEventId)
    {
        Id = id;
        Channel = channel;
        To = recipient;
        Template = template;
        Subject = subject;
        Body = body;
        SourceEventId = sourceEventId;
        Status = NotificationStatus.Pending;
        Attempts = 0;
        CreatedAt = now;
        NextAttemptAt = now;
    }

    public Guid Id { get; private set; }

    /// <summary>Transport that will carry the message.</summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>Normalised recipient: an email address or a phone number.</summary>
    public string To { get; private set; }

    /// <summary>Template code, for example <c>account.welcome</c>.</summary>
    public string Template { get; private set; }

    public string Subject { get; private set; }

    /// <summary>Rendered plain-text body.</summary>
    public string Body { get; private set; }

    /// <summary>
    /// Identity event that produced this message, when it was queued by the event
    /// consumer. The unique index on this column makes event consumption idempotent.
    /// </summary>
    public Guid? SourceEventId { get; private set; }

    public NotificationStatus Status { get; private set; }

    /// <summary>Number of delivery attempts already made.</summary>
    public int Attempts { get; private set; }

    /// <summary>Last delivery error, truncated to <see cref="MaxErrorLength"/>.</summary>
    public string? Error { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>When the worker may attempt the next delivery.</summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    /// <summary>Queues an email. Kept as the short form used by the SMTP path.</summary>
    public static NotificationMessage Create(
        EmailAddress recipient,
        string template,
        string subject,
        string body,
        DateTimeOffset now) =>
        Create(NotificationChannel.Email, recipient.Value, template, subject, body, now);

    /// <summary>Queues a message on any channel, optionally remembering its source event.</summary>
    public static NotificationMessage Create(
        NotificationChannel channel,
        string recipient,
        string template,
        string subject,
        string body,
        DateTimeOffset now,
        Guid? sourceEventId = null)
    {
        if (!Enum.IsDefined(channel))
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "unknown notification channel");
        }

        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new ArgumentException("recipient must not be empty", nameof(recipient));
        }

        if (recipient.Length > MaxRecipientLength)
        {
            throw new ArgumentException($"recipient is longer than {MaxRecipientLength} characters", nameof(recipient));
        }

        if (string.IsNullOrWhiteSpace(template))
        {
            throw new ArgumentException("template must not be empty", nameof(template));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("subject must not be empty", nameof(subject));
        }

        return new NotificationMessage(
            IdGenerator.NewId(now),
            channel,
            recipient.Trim(),
            template,
            subject,
            body,
            now,
            sourceEventId);
    }

    public bool IsDue(DateTimeOffset now) => Status == NotificationStatus.Pending && NextAttemptAt <= now;

    /// <summary>Marks the message as accepted by the sender.</summary>
    public void MarkSent(DateTimeOffset now)
    {
        Status = NotificationStatus.Sent;
        SentAt = now;
        Error = null;
    }

    /// <summary>
    /// Records a failed attempt and either schedules the next retry or moves the
    /// message to <see cref="NotificationStatus.Failed"/> when attempts run out.
    /// </summary>
    public void RecordFailure(string error, DateTimeOffset now, RetryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        Attempts += 1;
        Error = TruncateError(error);
        SentAt = null;

        if (Attempts >= policy.MaxAttempts)
        {
            Status = NotificationStatus.Failed;
            NextAttemptAt = now;
            return;
        }

        Status = NotificationStatus.Pending;
        NextAttemptAt = now.Add(policy.DelayAfterFailure(Attempts));
    }

    private static string TruncateError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return "delivery failed";
        }

        return error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
    }
}
