using Microsoft.EntityFrameworkCore;
using Nerves.Notification.Domain;

namespace Nerves.Notification.Persistence;

/// <summary>Owns the <c>notification</c> schema: the email delivery log.</summary>
public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public const string Schema = "notification";

    public DbSet<NotificationMessage> Messages => Set<NotificationMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<NotificationMessage>(builder =>
        {
            builder.ToTable("notification_message");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.To).HasMaxLength(320).IsRequired();
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

            builder.Property<uint>("xmin").IsRowVersion();
        });
    }
}
