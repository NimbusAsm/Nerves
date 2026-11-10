using Microsoft.EntityFrameworkCore;
using Nerves.Notification.Domain;

namespace Nerves.Notification.Persistence;

/// <summary>Owns the <c>notification</c> schema: the email delivery log.</summary>
public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public const string Schema = "notification";

    public DbSet<NotificationMessage> Messages => Set<NotificationMessage>();

    public DbSet<EventConsumerState> EventConsumerStates => Set<EventConsumerState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<NotificationMessage>(builder =>
        {
            builder.ToTable("notification_message");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Channel)
                .HasConversion<int>()
                .IsRequired()
                .HasDefaultValue(NotificationChannel.Email);
            builder.Property(x => x.To)
                .HasMaxLength(NotificationMessage.MaxRecipientLength)
                .IsRequired();
            builder.Property(x => x.Template).HasMaxLength(128).IsRequired();
            builder.Property(x => x.Subject).HasMaxLength(300).IsRequired();
            builder.Property(x => x.Body).IsRequired();
            builder.Property(x => x.Status).HasConversion<int>().IsRequired();
            builder.Property(x => x.Attempts).IsRequired();
            builder.Property(x => x.Error).HasMaxLength(NotificationMessage.MaxErrorLength);
            builder.Property(x => x.CreatedAt).IsRequired();
            builder.Property(x => x.NextAttemptAt).IsRequired();

            // The worker filters on status + due time; the list API filters by status
            // and orders by created time.
            builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
            builder.HasIndex(x => x.CreatedAt);
            builder.HasIndex(x => x.To);
            builder.HasIndex(x => x.Channel);

            // Idempotency for the event consumer: one message per identity event.
            // PostgreSQL allows several NULLs in a unique index, so messages queued
            // by the management API are unaffected.
            builder.HasIndex(x => x.SourceEventId).IsUnique();

            builder.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<EventConsumerState>(builder =>
        {
            builder.ToTable("event_consumer_state");
            builder.HasKey(x => x.Consumer);
            builder.Property(x => x.Consumer).HasMaxLength(64).IsRequired();
            builder.Property(x => x.LastOccurredAt).IsRequired();
            builder.Property(x => x.UpdatedAt).IsRequired();
        });
    }
}
