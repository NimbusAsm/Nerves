using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nerves.Identity.Domain;

namespace Nerves.Identity.Persistence.Configurations;

internal sealed class RoutingIndexEntryConfiguration : IEntityTypeConfiguration<RoutingIndexEntry>
{
    public void Configure(EntityTypeBuilder<RoutingIndexEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("routing_index");
        builder.HasKey(x => x.IdentifierHash);

        builder.Property(x => x.IdentifierHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.IdentifierType).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Realm).HasMaxLength(64).IsRequired();
        builder.Property(x => x.AccountId).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.AccountId);
        builder.HasIndex(x => x.Realm);
    }
}
