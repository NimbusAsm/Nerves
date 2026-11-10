using System.Text.RegularExpressions;
using Nerves.Core;

namespace Nerves.Idp.Applications;

/// <summary>Validation rules shared by create and update.</summary>
public static partial class ApplicationValidation
{
    public const int MaxDisplayNameLength = 200;
    public const int MaxDescriptionLength = 500;

    [GeneratedRegex("^[a-z][a-z0-9-]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex ClientIdPattern();

    public static Result<Unit> ValidateClientId(string? clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return Invalid("client_id is required");
        }

        var trimmed = clientId.Trim();
        var valid = ClientIdPattern().IsMatch(trimmed)
                    && !trimmed.EndsWith('-');

        return valid
            ? Result.Success(new Unit())
            : Invalid("client_id must be 3-64 lowercase letters, digits or hyphens, start with a letter and not end with a hyphen");
    }

    public static Result<Unit> ValidateDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Invalid("displayName is required");
        }

        return displayName.Trim().Length <= MaxDisplayNameLength
            ? Result.Success(new Unit())
            : Invalid($"displayName must be at most {MaxDisplayNameLength} characters");
    }

    public static Result<Unit> ValidateDescription(string? description) =>
        (description ?? string.Empty).Length <= MaxDescriptionLength
            ? Result.Success(new Unit())
            : Invalid($"description must be at most {MaxDescriptionLength} characters");

    /// <summary>Redirect URIs must be absolute HTTP(S) URIs; HTTP is only for loopback.</summary>
    public static Result<Unit> ValidateRedirectUris(IReadOnlyList<string>? redirectUris, bool required)
    {
        var uris = NormalizeUris(redirectUris);

        if (uris.Count == 0)
        {
            return required ? Invalid("at least one redirect URI is required") : Result.Success(new Unit());
        }

        foreach (var uri in uris)
        {
            var result = ValidateUri(uri, "redirect URI");
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success(new Unit());
    }

    public static Result<Unit> ValidatePostLogoutUris(IReadOnlyList<string>? uris)
    {
        foreach (var uri in NormalizeUris(uris))
        {
            var result = ValidateUri(uri, "post-logout redirect URI");
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success(new Unit());
    }

    public static Result<Unit> ValidateScopes(IReadOnlyList<string>? scopes)
    {
        var normalized = ApplicationScopes.Normalize(scopes);

        if (normalized.Count == 0)
        {
            return Invalid("at least one scope is required");
        }

        var unknown = normalized.FirstOrDefault(scope => !ApplicationScopes.IsAllowed(scope));
        return unknown is null
            ? Result.Success(new Unit())
            : Invalid($"unknown scope '{unknown}'");
    }

    public static IReadOnlyList<string> NormalizeUris(IReadOnlyList<string>? uris) =>
        uris is null
            ? []
            : uris
                .Select(uri => (uri ?? string.Empty).Trim())
                .Where(uri => uri.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();

    private static Result<Unit> ValidateUri(string value, string label)
    {
        if (value.Contains('\r', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal))
        {
            return Invalid($"{label} must not contain line breaks");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return Invalid($"{label} '{value}' is not an absolute URI");
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return Result.Success(new Unit());
        }

        if (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)
        {
            return Result.Success(new Unit());
        }

        return Invalid($"{label} '{value}' must use https (http is only allowed for loopback)");
    }

    private static Result<Unit> Invalid(string message) => Result.Failure<Unit>(Reason.Validation(message));
}

/// <summary>Marker value for validation results.</summary>
public readonly record struct Unit;
