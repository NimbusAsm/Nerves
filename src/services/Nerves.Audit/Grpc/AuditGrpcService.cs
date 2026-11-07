using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Nerves.Audit.Domain;
using Nerves.Audit.Persistence;
using Nerves.Contracts.Audit.V1;

namespace Nerves.Audit.Grpc;

/// <summary>Appends events to the hash chain.</summary>
public sealed class AuditGrpcService(AuditDbContext db) : AuditService.AuditServiceBase
{
    public override Task<GetServiceInfoResponse> GetServiceInfo(
        GetServiceInfoRequest request,
        ServerCallContext context) =>
        Task.FromResult(new GetServiceInfoResponse
        {
            Name = "nerves-audit",
            Version = typeof(AuditGrpcService).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            ContractVersion = "v1",
        });

    public override async Task<AppendResponse> Append(AppendRequest request, ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stream = string.IsNullOrWhiteSpace(request.Stream) ? "default" : request.Stream;

        // Serialize appends per stream so the chain stays linear.
        var chain = await db.AuditChains
            .FromSqlInterpolated($"SELECT * FROM audit.audit_chain WHERE \"Stream\" = {stream} FOR UPDATE")
            .FirstOrDefaultAsync(context.CancellationToken);

        if (chain is null)
        {
            chain = AuditChain.Start(stream);
            db.AuditChains.Add(chain);
        }

        var sequence = chain.Sequence + 1;
        var now = DateTimeOffset.UtcNow;

        var auditEvent = AuditEvent.Append(
            stream,
            sequence,
            chain.LastHash,
            now,
            string.IsNullOrWhiteSpace(request.ActorType) ? "system" : request.ActorType,
            string.IsNullOrWhiteSpace(request.ActorId) ? null : request.ActorId,
            string.IsNullOrWhiteSpace(request.Action) ? "unknown" : request.Action,
            string.IsNullOrWhiteSpace(request.TargetType) ? null : request.TargetType,
            string.IsNullOrWhiteSpace(request.TargetId) ? null : request.TargetId,
            string.IsNullOrWhiteSpace(request.Result) ? "success" : request.Result,
            string.IsNullOrWhiteSpace(request.IpHash) ? null : request.IpHash,
            string.IsNullOrWhiteSpace(request.TraceId) ? null : request.TraceId,
            string.IsNullOrWhiteSpace(request.DetailJson) ? null : request.DetailJson);

        chain.Advance(sequence, auditEvent.Hash);
        db.AuditEvents.Add(auditEvent);
        await db.SaveChangesAsync(context.CancellationToken);

        return new AppendResponse
        {
            EventId = auditEvent.Id.ToString(),
            Sequence = sequence,
            Hash = auditEvent.Hash,
        };
    }
}
