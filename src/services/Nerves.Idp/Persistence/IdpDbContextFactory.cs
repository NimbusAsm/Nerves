using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nerves.Idp.Persistence;

/// <summary>Lets <c>dotnet ef</c> build the OpenIddict model without booting the host.</summary>
public sealed class IdpDbContextFactory : IDesignTimeDbContextFactory<IdpDbContext>
{
    public IdpDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=nerves;Username=nerves;Password=nerves";

        return new IdpDbContext(
            new DbContextOptionsBuilder<IdpDbContext>().UseNpgsql(connectionString).Options);
    }
}
