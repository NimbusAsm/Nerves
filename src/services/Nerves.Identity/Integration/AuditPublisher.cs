namespace Nerves.Identity.Integration;

/// <summary>Adapts the generated audit client to <see cref="IAuditPublisher"/>.</summary>
public sealed class AuditPublisher(AuditClient client) : IAuditPublisher
{
    public Task AppendAsync(
        string action,
        string actorType,
        string? actorId,
        string? targetType,
        string? targetId,
        string result,
        string? traceId,
        string? detailJson,
        CancellationToken cancellationToken) =>
        client.AppendAsync(
            action, actorType, actorId, targetType, targetId, result, traceId, detailJson, cancellationToken);
}
