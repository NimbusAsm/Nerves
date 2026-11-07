using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nerves.Session.Persistence;

/// <summary>Design-time factory for <c>dotnet ef</c>.</summary>
public sealed class SessionDbContextFactory : IDesignTimeDbContextFactory<SessionDbContext>
{
    public SessionDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=nerves;Username=nerves;Password=nerves";

        return new SessionDbContext(
            new DbContextOptionsBuilder<SessionDbContext>().UseNpgsql(connectionString).Options);
    }
}
