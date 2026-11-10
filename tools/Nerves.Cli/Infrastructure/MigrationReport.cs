namespace Nerves.Cli.Infrastructure;

/// <summary>Result of migrating (or inspecting) one service database.</summary>
public sealed record MigrationReport(
    string Service,
    string Schema,
    int Pending,
    bool Applied,
    bool Succeeded,
    string? Error,
    long ElapsedMilliseconds);
