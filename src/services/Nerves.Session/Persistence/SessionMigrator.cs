using Microsoft.EntityFrameworkCore;

namespace Nerves.Session.Persistence;

/// <summary>Applies the session migrations to every configured shard.</summary>
public sealed class SessionMigrator(SessionStore store)
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);

        foreach (var options in store.AllOptions())
        {
            await using var db = new SessionDbContext(options);
            await db.Database.MigrateAsync(cancellationToken);
        }
    }
}
