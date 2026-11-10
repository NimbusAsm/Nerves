using Microsoft.EntityFrameworkCore;
using Nerves.Audit.Persistence;
using Nerves.Authorization.Persistence;
using Nerves.ComplianceCn.Persistence;
using Nerves.Directory.Persistence;
using Nerves.Identity.Persistence;
using Nerves.Idp.Persistence;
using Nerves.Notification.Persistence;
using Nerves.Session.Persistence;

namespace Nerves.Cli.Infrastructure;

/// <summary>One service database the CLI can migrate or inspect.</summary>
public sealed record DatabaseTarget(string Name, string Schema, Func<string, DbContext> CreateContext);

/// <summary>
/// The eight schemas that make up a Nerves deployment. The CLI references the
/// service projects on purpose: "migrate" has to run the exact same migration
/// assemblies the services ship, not a copy of them.
/// </summary>
public static class DatabaseTargets
{
    public static IReadOnlyList<DatabaseTarget> All { get; } =
    [
        new("identity", IdentityDbContext.Schema, connectionString =>
            new IdentityDbContext(Options<IdentityDbContext>(connectionString))),
        new("session", SessionDbContext.Schema, connectionString =>
            new SessionDbContext(Options<SessionDbContext>(connectionString))),
        new("authorization", AuthorizationDbContext.Schema, connectionString =>
            new AuthorizationDbContext(Options<AuthorizationDbContext>(connectionString))),
        new("audit", AuditDbContext.Schema, connectionString =>
            new AuditDbContext(Options<AuditDbContext>(connectionString))),
        new("directory", DirectoryDbContext.Schema, connectionString =>
            new DirectoryDbContext(Options<DirectoryDbContext>(connectionString))),
        new("compliance", ComplianceDbContext.Schema, connectionString =>
            new ComplianceDbContext(Options<ComplianceDbContext>(connectionString))),
        new("notification", NotificationDbContext.Schema, connectionString =>
            new NotificationDbContext(Options<NotificationDbContext>(connectionString))),
        new("idp", IdpDbContext.Schema, connectionString =>
            new IdpDbContext(Options<IdpDbContext>(connectionString))),
    ];

    public static DbContextOptions<TContext> Options<TContext>(string connectionString)
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>().UseNpgsql(connectionString).Options;

    public static IReadOnlyList<DatabaseTarget> Select(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        if (names.Count == 0)
        {
            return All;
        }

        return All
            .Where(target => names.Contains(target.Name, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }
}
