using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nerves.Identity.Domain;

namespace Nerves.Identity.Persistence.Configurations;

internal sealed class VerificationAttemptConfiguration : IEntityTypeConfiguration<VerificationAttempt>
{
    public void Configure(EntityTypeBuilder<VerificationAttempt> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("verification_attempt");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LegalEntityId).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(200);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.Message).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.LegalEntityId);
        builder.HasIndex(x => new { x.Provider, x.Reference });

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
