using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Nerves.Core;
using Nerves.Idp.Applications;
using Nerves.Idp.Integration;
using OpenIddict.Validation.AspNetCore;

namespace Nerves.Idp.Api;

/// <summary>
/// Runtime management of OIDC applications. Everything goes through the
/// OpenIddict manager so registered clients take effect without a restart, and
/// every write emits an audit event.
/// </summary>
public static class ApplicationEndpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/applications");

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/{id}", GetAsync);
        group.MapPatch("/{id}", UpdateAsync);
        group.MapDelete("/{id}", DeleteAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(
        ApplicationService applications,
        HttpContext http,
        int limit = 50,
        string? cursor = null,
        string? q = null,
        CancellationToken cancellationToken = default)
    {
        if (await GuardAsync(http) is { } denied)
        {
            return denied;
        }

        var result = await applications.ListAsync(limit, cursor, q, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Reason);
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] CreateApplicationRequest request,
        ApplicationService applications,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (await GuardAsync(http) is { } denied)
        {
            return denied;
        }

        var result = await applications.CreateAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            await audit.AppendApplicationAsync(
                "application.created",
                result.Value.ClientId,
                $"{{\"clientId\":\"{result.Value.ClientId}\",\"scopes\":{ScopeJson(result.Value.Scopes)}}}",
                cancellationToken);
        }

        return result.IsSuccess
            ? Results.Created($"/api/v1/admin/applications/{result.Value.Id}", result.Value)
            : ToProblem(result.Reason);
    }

    private static async Task<IResult> GetAsync(
        string id,
        ApplicationService applications,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (await GuardAsync(http) is { } denied)
        {
            return denied;
        }

        var result = await applications.GetAsync(id, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Reason);
    }

    private static async Task<IResult> UpdateAsync(
        string id,
        [FromBody] UpdateApplicationRequest request,
        ApplicationService applications,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (await GuardAsync(http) is { } denied)
        {
            return denied;
        }

        var result = await applications.UpdateAsync(id, request, cancellationToken);

        if (result.IsSuccess)
        {
            await audit.AppendApplicationAsync(
                "application.updated",
                result.Value.ClientId,
                $"{{\"clientId\":\"{result.Value.ClientId}\",\"status\":\"{result.Value.Status}\",\"scopes\":{ScopeJson(result.Value.Scopes)}}}",
                cancellationToken);
        }

        return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Reason);
    }

    private static async Task<IResult> DeleteAsync(
        string id,
        ApplicationService applications,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (await GuardAsync(http) is { } denied)
        {
            return denied;
        }

        var result = await applications.DeleteAsync(id, cancellationToken);

        if (result.IsSuccess)
        {
            await audit.AppendApplicationAsync(
                "application.deleted",
                result.Value.ClientId,
                $"{{\"clientId\":\"{result.Value.ClientId}\"}}",
                cancellationToken);
        }

        return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Reason);
    }

    /// <summary>
    /// Returns null when the caller may proceed. The IdP's default scheme is a
    /// cookie, so `Results.Unauthorized`/`Forbid` would redirect; APIs must answer
    /// 401/403 instead.
    /// </summary>
    private static async Task<IResult?> GuardAsync(HttpContext http)
    {
        var authentication = await http.AuthenticateAsync(
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);

        if (!authentication.Succeeded || authentication.Principal is null)
        {
            return Results.Problem("authentication is required", statusCode: StatusCodes.Status401Unauthorized);
        }

        var permissions = authentication.Principal.FindFirst("permissions")?.Value ?? string.Empty;
        var isAdmin = permissions
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains("nerves:admin", StringComparer.Ordinal);

        return isAdmin
            ? null
            : Results.Problem("the nerves:admin permission is required", statusCode: StatusCodes.Status403Forbidden);
    }

    private static string ScopeJson(IReadOnlyList<string> scopes) =>
        "[" + string.Join(",", scopes.Select(scope => $"\"{scope}\"")) + "]";

    private static IResult ToProblem(Reason reason)
    {
        var status = reason.Code switch
        {
            "not_found" => StatusCodes.Status404NotFound,
            "conflict" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return Results.Problem(reason.Message, statusCode: status);
    }
}
