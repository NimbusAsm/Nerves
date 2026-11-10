namespace Nerves.Notification.Consumption;

/// <summary>One identity domain event, as exposed by <c>GET /api/v1/events</c>.</summary>
public sealed record EventEnvelope(
    Guid Id,
    string Type,
    Guid? AccountId,
    Guid? LegalEntityId,
    string Payload,
    DateTimeOffset OccurredAt);

/// <summary>The subset of an identity account the consumer needs to address a message.</summary>
public sealed record AccountSnapshot(
    Guid Id,
    string? Email,
    string? DisplayName,
    string Username);
