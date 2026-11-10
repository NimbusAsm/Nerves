using Nerves.Contracts.Audit.V1;

namespace Nerves.Idp.Integration;

/// <summary>Best-effort audit emission from the identity provider.</summary>
public sealed class AuditClient(AuditService.AuditServiceClient client)
{
    /// <summary>Audit an OIDC application change made by an administrator.</summary>
    public Task AppendApplicationAsync(
        string action,
        string clientId,
        string? detailJson,
        CancellationToken cancellationToken) =>
        AppendAsync(action, "admin", null, "application", clientId, "success", detailJson, cancellationToken);

    public async Task AppendAsync(
        string action,
        string actorType,
        string? actorId,
        string? targetId,
        string result,
        string? detailJson,
        CancellationToken cancellationToken) =>
        await AppendAsync(action, actorType, actorId, "account", targetId, result, detailJson, cancellationToken);

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
