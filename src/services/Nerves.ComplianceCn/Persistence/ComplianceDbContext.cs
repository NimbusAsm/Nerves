using Microsoft.EntityFrameworkCore;
using Nerves.ComplianceCn.Domain;

namespace Nerves.ComplianceCn.Persistence;

/// <summary>Owns the <c>compliance</c> schema.</summary>
public sealed class ComplianceDbContext(DbContextOptions<ComplianceDbContext> options) : DbContext(options)
{
    public const string Schema = "compliance";

    public DbSet<Consent> Consents => Set<Consent>();

    public DbSet<RetentionPolicy> RetentionPolicies => Set<RetentionPolicy>();

    public DbSet<ErasureRequest> ErasureRequests => Set<ErasureRequest>();

    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Consent>(builder =>
        {
            builder.ToTable("consent");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.SubjectId).HasMaxLength(128).IsRequired();
            builder.Property(x => x.Purpose).HasMaxLength(128).IsRequired();
            builder.Property(x => x.Version).HasMaxLength(64).IsRequired();
            builder.HasIndex(x => new { x.SubjectId, x.Purpose });
            builder.Ignore(x => x.IsActive);
        });

        modelBuilder.Entity<ErasureRequest>(builder =>
        {
            builder.ToTable("erasure_request");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.SubjectId).HasMaxLength(128).IsRequired();
            builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
            builder.HasIndex(x => x.SubjectId);
        });

        modelBuilder.Entity<RetentionPolicy>(builder =>
        {
            builder.ToTable("retention_policy");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Category).HasMaxLength(128).IsRequired();
            builder.HasIndex(x => x.Category).IsUnique();
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
