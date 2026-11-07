using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nerves.Audit.Persistence;

/// <summary>Design-time factory for <c>dotnet ef</c>.</summary>
public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=nerves;Username=nerves;Password=nerves";

        return new AuditDbContext(
            new DbContextOptionsBuilder<AuditDbContext>().UseNpgsql(connectionString).Options);
    }
}
