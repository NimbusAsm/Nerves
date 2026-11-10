using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Nerves.ServiceDefaults;

/// <summary>Applies pending migrations when the deployment opts in.</summary>
public static class DatabaseMigrations
{
    /// <summary>
    /// Runs migrations for the given context when <c>Database:MigrateOnStart</c>
    /// is true. Production should migrate from a dedicated job; the development
    /// compose stack enables this so a fresh volume is usable immediately.
    /// </summary>
    /// <returns>
    /// True when the process was started in migrate-only mode and the caller
    /// should exit without starting the web server (used by Kubernetes jobs).
    /// </returns>
    public static async Task<bool> MigrateNervesDatabaseAsync<TContext>(this WebApplication app)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(app);

        var migrateOnly = app.Configuration.GetValue("Database:MigrateOnly", false);
        var migrateOnStart = app.Configuration.GetValue("Database:MigrateOnStart", false);

        if (!migrateOnly && !migrateOnStart)
        {
            return false;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetService<TContext>();
        if (context is not null)
        {
            await context.Database.MigrateAsync();
        }

        return migrateOnly;
    }
}
