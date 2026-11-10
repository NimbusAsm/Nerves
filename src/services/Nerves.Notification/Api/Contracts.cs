namespace Nerves.Notification.Api;

/// <summary>Request body for queueing a templated email.</summary>
public sealed record SendEmailRequest(
    string? To,
    string? Template,
    string? Subject,
    string? Body,
    IReadOnlyDictionary<string, string>? Values);

/// <summary>One row of the delivery log.</summary>
public sealed record NotificationResponse(
    Guid Id,
    string To,
    string Template,
    string Subject,
    string Status,
    int Attempts,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset NextAttemptAt);

/// <summary>Cursor page envelope, matching the other management APIs.</summary>
public sealed record PageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
