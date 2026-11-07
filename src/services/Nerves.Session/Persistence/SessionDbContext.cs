using Microsoft.EntityFrameworkCore;

namespace Nerves.Session.Persistence;

/// <summary>Owns the <c>session</c> schema.</summary>
public sealed class SessionDbContext(DbContextOptions<SessionDbContext> options) : DbContext(options)
{
    public const string Schema = "session";

    public DbSet<Domain.Session> Sessions => Set<Domain.Session>();

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
    }
}
