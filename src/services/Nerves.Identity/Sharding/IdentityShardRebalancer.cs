using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Persistence;

namespace Nerves.Identity.Sharding;

/// <summary>Result of an identity shard rebalance pass.</summary>
public sealed record IdentityRebalanceReport(int Scanned, int Moved, bool DryRun);

/// <summary>
/// Moves account-scoped rows from a previous shard topology to the new one:
/// accounts, credentials, passkey challenges and password reset tokens. Outbox
/// rows stay in place and are dispatched from whichever shard they live in, so
/// old databases must remain configured until their outbox is drained.
/// </summary>
public sealed class IdentityShardRebalancer(
    IConfiguration configuration,
    IdentityDataPlane plane,
    ILogger<IdentityShardRebalancer> logger)
{
    private static readonly Action<ILogger, int, int, bool, Exception?> Reported =
        LoggerMessage.Define<int, int, bool>(
            LogLevel.Information,
            new EventId(1, "IdentityRebalance"),
            "identity shard rebalance: scanned={Scanned} moved={Moved} dryRun={DryRun}");

    public async Task<IdentityRebalanceReport> RunAsync(bool dryRun, CancellationToken cancellationToken)
    {
        var previous = PreviousShards(configuration);
        var scanned = 0;
        var moved = 0;

        foreach (var source in previous)
        {
            await using var sourceDb = new IdentityDbContext(source);
            var accounts = await sourceDb.NetworkAccounts.AsNoTracking()
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            foreach (var account in accounts)
            {
                var targetOptions = plane.Shards()[plane.ShardFor(account.Id)];
                if (string.Equals(
                        ConnectionString(targetOptions),
                        ConnectionString(source),
                        StringComparison.Ordinal))
                {
                    continue;
                }

                scanned++;
                if (dryRun)
                {
                    moved++;
                    continue;
                }

                var credentials = await sourceDb.AccountCredentials.AsNoTracking()
                    .Where(x => x.AccountId == account.Id)
                    .ToListAsync(cancellationToken);
                var challenges = await sourceDb.PasskeyChallenges.AsNoTracking()
                    .Where(x => x.AccountId == account.Id)
                    .ToListAsync(cancellationToken);
                var resetTokens = await sourceDb.PasswordResetTokens.AsNoTracking()
                    .Where(x => x.AccountId == account.Id)
                    .ToListAsync(cancellationToken);

                await using (var targetDb = new IdentityDbContext(targetOptions))
                {
                    if (!await targetDb.NetworkAccounts.AnyAsync(x => x.Id == account.Id, cancellationToken))
                    {
                        targetDb.NetworkAccounts.Add(account);
                        targetDb.AccountCredentials.AddRange(credentials);
                        targetDb.PasskeyChallenges.AddRange(challenges);
                        targetDb.PasswordResetTokens.AddRange(resetTokens);
                        await targetDb.SaveChangesAsync(cancellationToken);
                    }
                }

                await using (var deleteDb = new IdentityDbContext(source))
                {
                    await deleteDb.Database.ExecuteSqlInterpolatedAsync(
                        $"delete from identity.\"password_reset_token\" where \"AccountId\" = {account.Id}", cancellationToken);
                    await deleteDb.Database.ExecuteSqlInterpolatedAsync(
                        $"delete from identity.\"PasskeyChallenges\" where \"AccountId\" = {account.Id}", cancellationToken);
                    await deleteDb.Database.ExecuteSqlInterpolatedAsync(
                        $"delete from identity.\"account_credential\" where \"AccountId\" = {account.Id}", cancellationToken);
                    await deleteDb.Database.ExecuteSqlInterpolatedAsync(
                        $"delete from identity.\"network_account\" where \"Id\" = {account.Id}", cancellationToken);
                }

                moved++;
            }
        }

        Reported(logger, scanned, moved, dryRun, null);

        return new IdentityRebalanceReport(scanned, moved, dryRun);
    }

    private static string? ConnectionString(DbContextOptions<IdentityDbContext> options)
    {
        using var db = new IdentityDbContext(options);
        return db.Database.GetConnectionString();
    }

    internal static IReadOnlyList<DbContextOptions<IdentityDbContext>> PreviousShards(IConfiguration configuration)
    {
        var count = configuration.GetValue("Sharding:PreviousShardCount", 0);
        if (count < 1)
        {
            throw new InvalidOperationException(
                "Sharding:PreviousShardCount must be at least 1 for a rebalance run.");
        }

        var result = new DbContextOptions<IdentityDbContext>[count];
        for (var index = 0; index < count; index++)
        {
            var connection = configuration[$"Sharding:PreviousConnectionStrings:{index}"];
            if (string.IsNullOrWhiteSpace(connection))
            {
                throw new InvalidOperationException(
                    $"Sharding:PreviousConnectionStrings:{index} is required for a rebalance run.");
            }

            result[index] = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseNpgsql(connection)
                .Options;
        }

        return result;
    }
}
