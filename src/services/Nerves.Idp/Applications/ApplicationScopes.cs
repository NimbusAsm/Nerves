using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Nerves.Idp.Applications;

/// <summary>
/// Translates the scopes an operator picks into the OpenIddict permission list,
/// keeping the same baseline as the development seeder: public client, code +
/// PKCE, refresh tokens, the three OIDC scopes and the API scope.
/// </summary>
public static class ApplicationScopes
{
    /// <summary>Scopes a registered application may request.</summary>
    public static readonly IReadOnlyList<string> Allowed =
    [
        "openid",
        "profile",
        "email",
        "roles",
        "nerves.api",
        "offline_access",
    ];

    /// <summary>Scopes that map to a `scp:` permission (openid is implicit).</summary>
    private static readonly HashSet<string> ScopePermissions = new(StringComparer.Ordinal)
    {
        "profile",
        "email",
        "roles",
        "nerves.api",
        "offline_access",
    };

    public static bool IsAllowed(string scope) =>
        !string.IsNullOrWhiteSpace(scope) && Allowed.Contains(scope, StringComparer.Ordinal);

    /// <summary>Normalises a scope list: trimmed, lower-cased, de-duplicated, ordered.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? scopes)
    {
        if (scopes is null)
        {
            return [];
        }

        return scopes
            .Select(scope => (scope ?? string.Empty).Trim().ToLowerInvariant())
            .Where(scope => scope.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(scope => scope, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Builds the OpenIddict permission list for the given scopes.</summary>
    public static IReadOnlyList<string> BuildPermissions(IEnumerable<string> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);

        var normalized = Normalize(scopes);
        var permissions = new List<string>
        {
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.Endpoints.EndSession,
            Permissions.GrantTypes.AuthorizationCode,
            // Refresh token support matches the seeder: the authorization code
            // flow always issues one when the client asks for offline_access.
            Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code,
        };

        foreach (var scope in normalized)
        {
            if (ScopePermissions.Contains(scope))
            {
                permissions.Add(Permissions.Prefixes.Scope + scope);
            }
        }

        return permissions;
    }

    /// <summary>Reads the scopes back out of a permission list.</summary>
    public static IReadOnlyList<string> ReadScopes(IEnumerable<string>? permissions)
    {
        if (permissions is null)
        {
            return [];
        }

        var scopes = new List<string> { "openid" };

        foreach (var permission in permissions)
        {
            if (permission.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal))
            {
                var scope = permission[Permissions.Prefixes.Scope.Length..];
                if (scope.Length > 0)
                {
                    scopes.Add(scope);
                }
            }
        }

        // Only report scopes the console knows about; anything else stays in the
        // permission list but is not offered as a checkbox.
        return scopes
            .Where(IsAllowed)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(scope => scope, StringComparer.Ordinal)
            .ToList();
    }
}
