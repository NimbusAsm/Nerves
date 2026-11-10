using Nerves.Idp.Options;

namespace Nerves.Idp.Federation;

/// <summary>One directory entry and the attributes the sync asked for.</summary>
public sealed record DirectoryEntry(string DistinguishedName, IReadOnlyDictionary<string, string?> Attributes);

/// <summary>Normalized form of a directory entry that can be provisioned.</summary>
public sealed record DirectoryAccountRecord(
    string Realm,
    string Username,
    string? Email,
    string? DisplayName,
    string DirectoryId);

/// <summary>Maps raw LDAP attributes to the provisioning contract.</summary>
public static class DirectoryEntryMapper
{
    /// <summary>
    /// Returns <see langword="null"/> when the entry has no username: such an
    /// entry cannot become an account, so the caller counts it as skipped.
    /// </summary>
    public static DirectoryAccountRecord? Map(
        DirectoryEntry entry,
        string realm,
        DirectoryAttributeMap attributeMap)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(attributeMap);

        var username = Read(entry, attributeMap.Username);
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        return new DirectoryAccountRecord(
            realm,
            username,
            Read(entry, attributeMap.Email),
            Read(entry, attributeMap.DisplayName),
            entry.DistinguishedName);
    }

    private static string? Read(DirectoryEntry entry, string attribute) =>
        entry.Attributes.TryGetValue(attribute, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
}
