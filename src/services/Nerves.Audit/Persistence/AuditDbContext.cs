using Microsoft.EntityFrameworkCore;
using Nerves.Audit.Domain;

namespace Nerves.Audit.Persistence;

/// <summary>Owns the <c>audit</c> schema: append-only events and chain tails.</summary>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public const string Schema = "audit";

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<AuditChain> AuditChains => Set<AuditChain>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<AuditEvent>(builder =>
        {
            builder.ToTable("audit_event");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Stream).HasMaxLength(64).IsRequired();
            builder.Property(x => x.ActorType).HasMaxLength(32).IsRequired();
            builder.Property(x => x.ActorId).HasMaxLength(64);
            builder.Property(x => x.Action).HasMaxLength(128).IsRequired();
            builder.Property(x => x.TargetType).HasMaxLength(64);
            builder.Property(x => x.TargetId).HasMaxLength(64);
            builder.Property(x => x.Result).HasMaxLength(32).IsRequired();
            builder.Property(x => x.IpHash).HasMaxLength(64);
            builder.Property(x => x.TraceId).HasMaxLength(64);
            builder.Property(x => x.Detail).HasColumnType("jsonb");
            builder.Property(x => x.PreviousHash).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Hash).HasMaxLength(64).IsRequired();

            builder.HasIndex(x => new { x.Stream, x.Sequence }).IsUnique();
            builder.HasIndex(x => x.OccurredAt);
            builder.HasIndex(x => x.ActorId);
            builder.HasIndex(x => x.Action);
        });

        modelBuilder.Entity<AuditChain>(builder =>
        {
            builder.ToTable("audit_chain");
            builder.HasKey(x => x.Stream);
            builder.Property(x => x.Stream).HasMaxLength(64);
            builder.Property(x => x.LastHash).HasMaxLength(64).IsRequired();
        });
    }
}
