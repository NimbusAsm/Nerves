using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nerves.Identity.Domain;

namespace Nerves.Identity.Persistence.Configurations;

internal sealed class LegalEntityConfiguration : IEntityTypeConfiguration<LegalEntity>
{
    public void Configure(EntityTypeBuilder<LegalEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("legal_entity");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).HasConversion<int>().IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.VerificationState).HasConversion<int>().IsRequired();
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.OwnsOne(x => x.RealName, realName =>
        {
            realName.Property(x => x.DocumentType)
                .HasColumnName("id_doc_type")
                .HasConversion<int>()
                .IsRequired();

            realName.Property(x => x.Hash)
                .HasColumnName("id_doc_hash")
                .HasMaxLength(64)
                .IsRequired();

            realName.Property(x => x.Country)
                .HasColumnName("id_doc_country")
                .HasMaxLength(2)
                .IsRequired();

            // Invariant I1: one legal entity per real-name identity.
            realName.HasIndex(x => x.Hash).IsUnique();
        });

        builder.HasIndex(x => x.VerificationState);
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
