using Nerves.Contracts.Audit.V1;

namespace Nerves.Authorization.Integration;

/// <summary>Audit options for the authorization service.</summary>
public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    public string GrpcAddress { get; set; } = "http://127.0.0.1:5205";
}

/// <summary>
/// Best-effort audit emission for authorization changes. A transport failure
/// must never fail the administrative operation itself.
/// </summary>
public sealed class AuthorizationAuditClient(AuditService.AuditServiceClient client)
{
    public async Task AppendAsync(
        string action,
        string actorId,
        string? targetType,
        string? targetId,
        string? detailJson = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.AppendAsync(
                new AppendRequest
                {
                    Stream = "nerves",
                    ActorType = "admin",
                    ActorId = actorId,
                    Action = action,
                    TargetType = targetType ?? string.Empty,
                    TargetId = targetId ?? string.Empty,
                    Result = "success",
                    DetailJson = detailJson ?? string.Empty,
                },
                cancellationToken: cancellationToken);
        }
        catch (global::Grpc.Core.RpcException)
        {
            // Auditing is best effort here; the outbox pattern replaces this later.
        }
    }
}
