using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nerves.Idp.Domain;
using Nerves.Idp.Federation;
using Nerves.Idp.Persistence;

namespace Nerves.Idp.Integration;

/// <summary>Loads and stores integration settings that the admin API edits.</summary>
public sealed class IntegrationSettingsStore(IdpDbContext db)
{
    public const string LdapKey = "ldap";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ApplyAsync(LdapOptions ldap, CancellationToken cancellationToken)
    {
        var row = await db.IntegrationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == LdapKey, cancellationToken);

        if (row is null)
        {
            return;
        }

        try
        {
            var saved = JsonSerializer.Deserialize<LdapSettingsPayload>(row.PayloadJson, JsonOptions);
            if (saved is null)
            {
                return;
            }

            ldap.Enabled = saved.Enabled;
            ldap.Host = saved.Host;
            ldap.Port = saved.Port;
            ldap.UseSsl = saved.UseSsl;
            ldap.BaseDn = saved.BaseDn;
            ldap.UserFilter = saved.UserFilter;
            ldap.TimeoutSeconds = saved.TimeoutSeconds;
        }
        catch (JsonException)
        {
            // A corrupt row must not stop the service; the file config stays.
        }
    }

    public async Task SaveLdapAsync(LdapOptions ldap, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new LdapSettingsPayload(
            ldap.Enabled, ldap.Host, ldap.Port, ldap.UseSsl, ldap.BaseDn, ldap.UserFilter, ldap.TimeoutSeconds), JsonOptions);

        var row = await db.IntegrationSettings.FirstOrDefaultAsync(x => x.Key == LdapKey, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (row is null)
        {
            db.IntegrationSettings.Add(IntegrationSetting.Create(LdapKey, payload, now));
        }
        else
        {
            row.Replace(payload, now);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record LdapSettingsPayload(
        bool Enabled,
        string Host,
        int Port,
        bool UseSsl,
        string BaseDn,
        string UserFilter,
        int TimeoutSeconds);
}
