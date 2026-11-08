using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nerves.Idp.Domain;
using Nerves.Idp.Integration;
using Nerves.Idp.Options;
using Nerves.Idp.Security;

namespace Nerves.Idp.Api;

/// <summary>
/// JSON login for every client (products and the admin console). The caller
/// owns its login UI; this endpoint verifies credentials and returns a
/// single-use ticket that the caller passes to /connect/authorize.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/login", LoginAsync).RequireRateLimiting("login");

        return app;
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        IdentityDirectory directory,
        SessionDirectory sessions,
        AuditClient audit,
        LoginThrottle throttle,
        LoginTickets tickets,
        IOptions<IdentityClientOptions> options,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Problem("Username and password are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var realm = string.IsNullOrWhiteSpace(request.Realm) ? options.Value.DefaultRealm : request.Realm.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;

        if (throttle.IsLocked(request.Username, now))
        {
            return Results.Problem(
                "Too many failed attempts. Try again in 15 minutes.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var check = await directory.VerifyCredentialAsync(request.Username, request.Password, realm, cancellationToken);

        if (!check.Verified)
        {
            throttle.RecordFailure(request.Username, now);

            await audit.AppendAsync(
                "auth.login.failed",
                "anonymous",
                request.Username,
                string.Empty,
                "failure",
                "{\"reason\":\"invalid_credentials\"}",
                cancellationToken);

            // Uniform response: do not reveal whether the account exists.
            return Results.Json(
                new { error = "invalid_credentials", message = "Invalid username or password." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        throttle.Reset(request.Username);

        var deviceFingerprint = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(
                    (http.Request.Headers.UserAgent.ToString() + "|" + http.Connection.RemoteIpAddress)
                    .ToLowerInvariant()))).ToLowerInvariant();

        await sessions.CreateAsync(
            check.AccountId,
            check.LegalEntityId,
            realm,
            deviceFingerprint[..32],
            deviceFingerprint[..16],
            deviceFingerprint[16..32],
            cancellationToken);

        await audit.AppendAsync(
            "auth.login.succeeded",
            "account",
            check.AccountId,
            check.AccountId,
            "success",
            null,
            cancellationToken);

        var loginTicket = await tickets.IssueAsync(
            realm,
            Guid.Parse(check.AccountId),
            Guid.TryParse(check.LegalEntityId, out var entityId) ? entityId : null,
            string.IsNullOrWhiteSpace(check.DisplayName) ? request.Username : check.DisplayName,
            check.Email,
            cancellationToken);

        return Results.Ok(new
        {
            loginTicket,
            expiresInSeconds = (int)LoginTickets.Lifetime.TotalSeconds,
            user = new
            {
                id = check.AccountId,
                name = string.IsNullOrWhiteSpace(check.DisplayName) ? request.Username : check.DisplayName,
                email = check.Email,
                legalEntityId = check.LegalEntityId,
            },
        });
    }
}

/// <summary>Login payload.</summary>
public sealed record LoginRequest(string Username, string Password, string? Realm);
