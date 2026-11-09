using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nerves.Authorization.Domain;
using Nerves.Authorization.Persistence;
using Nerves.Authorization.Policy;

namespace Nerves.Authorization.Integration;

/// <summary>Loads and stores the PDP configuration the admin API edits.</summary>
public sealed class IntegrationSettingsStore(AuthorizationDbContext db)
{
    public const string PdpKey = "pdp";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ApplyAsync(PdpOptions pdp, CancellationToken cancellationToken)
    {
        var row = await db.IntegrationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == PdpKey, cancellationToken);

        if (row is null)
        {
            return;
        }

        try
        {
            var saved = JsonSerializer.Deserialize<PdpSettingsPayload>(row.PayloadJson, JsonOptions);
            if (saved is null)
            {
                return;
            }

            pdp.Url = saved.Url;
            pdp.Token = saved.Token;
            pdp.TimeoutSeconds = saved.TimeoutSeconds;
            pdp.FailOpen = saved.FailOpen;
        }
        catch (JsonException)
        {
            // Keep the configuration file values when the row is unreadable.
        }
    }

    public async Task SavePdpAsync(PdpOptions pdp, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            new PdpSettingsPayload(pdp.Url, pdp.Token, pdp.TimeoutSeconds, pdp.FailOpen), JsonOptions);

        var row = await db.IntegrationSettings.FirstOrDefaultAsync(x => x.Key == PdpKey, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (row is null)
        {
            db.IntegrationSettings.Add(IntegrationSetting.Create(PdpKey, payload, now));
        }
        else
        {
            row.Replace(payload, now);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record PdpSettingsPayload(string? Url, string? Token, int TimeoutSeconds, bool FailOpen);
}
