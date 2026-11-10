using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.Extensions.Options;
using Nerves.Idp.Options;

namespace Nerves.Idp.Federation;

/// <summary>Reads entries from the configured LDAP directory.</summary>
public interface IDirectorySearch
{
    Task<IReadOnlyList<DirectoryEntry>> ListAsync(
        string baseDn,
        string filter,
        IReadOnlyCollection<string> attributes,
        CancellationToken cancellationToken);
}

/// <summary>
/// LDAP implementation on top of <see cref="LdapConnection"/>. A service bind
/// is required: the sync never reads with anonymous credentials.
/// </summary>
public sealed class LdapDirectorySearch(
    IOptionsMonitor<LdapOptions> ldapOptions,
    IOptions<DirectorySyncOptions> syncOptions) : IDirectorySearch
{
    public async Task<IReadOnlyList<DirectoryEntry>> ListAsync(
        string baseDn,
        string filter,
        IReadOnlyCollection<string> attributes,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDn);
        ArgumentException.ThrowIfNullOrWhiteSpace(filter);
        ArgumentNullException.ThrowIfNull(attributes);

        var settings = ldapOptions.CurrentValue;
        var pageSize = Math.Clamp(syncOptions.Value.PageSize, 1, 1000);

        return await Task.Run(() =>
        {
            using var connection = CreateConnection(settings);

            if (string.IsNullOrEmpty(settings.BindDn))
            {
                throw new InvalidOperationException("Ldap:BindDn is required for directory sync.");
            }

            connection.Bind(new NetworkCredential(settings.BindDn, settings.BindPassword));

            var request = new SearchRequest(baseDn, filter, SearchScope.Subtree, [.. attributes]);
            request.Controls.Add(new PageResultRequestControl(pageSize));

            var entries = new List<DirectoryEntry>();
            while (true)
            {
                var response = (SearchResponse)connection.SendRequest(request);

                foreach (SearchResultEntry entry in response.Entries)
                {
                    var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var attribute in attributes)
                    {
                        values[attribute] = entry.Attributes[attribute]?[0]?.ToString();
                    }

                    entries.Add(new DirectoryEntry(entry.DistinguishedName, values));
                }

                var cookie = response.Controls
                    .OfType<PageResultResponseControl>()
                    .FirstOrDefault()?.Cookie;

                if (cookie is null || cookie.Length == 0)
                {
                    break;
                }

                request.Controls.Clear();
                request.Controls.Add(new PageResultRequestControl(cookie));
            }

            return (IReadOnlyList<DirectoryEntry>)entries;
        }, cancellationToken);
    }

    private static LdapConnection CreateConnection(LdapOptions settings)
    {
        var identifier = new LdapDirectoryIdentifier(settings.Host, settings.Port);
        var connection = new LdapConnection(identifier)
        {
            AuthType = AuthType.Basic,
            Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
        };

        connection.SessionOptions.ProtocolVersion = 3;

        if (settings.UseSsl)
        {
            connection.SessionOptions.SecureSocketLayer = true;
        }

        return connection;
    }
}
