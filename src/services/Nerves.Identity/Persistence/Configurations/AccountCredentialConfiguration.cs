using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nerves.Identity.Domain;

namespace Nerves.Identity.Persistence.Configurations;

internal sealed class AccountCredentialConfiguration : IEntityTypeConfiguration<AccountCredential>
{
    public void Configure(EntityTypeBuilder<AccountCredential> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("account_credential");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AccountId).IsRequired();
        builder.Property(x => x.Type).HasConversion<int>().IsRequired();
        builder.Property(x => x.Secret).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Algorithm).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => x.AccountId);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
