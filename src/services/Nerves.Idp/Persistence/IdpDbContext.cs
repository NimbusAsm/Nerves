using Microsoft.EntityFrameworkCore;
using Nerves.Idp.Domain;

namespace Nerves.Idp.Persistence;

/// <summary>Stores OpenIddict state plus single-use login tickets.</summary>
public sealed class IdpDbContext(DbContextOptions<IdpDbContext> options) : DbContext(options)
{
    public const string Schema = "idp";

    public DbSet<LoginTicket> LoginTickets => Set<LoginTicket>();

    public DbSet<Nerves.Idp.Federation.FederationState> FederationStates => Set<Nerves.Idp.Federation.FederationState>();

    public DbSet<Nerves.Idp.Domain.IntegrationSetting> IntegrationSettings => Set<Nerves.Idp.Domain.IntegrationSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.UseOpenIddict();

        modelBuilder.Entity<Nerves.Idp.Domain.IntegrationSetting>(builder =>
        {
            builder.ToTable("integration_setting");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Key).HasMaxLength(64).IsRequired();
            builder.Property(x => x.PayloadJson).HasColumnType("text").IsRequired();
            builder.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<Nerves.Idp.Federation.FederationState>(builder =>
        {
            builder.ToTable("federation_state");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Provider).HasMaxLength(64).IsRequired();
            builder.Property(x => x.ReturnUrl).HasMaxLength(512);
            builder.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<LoginTicket>(builder =>
        {
            builder.ToTable("login_ticket");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Realm).HasMaxLength(64).IsRequired();
            builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Email).HasMaxLength(320);
            builder.HasIndex(x => x.TokenHash).IsUnique();
            builder.HasIndex(x => x.ExpiresAt);
        });
    }
}
