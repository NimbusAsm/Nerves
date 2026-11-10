using System.Text.Json;
using Nerves.Core;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Nerves.Idp.Applications;

/// <summary>
/// Reads and writes OpenIddict applications through the manager so the IdP,
/// the console and the seeded clients all share one source of truth.
/// </summary>
public sealed class ApplicationService(
    IOpenIddictApplicationManager manager,
    IClock clock)
{
    /// <summary>Loads one page of applications, newest first.</summary>
    public async Task<Result<ApplicationPageResponse>> ListAsync(
        int limit,
        string? cursor,
        string? query,
        CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 100);

        if (!ApplicationCursor.TryDecode(cursor, out var offset, out var cursorError))
        {
            return Result.Failure<ApplicationPageResponse>(Reason.Validation(cursorError!));
        }

        var keyword = (query ?? string.Empty).Trim();
        var descriptors = new List<OpenIddictApplicationDescriptor>();

        if (keyword.Length == 0)
        {
            // `ListAsync` takes (count, offset); passing them the other way round
            // returns an empty page.
            await foreach (var application in manager.ListAsync(limit + 1, offset, cancellationToken))
            {
                var descriptor = new OpenIddictApplicationDescriptor();
                await manager.PopulateAsync(descriptor, application, cancellationToken);
                descriptors.Add(descriptor);
            }
        }
        else
        {
            // There is no queryable search over the manager API, and an account
            // centre has tens of applications, so filter the whole set in memory
            // and page the result.
            var all = new List<OpenIddictApplicationDescriptor>();
            await foreach (var application in manager.ListAsync(count: null, offset: null, cancellationToken))
            {
                var descriptor = new OpenIddictApplicationDescriptor();
                await manager.PopulateAsync(descriptor, application, cancellationToken);
                all.Add(descriptor);
            }

            var matches = all
                .Where(descriptor => Matches(descriptor, keyword))
                .OrderByDescending(descriptor => ApplicationMetadata.ReadTimestamp(
                    descriptor.Properties, ApplicationMetadata.CreatedAtKey))
                .ThenBy(descriptor => descriptor.ClientId, StringComparer.Ordinal)
                .Skip(offset)
                .Take(limit + 1)
                .ToList();

            descriptors = matches;
        }

        var hasMore = descriptors.Count > limit;
        if (hasMore)
        {
            descriptors = descriptors.Take(limit).ToList();
        }

        var items = descriptors
            .Select(ToResponse)
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.ClientId, StringComparer.Ordinal)
            .ToList();

        return Result.Success(new ApplicationPageResponse(items, hasMore ? ApplicationCursor.Encode(offset + limit) : null));
    }

    private static bool Matches(OpenIddictApplicationDescriptor descriptor, string keyword)
    {
        var haystacks = new[]
        {
            descriptor.ClientId,
            descriptor.DisplayName,
            descriptor.Properties.ReadString(ApplicationMetadata.DescriptionKey),
        };

        return haystacks.Any(value =>
            value is not null && value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Result<ApplicationResponse>> GetAsync(string id, CancellationToken cancellationToken)
    {
        var application = await manager.FindByIdAsync(id, cancellationToken)
            ?? await manager.FindByClientIdAsync(id, cancellationToken);

        if (application is null)
        {
            return Result.Failure<ApplicationResponse>(Reason.NotFound($"application '{id}' was not found"));
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, application, cancellationToken);
        return Result.Success(ToResponse(descriptor));
    }

    public async Task<Result<ApplicationResponse>> CreateAsync(
        CreateApplicationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var clientId = (request.ClientId ?? string.Empty).Trim().ToLowerInvariant();
        var displayName = (request.DisplayName ?? string.Empty).Trim();
        var description = (request.Description ?? string.Empty).Trim();
        var redirectUris = ApplicationValidation.NormalizeUris(request.RedirectUris);
        var postLogoutUris = ApplicationValidation.NormalizeUris(request.PostLogoutRedirectUris);
        var scopes = ApplicationScopes.Normalize(request.Scopes);

        foreach (var validation in new[]
                 {
                     ApplicationValidation.ValidateClientId(clientId),
                     ApplicationValidation.ValidateDisplayName(displayName),
                     ApplicationValidation.ValidateDescription(description),
                     ApplicationValidation.ValidateRedirectUris(redirectUris, required: true),
                     ApplicationValidation.ValidatePostLogoutUris(postLogoutUris),
                     ApplicationValidation.ValidateScopes(scopes),
                 })
        {
            if (validation.IsFailure)
            {
                return Result.Failure<ApplicationResponse>(validation.Reason);
            }
        }

        if (await manager.FindByClientIdAsync(clientId, cancellationToken) is not null)
        {
            return Result.Failure<ApplicationResponse>(
                Reason.Conflict($"client_id '{clientId}' is already registered"));
        }

        var now = clock.UtcNow;
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = displayName,
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };

        descriptor.SetString(ApplicationMetadata.DescriptionKey, description);
        descriptor.SetString(ApplicationMetadata.StatusKey, (request.Enabled ?? true) ? "active" : "disabled");
        descriptor.SetString(ApplicationMetadata.CreatedAtKey, ApplicationMetadata.FormatTimestamp(now));
        descriptor.SetString(ApplicationMetadata.UpdatedAtKey, ApplicationMetadata.FormatTimestamp(now));

        if (!string.IsNullOrWhiteSpace(request.LogoUrl))
        {
            descriptor.SetString(ApplicationMetadata.LogoKey, request.LogoUrl.Trim());
        }

        ApplyUris(descriptor, redirectUris, postLogoutUris);
        ApplyPermissions(descriptor, scopes);

        await manager.CreateAsync(descriptor, cancellationToken);

        return Result.Success(ToResponse(descriptor));
    }

    public async Task<Result<ApplicationResponse>> UpdateAsync(
        string id,
        UpdateApplicationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var application = await manager.FindByIdAsync(id, cancellationToken)
            ?? await manager.FindByClientIdAsync(id, cancellationToken);

        if (application is null)
        {
            return Result.Failure<ApplicationResponse>(Reason.NotFound($"application '{id}' was not found"));
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, application, cancellationToken);

        if (request.DisplayName is not null)
        {
            var displayName = request.DisplayName.Trim();
            var validation = ApplicationValidation.ValidateDisplayName(displayName);
            if (validation.IsFailure)
            {
                return Result.Failure<ApplicationResponse>(validation.Reason);
            }

            descriptor.DisplayName = displayName;
        }

        if (request.Description is not null)
        {
            var description = request.Description.Trim();
            var validation = ApplicationValidation.ValidateDescription(description);
            if (validation.IsFailure)
            {
                return Result.Failure<ApplicationResponse>(validation.Reason);
            }

            descriptor.SetString(ApplicationMetadata.DescriptionKey, description);
        }

        if (request.LogoUrl is not null)
        {
            descriptor.SetString(ApplicationMetadata.LogoKey, request.LogoUrl.Trim());
        }

        if (request.RedirectUris is not null)
        {
            var redirectUris = ApplicationValidation.NormalizeUris(request.RedirectUris);
            var validation = ApplicationValidation.ValidateRedirectUris(redirectUris, required: true);
            if (validation.IsFailure)
            {
                return Result.Failure<ApplicationResponse>(validation.Reason);
            }

            descriptor.RedirectUris.Clear();
            ApplyUris(descriptor, redirectUris, []);
        }

        if (request.PostLogoutRedirectUris is not null)
        {
            var postLogoutUris = ApplicationValidation.NormalizeUris(request.PostLogoutRedirectUris);
            var validation = ApplicationValidation.ValidatePostLogoutUris(postLogoutUris);
            if (validation.IsFailure)
            {
                return Result.Failure<ApplicationResponse>(validation.Reason);
            }

            descriptor.PostLogoutRedirectUris.Clear();
            ApplyUris(descriptor, [], postLogoutUris);
        }

        if (request.Scopes is not null)
        {
            var scopes = ApplicationScopes.Normalize(request.Scopes);
            var validation = ApplicationValidation.ValidateScopes(scopes);
            if (validation.IsFailure)
            {
                return Result.Failure<ApplicationResponse>(validation.Reason);
            }

            ApplyPermissions(descriptor, scopes);
        }

        if (request.Enabled is not null)
        {
            descriptor.SetString(ApplicationMetadata.StatusKey, request.Enabled.Value ? "active" : "disabled");
        }

        descriptor.SetString(ApplicationMetadata.UpdatedAtKey, ApplicationMetadata.FormatTimestamp(clock.UtcNow));

        await manager.UpdateAsync(application, descriptor, cancellationToken);

        return Result.Success(ToResponse(descriptor));
    }

    public async Task<Result<DeleteApplicationResponse>> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var application = await manager.FindByIdAsync(id, cancellationToken)
            ?? await manager.FindByClientIdAsync(id, cancellationToken);

        if (application is null)
        {
            return Result.Failure<DeleteApplicationResponse>(Reason.NotFound($"application '{id}' was not found"));
        }

        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, application, cancellationToken);

        if (ApplicationMetadata.IsBuiltIn(descriptor.ClientId))
        {
            return Result.Failure<DeleteApplicationResponse>(
                Reason.Conflict($"'{descriptor.ClientId}' is a built-in client and cannot be deleted"));
        }

        await manager.DeleteAsync(application, cancellationToken);

        return Result.Success(new DeleteApplicationResponse(
            descriptor.ClientId ?? id,
            descriptor.ClientId ?? string.Empty,
            descriptor.DisplayName ?? string.Empty));
    }

    /// <summary>True when the client exists and has been disabled by an operator.</summary>
    public async Task<bool> IsDisabledAsync(string? clientId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return false;
        }

        var application = await manager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return false;
        }

        var properties = await manager.GetPropertiesAsync(application, cancellationToken);
        return ApplicationMetadata.ReadStatus(properties) == ApplicationStatus.Disabled;
    }

    private static void ApplyUris(
        OpenIddictApplicationDescriptor descriptor,
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> postLogoutUris)
    {
        foreach (var uri in redirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(uri));
        }

        foreach (var uri in postLogoutUris)
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
        }
    }

    private static void ApplyPermissions(OpenIddictApplicationDescriptor descriptor, IReadOnlyList<string> scopes)
    {
        descriptor.Permissions.Clear();

        foreach (var permission in ApplicationScopes.BuildPermissions(scopes))
        {
            descriptor.Permissions.Add(permission);
        }
    }

    private static ApplicationResponse ToResponse(OpenIddictApplicationDescriptor descriptor)
    {
        var clientId = descriptor.ClientId ?? string.Empty;
        var status = ApplicationMetadata.ReadStatus(descriptor);
        var createdAt = ApplicationMetadata.ReadTimestamp(descriptor.Properties, ApplicationMetadata.CreatedAtKey);
        var updatedAt = ApplicationMetadata.ReadTimestamp(descriptor.Properties, ApplicationMetadata.UpdatedAtKey);
        var description = descriptor.Properties.ReadString(ApplicationMetadata.DescriptionKey) ?? string.Empty;
        var logo = descriptor.Properties.ReadString(ApplicationMetadata.LogoKey);

        return new ApplicationResponse(
            clientId,
            clientId,
            descriptor.DisplayName ?? clientId,
            description,
            string.IsNullOrWhiteSpace(logo) ? null : logo,
            descriptor.RedirectUris.Select(uri => uri.ToString()).ToList(),
            descriptor.PostLogoutRedirectUris.Select(uri => uri.ToString()).ToList(),
            ApplicationScopes.ReadScopes(descriptor.Permissions),
            status.ToWire(),
            ApplicationMetadata.IsBuiltIn(clientId),
            createdAt,
            updatedAt);
    }
}
