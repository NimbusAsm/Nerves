using Nerves.Contracts.Audit.V1;

namespace Nerves.ComplianceCn.Integration;

/// <summary>
/// Best-effort transport to the audit service, used only by the outbox
/// dispatcher. Domain code writes outbox rows instead so an audit outage never
/// loses an event.
/// </summary>
public interface IComplianceAuditPublisher
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

/// <summary>Thin adapter over the generated audit client.</summary>
public sealed class ComplianceAuditPublisher(AuditService.AuditServiceClient client) : IComplianceAuditPublisher
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
        // No catch here: the dispatcher owns retry and the attempt counter.
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
