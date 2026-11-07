using Microsoft.EntityFrameworkCore;

namespace Nerves.Idp.Persistence;

/// <summary>Stores OpenIddict applications, scopes, tokens and authorizations.</summary>
public sealed class IdpDbContext(DbContextOptions<IdpDbContext> options) : DbContext(options)
{
    public const string Schema = "idp";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.UseOpenIddict();
    }
}
