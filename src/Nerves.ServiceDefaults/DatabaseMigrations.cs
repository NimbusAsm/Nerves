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
    public static async Task MigrateNervesDatabaseAsync<TContext>(this WebApplication app)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Configuration.GetValue("Database:MigrateOnStart", false))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetService<TContext>();
        if (context is not null)
        {
            await context.Database.MigrateAsync();
        }
    }
}
