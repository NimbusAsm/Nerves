using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Nerves.Audit.Domain;

/// <summary>
/// One append-only audit record. Events form a hash chain per stream so that
/// deleting or altering a row breaks verification (PLAN §5.2).
/// </summary>
public sealed class AuditEvent
{
    private AuditEvent()
    {
        Stream = string.Empty;
        ActorType = string.Empty;
        Action = string.Empty;
        Result = string.Empty;
        PreviousHash = string.Empty;
        Hash = string.Empty;
    }

    private AuditEvent(
        Guid id,
        string stream,
        long sequence,
        DateTimeOffset occurredAt,
        string actorType,
        string? actorId,
        string action,
        string? targetType,
        string? targetId,
        string result,
        string? ipHash,
        string? traceId,
        string? detail,
        string previousHash,
        string hash)
    {
        Id = id;
        Stream = stream;
        Sequence = sequence;
        OccurredAt = occurredAt;
        ActorType = actorType;
        ActorId = actorId;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        Result = result;
        IpHash = ipHash;
        TraceId = traceId;
        Detail = detail;
        PreviousHash = previousHash;
        Hash = hash;
    }

    public Guid Id { get; private set; }

    public string Stream { get; private set; }

    public long Sequence { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string ActorType { get; private set; }

    public string? ActorId { get; private set; }

    public string Action { get; private set; }

    public string? TargetType { get; private set; }

    public string? TargetId { get; private set; }

    public string Result { get; private set; }

    public string? IpHash { get; private set; }

    public string? TraceId { get; private set; }

    public string? Detail { get; private set; }

    public string PreviousHash { get; private set; }

    public string Hash { get; private set; }

    /// <summary>When the archive job copied the event to cold storage.</summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    /// <summary>Builds the event and returns the hash that becomes the next link.</summary>
    public static AuditEvent Append(
        string stream,
        long sequence,
        string previousHash,
        DateTimeOffset occurredAt,
        string actorType,
        string? actorId,
        string action,
        string? targetType,
        string? targetId,
        string result,
        string? ipHash,
        string? traceId,
        string? detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);

        // PostgreSQL timestamptz keeps microseconds; truncate so the stored
        // value hashes identically when the chain is verified later.
        occurredAt = TruncateToMicroseconds(occurredAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(result);

        var hash = ComputeHash(
            stream, sequence, previousHash, occurredAt, actorType, actorId, action, targetType, targetId, result);

        return new AuditEvent(
            Guid.CreateVersion7(occurredAt),
            stream,
            sequence,
            occurredAt,
            actorType,
            actorId,
            action,
            targetType,
            targetId,
            result,
            ipHash,
            traceId,
            detail,
            previousHash,
            hash);
    }

    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value) =>
        new(value.Ticks - (value.Ticks % 10), value.Offset);

    /// <summary>Recomputes the hash, used by chain verification.</summary>
    public string RecomputeHash() => ComputeHash(
        Stream, Sequence, PreviousHash, OccurredAt, ActorType, ActorId, Action, TargetType, TargetId, Result);

    public static string ComputeHash(
        string stream,
        long sequence,
        string previousHash,
        DateTimeOffset occurredAt,
        string actorType,
        string? actorId,
        string action,
        string? targetType,
        string? targetId,
        string result)
    {
        var payload = string.Join(
            '|',
            stream,
            sequence.ToString(CultureInfo.InvariantCulture),
            previousHash,
            occurredAt.ToString("O", CultureInfo.InvariantCulture),
            actorType,
            actorId ?? string.Empty,
            action,
            targetType ?? string.Empty,
            targetId ?? string.Empty,
            result);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public void MarkArchived(DateTimeOffset now) => ArchivedAt = now;
}

/// <summary>Tracks the tail of the hash chain for one stream.</summary>
public sealed class AuditChain
{
    private AuditChain()
    {
        Stream = string.Empty;
        LastHash = string.Empty;
    }

    private AuditChain(string stream, string genesis)
    {
        Stream = stream;
        Sequence = 0;
        LastHash = genesis;
    }

    public string Stream { get; private set; }

    public long Sequence { get; private set; }

    public string LastHash { get; private set; }

    public static AuditChain Start(string stream) => new(stream, new string('0', 64));

    public void Advance(long sequence, string hash)
    {
        Sequence = sequence;
        LastHash = hash;
    }

}
