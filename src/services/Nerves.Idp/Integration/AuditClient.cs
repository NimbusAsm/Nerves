using Nerves.Contracts.Audit.V1;

namespace Nerves.Idp.Integration;

/// <summary>Best-effort audit emission from the identity provider.</summary>
public sealed class AuditClient(AuditService.AuditServiceClient client)
{
    public async Task AppendAsync(
        string action,
        string actorType,
        string? actorId,
        string? targetId,
        string result,
        string? detailJson,
        CancellationToken cancellationToken)
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
                    TargetType = "account",
                    TargetId = targetId ?? string.Empty,
                    Result = result,
                    DetailJson = detailJson ?? string.Empty,
                },
                cancellationToken: cancellationToken);
        }
        catch (global::Grpc.Core.RpcException)
        {
            // Auditing must not fail the sign-in path.
        }
    }
}
