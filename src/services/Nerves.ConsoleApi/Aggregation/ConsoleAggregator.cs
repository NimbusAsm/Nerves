using System.Globalization;
using System.Text.Json;
using Nerves.ConsoleApi.Options;

namespace Nerves.ConsoleApi.Aggregation;

/// <summary>
/// Fans out one console request into the services that own the data and merges
/// the answers. Calls run concurrently; every failure becomes a warning, and a
/// missing primary resource (404 from the owning service) becomes null so the
/// endpoint can answer 404.
/// </summary>
public sealed class ConsoleAggregator(IDownstreamClient downstream, ConsoleOptions options)
{
    public async Task<ConsoleOverviewResponse> OverviewAsync(CancellationToken cancellationToken)
    {
        var identityTask = downstream.GetAsync("identity", "/api/v1/stats", cancellationToken);
        var authorizationTask = downstream.GetAsync("authorization", "/api/v1/decisions/stats", cancellationToken);
        var complianceTask = downstream.GetAsync("compliance", "/api/v1/compliance/summary", cancellationToken);
        var probes = options.Services.Keys
            .Select(name => ProbeAsync(name, cancellationToken))
            .ToArray();

        var identity = await identityTask.ConfigureAwait(false);
        var authorization = await authorizationTask.ConfigureAwait(false);
        var compliance = await complianceTask.ConfigureAwait(false);
        var services = await Task.WhenAll(probes).ConfigureAwait(false);

        var warnings = new List<string>();
        AddWarning(warnings, identity);
        AddWarning(warnings, authorization);
        AddWarning(warnings, compliance);

        return new ConsoleOverviewResponse(
            DateTimeOffset.UtcNow,
            identity.Body,
            authorization.Body,
            compliance.Body,
            services,
            warnings);
    }

    public async Task<ConsoleAccountResponse?> AccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var id = accountId.ToString("D", CultureInfo.InvariantCulture);
        var auditLimit = options.MaxAuditEvents.ToString(CultureInfo.InvariantCulture);

        var accountTask = downstream.GetAsync("identity", $"/api/v1/accounts/{id}", cancellationToken);
        var sessionsTask = downstream.GetAsync("session", $"/api/v1/accounts/{id}/sessions", cancellationToken);
        var auditTask = downstream.GetAsync(
            "audit",
            $"/api/v1/audit-events?actorId={id}&limit={auditLimit}",
            cancellationToken);

        var account = await accountTask.ConfigureAwait(false);
        var sessions = await sessionsTask.ConfigureAwait(false);
        var audit = await auditTask.ConfigureAwait(false);

        if (account.IsNotFound)
        {
            return null;
        }

        var warnings = new List<string>();
        AddWarning(warnings, account);
        AddWarning(warnings, sessions);
        AddWarning(warnings, audit);

        return new ConsoleAccountResponse(accountId, account.Body, sessions.Body, audit.Body, warnings);
    }

    public async Task<ConsoleEntityResponse?> EntityAsync(Guid entityId, CancellationToken cancellationToken)
    {
        var id = entityId.ToString("D", CultureInfo.InvariantCulture);
        var limit = options.AccountScanLimit.ToString(CultureInfo.InvariantCulture);

        var entityTask = downstream.GetAsync("identity", $"/api/v1/legal-entities/{id}", cancellationToken);
        var accountsTask = downstream.GetAsync("identity", $"/api/v1/accounts?limit={limit}", cancellationToken);

        var entity = await entityTask.ConfigureAwait(false);
        var accounts = await accountsTask.ConfigureAwait(false);

        if (entity.IsNotFound)
        {
            return null;
        }

        var warnings = new List<string>();
        AddWarning(warnings, entity);

        var matches = new List<JsonElement>();
        var truncated = false;

        if (accounts.Body is { ValueKind: JsonValueKind.Object } page
            && page.TryGetProperty("items", out var items)
            && items.ValueKind == JsonValueKind.Array)
        {
            var scanned = items.GetArrayLength();
            truncated = scanned >= options.AccountScanLimit;

            foreach (var item in items.EnumerateArray())
            {
                if (BelongsTo(item, id))
                {
                    matches.Add(item);
                }
            }

            if (truncated)
            {
                warnings.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"identity returned the first {scanned} accounts; the entity view stops there (raise Console:AccountScanLimit to widen it)"));
            }
        }
        else
        {
            AddWarning(warnings, accounts);
        }

        return new ConsoleEntityResponse(entityId, entity.Body, matches, truncated, warnings);
    }

    private async Task<ServiceStatus> ProbeAsync(string name, CancellationToken cancellationToken)
    {
        var meta = await downstream.GetAsync(name, "/api/v1/meta", cancellationToken).ConfigureAwait(false);
        return new ServiceStatus(
            name,
            meta.IsSuccess,
            Version(meta.Body),
            meta.ElapsedMilliseconds,
            meta.Warning);
    }

    private static string? Version(JsonElement? body) =>
        body is { ValueKind: JsonValueKind.Object } element
        && element.TryGetProperty("version", out var version)
        && version.ValueKind == JsonValueKind.String
            ? version.GetString()
            : null;

    private static bool BelongsTo(JsonElement account, string legalEntityId) =>
        account.ValueKind == JsonValueKind.Object
        && account.TryGetProperty("legalEntityId", out var value)
        && value.ValueKind == JsonValueKind.String
        && string.Equals(value.GetString(), legalEntityId, StringComparison.OrdinalIgnoreCase);

    private static void AddWarning(List<string> warnings, DownstreamResult result)
    {
        if (result.Warning is not null)
        {
            warnings.Add(result.Warning);
        }
    }
}
