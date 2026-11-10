using Microsoft.EntityFrameworkCore;

namespace Nerves.Session.Persistence;

/// <summary>Owns the <c>session</c> schema.</summary>
public sealed class SessionDbContext(DbContextOptions<SessionDbContext> options) : DbContext(options)
{
    public const string Schema = "session";

    public DbSet<Domain.Session> Sessions => Set<Domain.Session>();

    public DbSet<Domain.OutboxEvent> OutboxEvents => Set<Domain.OutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Domain.Session>(builder =>
        {
            builder.ToTable("session");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Realm).HasMaxLength(64).IsRequired();
            builder.Property(x => x.DeviceId).HasMaxLength(128).IsRequired();
            builder.Property(x => x.IpHash).HasMaxLength(64).IsRequired();
            builder.Property(x => x.UserAgentHash).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Amr).HasMaxLength(64).IsRequired();
            builder.Property(x => x.RevokedReason).HasMaxLength(200);
            builder.HasIndex(x => new { x.AccountId, x.RevokedAt });
            builder.HasIndex(x => x.AbsoluteExpiresAt);
            builder.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<Domain.OutboxEvent>(builder =>
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
