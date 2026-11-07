using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nerves.Directory.Persistence;

/// <summary>Design-time factory for <c>dotnet ef</c>.</summary>
public sealed class DirectoryDbContextFactory : IDesignTimeDbContextFactory<DirectoryDbContext>
{
    public DirectoryDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=nerves;Username=nerves;Password=nerves";

        return new DirectoryDbContext(
            new DbContextOptionsBuilder<DirectoryDbContext>().UseNpgsql(connectionString).Options);
    }
}
