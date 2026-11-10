using System.Text.Json;

namespace Nerves.ConsoleApi.Aggregation;

/// <summary>One downstream service as seen by the console.</summary>
public sealed record ServiceStatus(
    string Name,
    bool Reachable,
    string? Version,
    long ElapsedMilliseconds,
    string? Error);

/// <summary>
/// Answers <c>GET /api/v1/console/overview</c>. Upstream payloads are passed
/// through unchanged so the console never becomes a second API contract.
/// </summary>
public sealed record ConsoleOverviewResponse(
    DateTimeOffset GeneratedAt,
    JsonElement? Identity,
    JsonElement? Authorization,
    JsonElement? Compliance,
    IReadOnlyList<ServiceStatus> Services,
    IReadOnlyList<string> Warnings);

/// <summary>Answers <c>GET /api/v1/console/account/{id}</c>.</summary>
public sealed record ConsoleAccountResponse(
    Guid AccountId,
    JsonElement? Account,
    JsonElement? Sessions,
    JsonElement? AuditEvents,
    IReadOnlyList<string> Warnings);

/// <summary>Answers <c>GET /api/v1/console/entity/{id}</c>.</summary>
public sealed record ConsoleEntityResponse(
    Guid EntityId,
    JsonElement? LegalEntity,
    IReadOnlyList<JsonElement> Accounts,
    bool AccountsTruncated,
    IReadOnlyList<string> Warnings);
