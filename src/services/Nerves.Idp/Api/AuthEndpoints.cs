using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nerves.Idp.Integration;
using Nerves.Idp.Options;
using Nerves.Idp.Security;

namespace Nerves.Idp.Api;

/// <summary>
/// JSON login for every client (products and the admin console). The caller
/// owns its login UI; this endpoint verifies credentials and returns a
/// single-use ticket that the caller passes to /connect/authorize. An active
/// TOTP factor turns the first call into an MFA challenge.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/login", LoginAsync).RequireRateLimiting("login");
        group.MapPost("/mfa", MfaAsync).RequireRateLimiting("login");

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

        if (await directory.HasActiveTotpAsync(check.AccountId, cancellationToken))
        {
            var mfaTicket = await tickets.IssueAsync(
                realm,
                Guid.Parse(check.AccountId),
                TryEntity(check.LegalEntityId),
                DisplayName(check, request.Username),
                check.Email,
                "mfa",
                cancellationToken);

            return Results.Ok(new
            {
                mfaRequired = true,
                mfaTicket,
                expiresInSeconds = (int)LoginTickets.Lifetime.TotalSeconds,
            });
        }

        return await CompleteAsync(
            realm, check, request.Username, ["pwd"], tickets, sessions, audit, http, cancellationToken);
    }

    private static async Task<IResult> MfaAsync(
        [FromBody] MfaRequest request,
        IdentityDirectory directory,
        SessionDirectory sessions,
        AuditClient audit,
        LoginTickets tickets,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MfaTicket) || string.IsNullOrWhiteSpace(request.Code))
        {
            return Results.Problem("mfaTicket and code are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var ticket = await tickets.RedeemAsync(request.MfaTicket, "mfa", cancellationToken);
        if (ticket is null)
        {
            return Results.Json(
                new { error = "mfa_ticket_invalid", message = "The MFA challenge expired. Sign in again." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var accountId = ticket.AccountId.ToString();
        var (mfaValid, mfaMethod) = await directory.VerifyMfaAsync(accountId, request.Code.Trim(), cancellationToken);
        if (!mfaValid)
        {
            await audit.AppendAsync(
                "auth.mfa.failed",
                "account",
                accountId,
                accountId,
                "failure",
                null,
                cancellationToken);

            return Results.Json(
                new { error = "invalid_mfa_code", message = "Invalid verification code." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var check = new CredentialCheck(
            true,
            accountId,
            ticket.LegalEntityId?.ToString() ?? string.Empty,
            ticket.DisplayName,
            string.Empty,
            ticket.Email ?? string.Empty);

        return await CompleteAsync(
            ticket.Realm, check, ticket.DisplayName, ["pwd", mfaMethod], tickets, sessions, audit, http, cancellationToken);
    }

    private static async Task<IResult> CompleteAsync(
        string realm,
        CredentialCheck check,
        string fallbackName,
        string[] amr,
        LoginTickets tickets,
        SessionDirectory sessions,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var fingerprint = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(
                    (http.Request.Headers.UserAgent.ToString() + "|" + http.Connection.RemoteIpAddress)
                    .ToLowerInvariant()))).ToLowerInvariant();

        await sessions.CreateAsync(
            check.AccountId,
            check.LegalEntityId,
            realm,
            fingerprint[..32],
            fingerprint[..16],
            fingerprint[16..32],
            amr,
            cancellationToken);

        await audit.AppendAsync(
            "auth.login.succeeded",
            "account",
            check.AccountId,
            check.AccountId,
            "success",
            amr.Length > 1 ? "{\"amr\":\"pwd totp\"}" : null,
            cancellationToken);

        var loginTicket = await tickets.IssueAsync(
            realm,
            Guid.Parse(check.AccountId),
            TryEntity(check.LegalEntityId),
            DisplayName(check, fallbackName),
            check.Email,
            "login",
            cancellationToken);

        return Results.Ok(new
        {
            loginTicket,
            expiresInSeconds = (int)LoginTickets.Lifetime.TotalSeconds,
            user = new
            {
                id = check.AccountId,
                name = DisplayName(check, fallbackName),
                email = check.Email,
                legalEntityId = check.LegalEntityId,
            },
        });
    }

    private static Guid? TryEntity(string legalEntityId) =>
        Guid.TryParse(legalEntityId, out var id) ? id : null;

    private static string DisplayName(CredentialCheck check, string fallback) =>
        string.IsNullOrWhiteSpace(check.DisplayName) ? fallback : check.DisplayName;
}

/// <summary>Login payload.</summary>
public sealed record LoginRequest(string Username, string Password, string? Realm);

/// <summary>MFA challenge payload.</summary>
public sealed record MfaRequest(string MfaTicket, string Code);
