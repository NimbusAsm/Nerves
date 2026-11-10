namespace Nerves.Notification.Api;

/// <summary>Request body for queueing a templated email.</summary>
public sealed record SendEmailRequest(
    string? To,
    string? Template,
    string? Subject,
    string? Body,
    IReadOnlyDictionary<string, string>? Values);

/// <summary>Request body for queueing a templated notification on an explicit channel.</summary>
public sealed record SendMessageRequest(
    string? To,
    string? Template,
    string? Channel,
    IReadOnlyDictionary<string, string>? Values);

/// <summary>One row of the delivery log.</summary>
public sealed record NotificationResponse(
    Guid Id,
    string To,
    string Channel,
    string Template,
    string Subject,
    string Status,
    int Attempts,
    string? Error,
    Guid? SourceEventId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset NextAttemptAt);

/// <summary>Cursor page envelope, matching the other management APIs.</summary>
public sealed record PageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
