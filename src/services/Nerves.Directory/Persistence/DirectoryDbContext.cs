using Microsoft.EntityFrameworkCore;
using Nerves.Directory.Domain;

namespace Nerves.Directory.Persistence;

/// <summary>Owns the <c>directory</c> schema: organizations, members and invitations.</summary>
public sealed class DirectoryDbContext(DbContextOptions<DirectoryDbContext> options) : DbContext(options)
{
    public const string Schema = "directory";

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Organization>(builder =>
        {
            builder.ToTable("organization");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Realm).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Slug).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(1000);
            builder.HasIndex(x => new { x.Realm, x.Slug }).IsUnique();
            builder.HasIndex(x => x.ParentId);
            builder.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<Membership>(builder =>
        {
            builder.ToTable("membership");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Role).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Status).HasConversion<int>().IsRequired();
            builder.HasIndex(x => new { x.OrganizationId, x.AccountId }).IsUnique();
            builder.HasIndex(x => x.AccountId);
        });

        modelBuilder.Entity<Invitation>(builder =>
        {
            builder.ToTable("invitation");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.EmailHash).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Role).HasMaxLength(64).IsRequired();
            builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            builder.HasIndex(x => x.TokenHash).IsUnique();
            builder.HasIndex(x => x.OrganizationId);
        });

        modelBuilder.Entity<OutboxEvent>(builder =>
        {
            builder.ToTable("outbox_event");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Type).HasMaxLength(128).IsRequired();
            builder.Property(x => x.TargetType).HasMaxLength(64);
            builder.Property(x => x.TargetId).HasMaxLength(64);
            builder.Property(x => x.Payload).HasColumnType("text").IsRequired();
            builder.HasIndex(x => new { x.DispatchedAt, x.OccurredAt });
        });
    }
}
