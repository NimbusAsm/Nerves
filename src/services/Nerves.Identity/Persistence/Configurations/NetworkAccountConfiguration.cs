using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nerves.Identity.Domain;

namespace Nerves.Identity.Persistence.Configurations;

internal sealed class NetworkAccountConfiguration : IEntityTypeConfiguration<NetworkAccount>
{
    public void Configure(EntityTypeBuilder<NetworkAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("network_account");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Realm).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Username).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.PhoneNumber).HasMaxLength(32);
        builder.Property(x => x.DisplayName).HasMaxLength(200);
        builder.Property(x => x.Type).HasConversion<int>().IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        // Invariant I4: the login name is unique inside a realm, not globally.
        builder.HasIndex(x => new { x.Realm, x.Username }).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.PhoneNumber).IsUnique();
        builder.HasIndex(x => x.LegalEntityId);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
