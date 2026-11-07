using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Domain;

namespace Nerves.Identity.Persistence;

/// <summary>
/// Owns the <c>identity</c> schema. Other services store the same identifiers
/// as soft references; no cross-schema foreign keys are allowed (PLAN §8).
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public const string Schema = "identity";

    public DbSet<LegalEntity> LegalEntities => Set<LegalEntity>();

    public DbSet<NetworkAccount> NetworkAccounts => Set<NetworkAccount>();

    public DbSet<AccountCredential> AccountCredentials => Set<AccountCredential>();

    public DbSet<RoutingIndexEntry> RoutingIndex => Set<RoutingIndexEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}
