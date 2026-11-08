using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nerves.Authorization.Integration;
using Nerves.Authorization.Policy;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Authorization.Api;

/// <summary>Read-only deployment facts for the admin console.</summary>
public static class AdminStatusEndpoints
{
    public static RouteGroupBuilder MapAdminStatusEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin")
            .RequireAuthorization(BearerAuthentication.AdminPolicy);

        group.MapPatch("/pdp", PatchPdpAsync);

        group.MapGet("/pdp", (IOptions<PdpOptions> options) =>
        {
            var pdp = options.Value;

            return Results.Ok(new
            {
                enabled = !string.IsNullOrWhiteSpace(pdp.Url),
                url = pdp.Url,
                timeoutSeconds = pdp.TimeoutSeconds,
                failOpen = pdp.FailOpen,
                tokenConfigured = !string.IsNullOrWhiteSpace(pdp.Token),
            });
        });

        return group;
    }

    private static async Task<IResult> PatchPdpAsync(
        [FromBody] PdpSettingsPatch patch,
        IOptions<PdpOptions> options,
        AuthorizationAuditClient audit,
        CancellationToken cancellationToken)
    {
        var current = options.Value;

        if (patch.Url is not null)
        {
            current.Url = string.IsNullOrWhiteSpace(patch.Url) ? null : patch.Url.Trim();
        }

        if (patch.TimeoutSeconds is > 0 and <= 30) current.TimeoutSeconds = patch.TimeoutSeconds.Value;
        if (patch.FailOpen is not null) current.FailOpen = patch.FailOpen.Value;
        if (patch.Token is not null)
        {
            current.Token = string.IsNullOrWhiteSpace(patch.Token) ? null : patch.Token;
        }

        // An empty URL disables the external PDP and falls back to local checks.
        if (patch.Enabled is not null)
        {
            if (!patch.Enabled.Value)
            {
                current.Url = null;
            }
            else if (string.IsNullOrWhiteSpace(current.Url))
            {
                return Results.Problem("url is required when enabling the PDP.", statusCode: StatusCodes.Status400BadRequest);
            }
        }

        await audit.AppendAsync(
            "admin.integration.pdp.updated",
            "admin",
            "integration",
            "pdp",
            "{\"enabled\":" + (string.IsNullOrWhiteSpace(current.Url) ? "false" : "true") + "}",
            cancellationToken);

        return Results.Ok(new
        {
            enabled = !string.IsNullOrWhiteSpace(current.Url),
            url = current.Url,
            timeoutSeconds = current.TimeoutSeconds,
            failOpen = current.FailOpen,
            tokenConfigured = !string.IsNullOrWhiteSpace(current.Token),
            note = "The change applies to this process only and is lost on restart.",
        });
    }
}

/// <summary>Partial PDP update; omitted fields stay unchanged.</summary>
public sealed record PdpSettingsPatch(string? Url, int? TimeoutSeconds, bool? FailOpen, bool? Enabled, string? Token);
