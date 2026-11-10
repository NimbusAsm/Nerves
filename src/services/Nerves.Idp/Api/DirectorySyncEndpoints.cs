using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Nerves.Idp.Federation;
using Nerves.Idp.Options;
using OpenIddict.Validation.AspNetCore;

namespace Nerves.Idp.Api;

/// <summary>
/// Read-only status of the directory provisioning worker. Operators use it to
/// confirm that the last pass ran and what it changed; secrets never appear.
/// </summary>
public static class DirectorySyncEndpoints
{
    public static IEndpointRouteBuilder MapDirectorySyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/directory-sync/status", GetStatusAsync);
        return app;
    }

    private static async Task<IResult> GetStatusAsync(
        DirectorySyncState state,
        IOptions<DirectorySyncOptions> options,
        IOptions<LdapOptions> ldap,
        HttpContext http)
    {
        var authentication = await http.AuthenticateAsync(
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);

        if (!authentication.Succeeded || authentication.Principal is null)
        {
            return Results.Unauthorized();
        }

        var permissions = authentication.Principal.FindFirst("permissions")?.Value ?? string.Empty;
        if (!permissions.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("nerves:admin"))
        {
            return Results.Forbid();
        }

        var sync = options.Value;
        var snapshot = state.Current;

        return Results.Ok(new
        {
            enabled = sync.Enabled && ldap.Value.Enabled,
            intervalMinutes = sync.IntervalMinutes,
            realm = sync.Realm,
            baseDn = sync.ResolveBaseDn(ldap.Value.BaseDn),
            filter = sync.ResolveFilter(),
            attributeMap = new
            {
                username = sync.AttributeMap.Username,
                email = sync.AttributeMap.Email,
                displayName = sync.AttributeMap.DisplayName,
            },
            lastRun = new
            {
                startedAt = snapshot.LastStartedAt,
                completedAt = snapshot.LastCompletedAt,
                durationSeconds = snapshot.Duration?.TotalSeconds,
                running = snapshot.Running,
                error = snapshot.LastError,
            },
            counters = new
            {
                scanned = snapshot.Scanned,
                created = snapshot.Created,
                updated = snapshot.Updated,
                unchanged = snapshot.Unchanged,
                skipped = snapshot.Skipped,
                failed = snapshot.Failed,
            },
        });
    }
}
