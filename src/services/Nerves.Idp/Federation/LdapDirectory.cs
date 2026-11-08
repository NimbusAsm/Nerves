using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.Extensions.Options;

namespace Nerves.Idp.Federation;

/// <summary>Configuration for one LDAP directory that can authenticate users.</summary>
public sealed class LdapOptions
{
    public const string SectionName = "Ldap";

    public bool Enabled { get; set; }

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 389;

    public bool UseSsl { get; set; }

    /// <summary>Service account used to search for the user entry.</summary>
    public string? BindDn { get; set; }

    public string? BindPassword { get; set; }

    public string BaseDn { get; set; } = "dc=example,dc=com";

    /// <summary>Search filter with <c>{0}</c> replaced by the escaped user name.</summary>
    public string UserFilter { get; set; } = "(uid={0})";

    /// <summary>Attribute that carries the mail address, if any.</summary>
    public string MailAttribute { get; set; } = "mail";

    public int TimeoutSeconds { get; set; } = 5;
}

/// <summary>
/// Minimal LDAP authenticator: look the user up with a service bind (when
/// configured) and then re-bind as that DN. Group-based authorization stays in
/// Nerves; LDAP is only a credential source.
/// </summary>
public sealed class LdapDirectory(IOptionsMonitor<LdapOptions> options)
{
    public async Task<LdapIdentity?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        return await Task.Run(() =>
        {
            using var connection = CreateConnection(settings);

            if (!string.IsNullOrEmpty(settings.BindDn))
            {
                connection.Bind(new NetworkCredential(settings.BindDn, settings.BindPassword));
            }

            var filter = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                settings.UserFilter,
                Escape(username));

            var request = new SearchRequest(
                settings.BaseDn,
                filter,
                SearchScope.Subtree,
                "dn",
                settings.MailAttribute,
                "cn");

            var response = (SearchResponse)connection.SendRequest(request);
            if (response.Entries.Count != 1)
            {
                return null;
            }

            var entry = response.Entries[0];
            var dn = entry.DistinguishedName;
            var mail = entry.Attributes[settings.MailAttribute]?[0]?.ToString();
            var name = entry.Attributes["cn"]?[0]?.ToString();

            try
            {
                connection.Bind(new NetworkCredential(dn, password));
            }
            catch (LdapException)
            {
                return null;
            }

            return new LdapIdentity(dn, mail, name);
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

    private static string Escape(string value) =>
        value.Replace("\\", "\\5c", StringComparison.Ordinal)
            .Replace("*", "\\2a", StringComparison.Ordinal)
            .Replace("(", "\\28", StringComparison.Ordinal)
            .Replace(")", "\\29", StringComparison.Ordinal)
            .Replace("\0", "\\00", StringComparison.Ordinal);
}

/// <summary>The directory entry that authenticated.</summary>
public sealed record LdapIdentity(string DistinguishedName, string? Mail, string? CommonName);
