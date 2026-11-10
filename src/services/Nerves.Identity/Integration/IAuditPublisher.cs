namespace Nerves.Identity.Integration;

/// <summary>Transport used by the outbox dispatcher; replaceable in tests.</summary>
public interface IAuditPublisher
{
    Task AppendAsync(
        string action,
        string actorType,
        string? actorId,
        string? targetType,
        string? targetId,
        string result,
        string? traceId,
        string? detailJson,
        CancellationToken cancellationToken);
}
