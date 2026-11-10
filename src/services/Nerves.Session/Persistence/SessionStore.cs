using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nerves.Session.Integration;
using Nerves.Session.Options;
using Nerves.Sharding;

namespace Nerves.Session.Persistence;

/// <summary>
/// Routes session reads and writes across one or more databases. With sharding
/// disabled this is a thin wrapper over the single default database, so the
/// single-database deployment keeps the previous behaviour.
/// </summary>
public sealed class SessionStore : IDisposable
{
    private static readonly Action<ILogger, Guid, Exception?> EventFailed =
        LoggerMessage.Define<Guid>(LogLevel.Warning, new EventId(1, "OutboxEvent"), "Failed to dispatch session outbox event {EventId}");

    private readonly DbContextOptions<SessionDbContext>[] shards;
    private readonly ShardMap shardsById;
    private readonly ILogger<SessionStore> logger;

    public SessionStore(
        ShardConnectionResolver connections,
        IOptions<SessionStoreOptions> options,
        ILogger<SessionStore> logger)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
        var enabled = options.Value.Enabled;
        var count = enabled ? connections.ShardCount : 1;
        shardsById = new ShardMap(count);

        shards = new DbContextOptions<SessionDbContext>[count];
        for (var shard = 0; shard < count; shard++)
        {
            var connectionString = enabled
                ? connections.GetConnectionString(shard)
                : connections.GetConnectionString(0);

            shards[shard] = new DbContextOptionsBuilder<SessionDbContext>()
                .UseNpgsql(connectionString)
                .Options;
        }
    }

    public bool IsSharded => shards.Length > 1;

    public int ShardCount => shards.Length;

    public int ShardFor(Guid accountId) => shardsById.Resolve(accountId);

    public IReadOnlyList<DbContextOptions<SessionDbContext>> AllOptions() => shards;

    public async Task AddAsync(Domain.Session session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        await using var db = new SessionDbContext(shards[ShardFor(session.AccountId)]);
        db.Sessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Domain.Session>> ListAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var db = new SessionDbContext(shards[ShardFor(accountId)]);
        return await db.Sessions.AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Session ids do not encode a shard, so lookup fans out.</summary>
    public async Task<Domain.Session?> FindAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        foreach (var options in shards)
        {
            await using var db = new SessionDbContext(options);
            var session = await db.Sessions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
            if (session is not null)
            {
                return session;
            }
        }

        return null;
    }

    public async Task<bool> RevokeSessionAsync(Guid sessionId, string reason, CancellationToken cancellationToken)
    {
        foreach (var options in shards)
        {
            await using var db = new SessionDbContext(options);
            var session = await db.Sessions.FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
            if (session is null)
            {
                continue;
            }

            if (session.RevokedAt is not null)
            {
                return false;
            }

            var now = DateTimeOffset.UtcNow;
            session.Revoke(reason, now);
            db.OutboxEvents.Add(Domain.OutboxEvent.Create(
                "session.admin_revoked",
                "session",
                session.Id.ToString(),
                "{}",
                now));
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false;
    }

    public async Task<int> RevokeAccountAsync(Guid accountId, string reason, CancellationToken cancellationToken)
    {
        await using var db = new SessionDbContext(shards[ShardFor(accountId)]);
        var sessions = await db.Sessions
            .Where(x => x.AccountId == accountId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var session in sessions)
        {
            session.Revoke(reason, now);
        }

        if (sessions.Count > 0)
        {
            db.OutboxEvents.Add(Domain.OutboxEvent.Create(
                "session.account_revoked",
                "account",
                accountId.ToString(),
                "{\"revoked\":" + sessions.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}",
                now));
            await db.SaveChangesAsync(cancellationToken);
        }

        return sessions.Count;
    }

    /// <summary>Revokes expired sessions on every shard.</summary>
    public async Task<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var total = 0;
        foreach (var options in shards)
        {
            await using var db = new SessionDbContext(options);
            var expired = await db.Sessions
                .Where(x => x.RevokedAt == null && (x.IdleExpiresAt <= now || x.AbsoluteExpiresAt <= now))
                .Take(1000)
                .ToListAsync(cancellationToken);

            foreach (var session in expired)
            {
                session.Revoke("expired", now);
            }

            if (expired.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                total += expired.Count;
            }
        }

        return total;
    }

    /// <summary>Drains pending outbox rows from every shard.</summary>
    public async Task<int> DispatchOutboxAsync(
        IAuditPublisher publisher,
        CancellationToken cancellationToken)
    {
        var dispatched = 0;
        foreach (var options in shards)
        {
            await using var db = new SessionDbContext(options);
            var pending = await db.OutboxEvents
                .Where(x => x.DispatchedAt == null && x.Attempts < 10)
                .OrderBy(x => x.OccurredAt)
                .Take(100)
                .ToListAsync(cancellationToken);

            foreach (var item in pending)
            {
                try
                {
                    await publisher.AppendAsync(
                        item.Type,
                        "system",
                        null,
                        item.TargetType,
                        item.TargetId,
                        "success",
                        item.Payload,
                        cancellationToken);

                    item.MarkDispatched(DateTimeOffset.UtcNow);
                    dispatched++;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    item.MarkAttempt();
                    EventFailed(logger, item.Id, exception);
                }
            }

            if (pending.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return dispatched;
    }

    public void Dispose()
    {
    }
}
