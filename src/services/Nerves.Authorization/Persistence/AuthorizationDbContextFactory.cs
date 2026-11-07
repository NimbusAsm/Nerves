using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nerves.Authorization.Persistence;

/// <summary>Design-time factory for <c>dotnet ef</c>.</summary>
public sealed class AuthorizationDbContextFactory : IDesignTimeDbContextFactory<AuthorizationDbContext>
{
    public AuthorizationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=nerves;Username=nerves;Password=nerves";

        return new AuthorizationDbContext(
            new DbContextOptionsBuilder<AuthorizationDbContext>().UseNpgsql(connectionString).Options);
    }
}
