using Microsoft.Extensions.Options;
using Nerves.Contracts.Audit.V1;
using Nerves.Identity.Options;

namespace Nerves.Identity.Integration;

/// <summary>
/// Best-effort audit emission. Auditing must never fail a business operation,
/// so transport errors are swallowed (the outbox pattern replaces this later).
/// </summary>
public sealed class AuditClient(AuditService.AuditServiceClient client, IOptions<AuditOptions> options)
{
    public AuditOptions Options { get; } = options.Value;

    public async Task AppendAsync(
        string action,
        string actorType,
        string? actorId,
        string? targetType,
        string? targetId,
        string result,
        string? traceId = null,
        string? detailJson = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
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
                    TraceId = traceId ?? string.Empty,
                    DetailJson = detailJson ?? string.Empty,
                },
                cancellationToken: cancellationToken);
        }
        catch (global::Grpc.Core.RpcException)
        {
            // Never let audit loss break identity operations.
        }
    }
}
