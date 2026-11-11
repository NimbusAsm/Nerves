using Microsoft.EntityFrameworkCore;
using Nerves.Core;
using Nerves.Identity.Domain;

namespace Nerves.Identity.Persistence;

/// <summary>Filters shared by the account list endpoint and the stats counters.</summary>
public sealed record AccountFilter(
    string? Realm = null,
    AccountStatus? Status = null,
    string? Query = null,
    Guid? LegalEntityId = null);

/// <summary>
/// Routes identity reads and writes across the global database and the account
/// shards. Everything account scoped goes through <see cref="AccountShard"/>;
/// legal entities, routing entries and verification attempts live in
/// <see cref="Global"/>. With sharding disabled both return the same database.
/// </summary>
public sealed class IdentityStore(IdentityDataPlane plane, ILogger<IdentityStore> logger)
{
    private static readonly Action<ILogger, Guid, Exception?> CompensationFailed =
        LoggerMessage.Define<Guid>(LogLevel.Error, new EventId(1, "AccountCreateCompensation"),
            "failed to compensate account {AccountId} after a routing index failure");

    public IdentityDataPlane Plane { get; } = plane;

    public bool IsSharded => Plane.IsSharded;

    public int ShardCount => Plane.ShardCount;

    public int ShardFor(Guid accountId) => Plane.ShardFor(accountId);

    /// <summary>Global database: legal entities, routing index, verification attempts.</summary>
    public IdentityDbContext Global() => Plane.CreateGlobal();

    /// <summary>Shard that owns the account: accounts, credentials, passkey challenges, reset tokens, outbox.</summary>
    public IdentityDbContext AccountShard(Guid accountId) => Plane.CreateShardFor(accountId);

    /// <summary>
    /// Creates an account: the account row and its credential go to the owning
    /// shard first, then the routing index is written to the global database.
    /// A failed index write is compensated by deleting the shard rows again, so
    /// no account can exist without a routing entry.
    /// </summary>
    public async Task<Result> CreateAccountAsync(
        NetworkAccount account,
        AccountCredential? credential,
        IReadOnlyList<RoutingIndexEntry> indexes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(indexes);

        await using (var shard = AccountShard(account.Id))
        {
            shard.NetworkAccounts.Add(account);
            if (credential is not null)
            {
                shard.AccountCredentials.Add(credential);
            }

            await shard.SaveChangesAsync(cancellationToken);
        }

        try
        {
            await using var global = Global();
            global.RoutingIndex.AddRange(indexes);
            await global.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException)
        {
            await CompensateAsync(account.Id, cancellationToken);
            return Result.Failure(Reason.Conflict("an identifier is already in use"));
        }
    }

    /// <summary>Removes the shard rows written before a failed routing index insert.</summary>
    public async Task CompensateAsync(Guid accountId, CancellationToken cancellationToken)
    {
        try
        {
            await using var shard = AccountShard(accountId);
            var account = await shard.NetworkAccounts.FirstOrDefaultAsync(x => x.Id == accountId, cancellationToken);
            if (account is not null)
            {
                shard.NetworkAccounts.Remove(account);
            }

            var credentials = await shard.AccountCredentials
                .Where(x => x.AccountId == accountId)
                .ToListAsync(cancellationToken);
            shard.AccountCredentials.RemoveRange(credentials);

            await shard.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The routing index is the login authority: without it the orphan
            // account is unreachable, so a failed cleanup is logged, not fatal.
            CompensationFailed(logger, accountId, exception);
        }
    }

    /// <summary>Queues a domain event in the database that matches its scope.</summary>
    public async Task AddEventAsync(OutboxEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await using var db = domainEvent.AccountId is null
            ? Global()
            : AccountShard(domainEvent.AccountId.Value);

        db.OutboxEvents.Add(domainEvent);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Fan-out account listing with the same filters the API exposes.</summary>
    public async Task<List<NetworkAccount>> ListAccountsAsync(
        AccountFilter filter,
        int limit,
        Guid? cursor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var collected = new List<NetworkAccount>(limit + 1);
        foreach (var options in Plane.Shards())
        {
            await using var shard = new IdentityDbContext(options);
            var query = ApplyFilter(shard.NetworkAccounts.AsNoTracking(), filter);

            if (cursor is not null)
            {
                query = query.Where(x => x.Id.CompareTo(cursor.Value) > 0);
            }

            collected.AddRange(await query.OrderBy(x => x.Id).Take(limit + 1).ToListAsync(cancellationToken));
        }

        return collected
            .OrderBy(x => x.Id)
            .Take(limit + 1)
            .ToList();
    }

    /// <summary>Counts accounts across every shard.</summary>
    public async Task<int> CountAccountsAsync(AccountFilter? filter, CancellationToken cancellationToken)
    {
        var total = 0;
        foreach (var options in Plane.Shards())
        {
            await using var shard = new IdentityDbContext(options);
            var query = filter is null
                ? shard.NetworkAccounts.AsNoTracking()
                : ApplyFilter(shard.NetworkAccounts.AsNoTracking(), filter);
            total += await query.CountAsync(cancellationToken);
        }

        return total;
    }

    /// <summary>Finds an account by user name across shards (directory and SCIM provisioning).</summary>
    public async Task<NetworkAccount?> FindByUsernameAsync(
        string realm,
        string username,
        CancellationToken cancellationToken)
    {
        foreach (var options in Plane.Shards())
        {
            await using var shard = new IdentityDbContext(options);
            var account = await shard.NetworkAccounts.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Realm == realm && x.Username == username, cancellationToken);
            if (account is not null)
            {
                return account;
            }
        }

        return null;
    }

    /// <summary>Finds an account by email across shards; email is deployment-wide unique.</summary>
    public async Task<NetworkAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        foreach (var options in Plane.Shards())
        {
            await using var shard = new IdentityDbContext(options);
            var account = await shard.NetworkAccounts.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
            if (account is not null)
            {
                return account;
            }
        }

        return null;
    }

    /// <summary>Accounts owned by one legal entity, gathered from every shard.</summary>
    public async Task<List<NetworkAccount>> ListAccountsForEntityAsync(
        Guid legalEntityId,
        CancellationToken cancellationToken)
    {
        var collected = new List<NetworkAccount>();
        foreach (var options in Plane.Shards())
        {
            await using var shard = new IdentityDbContext(options);
            collected.AddRange(await shard.NetworkAccounts.AsNoTracking()
                .Where(x => x.LegalEntityId == legalEntityId)
                .ToListAsync(cancellationToken));
        }

        return collected;
    }

    /// <summary>SUM of stats scoped to the account tables.</summary>
    public async Task<int> CountAccountsByStatusAsync(AccountStatus status, CancellationToken cancellationToken)
    {
        var total = 0;
        foreach (var options in Plane.Shards())
        {
            await using var shard = new IdentityDbContext(options);
            total += await shard.NetworkAccounts.AsNoTracking().CountAsync(x => x.Status == status, cancellationToken);
        }

        return total;
    }

    /// <summary>Runs the same read against every shard and concatenates the results.</summary>
    public async Task<List<T>> QueryShardsAsync<T>(
        Func<IdentityDbContext, Task<List<T>>> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var results = new List<T>();
        foreach (var options in Plane.Shards())
        {
            await using var shard = new IdentityDbContext(options);
            results.AddRange(await query(shard));
        }

        return results;
    }

    /// <summary>Runs a mutation against every shard (outbox dispatch, bulk disable).</summary>
    public async Task<List<T>> MutateShardsAsync<T>(
        Func<IdentityDbContext, Task<T>> mutation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        var results = new List<T>();
        foreach (var options in Plane.Shards())
        {
            await using var shard = new IdentityDbContext(options);
            results.Add(await mutation(shard));
        }

        return results;
    }

    private static IQueryable<NetworkAccount> ApplyFilter(
        IQueryable<NetworkAccount> query,
        AccountFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Realm))
        {
            query = query.Where(x => x.Realm == filter.Realm);
        }

        if (filter.Status is not null)
        {
            query = query.Where(x => x.Status == filter.Status);
        }

        if (filter.LegalEntityId is not null)
        {
            query = query.Where(x => x.LegalEntityId == filter.LegalEntityId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            query = query.Where(x => EF.Functions.ILike(x.Username, "%" + filter.Query + "%"));
        }

        return query;
    }
}
