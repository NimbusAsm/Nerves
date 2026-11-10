using Nerves.Contracts.Audit.V1;

namespace Nerves.Session.Integration;

/// <summary>Transport used by the dispatcher; replaceable in tests.</summary>
public interface IAuditPublisher
{
    Task AppendAsync(
        string action,
        string actorType,
        string? actorId,
        string? targetType,
        string? targetId,
        string result,
        string? detailJson,
        CancellationToken cancellationToken);
}

/// <summary>Adapts the generated audit client to <see cref="IAuditPublisher"/>.</summary>
public sealed class AuditPublisher(AuditService.AuditServiceClient client) : IAuditPublisher
{
    public async Task AppendAsync(
        string action,
        string actorType,
        string? actorId,
        string? targetType,
        string? targetId,
        string result,
        string? detailJson,
        CancellationToken cancellationToken)
    {
        // Deliberately no catch: the dispatcher owns retry and the attempt
        // counter, so swallowing here would mark the event dispatched.
        await client.AppendAsync(
                new AppendRequest
                {
                    Stream = "nerves",
                    ActorType = actorType,
                    ActorId = actorId ?? string.Empty,
                    Action = action,
                    TargetType = targetType ?? string.Empty,
                    TargetId = targetId ?? string.Empty,
                    Result = result,
                    DetailJson = detailJson ?? string.Empty,
                },
                cancellationToken: cancellationToken);
    }
}
