using Nerves.Idp.Integration;

namespace Nerves.Idp.Api;

/// <summary>
/// The login API lives here, so the self-service reset is mounted here too and
/// delegated to the identity service. Responses are deliberately uniform: the
/// caller cannot tell whether an identifier maps to an account.
/// </summary>
public static class PasswordResetEndpoints
{
    private const string GenericMessage = "If the account exists, a reset link has been sent.";

    public static IEndpointRouteBuilder MapPasswordResetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth/password-reset");

        group.MapPost("/request", RequestAsync).RequireRateLimiting("password-reset");
        group.MapPost("/confirm", ConfirmAsync).RequireRateLimiting("password-reset");

        return app;
    }

    private static async Task<IResult> RequestAsync(
        PasswordResetRequest request,
        PasswordResetDirectory directory,
        IConfiguration configuration,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier) || request.Identifier.Length > 320)
        {
            return Results.Problem("identifier is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Prefer the explicitly configured public URL (the admin console or the
        // product that owns the reset page); fall back to the request host.
        var configured = configuration["PasswordReset:PublicBaseUrl"];
        var baseUrl = !string.IsNullOrWhiteSpace(request.PublicBaseUrl)
            ? request.PublicBaseUrl.Trim()
            : !string.IsNullOrWhiteSpace(configured)
                ? configured.Trim()
                : http.Request.Scheme + "://" + http.Request.Host;

        await directory.RequestAsync(request.Identifier, request.Realm, baseUrl, cancellationToken);

        return Results.Accepted(value: new { message = GenericMessage });
    }

    private static async Task<IResult> ConfirmAsync(
        PasswordResetConfirmRequest request,
        PasswordResetDirectory directory,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 512)
        {
            return Results.Problem(
                "The reset token is invalid or has expired.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await directory.CompleteAsync(request.Token, request.NewPassword, cancellationToken);
        if (!result.Reset)
        {
            await audit.AppendAsync(
                "auth.password-reset.failed", "anonymous", null, null, "failure",
                "{\"reason\":\"invalid_or_expired_token\"}", cancellationToken);

            return Results.Problem(
                "The reset token is invalid or has expired.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Ok(new { accountId = result.AccountId, reset = true });
    }
}

/// <summary>Request body for starting a password reset.</summary>
public sealed record PasswordResetRequest(string? Identifier, string? Realm, string? PublicBaseUrl);

/// <summary>Request body for completing a password reset.</summary>
public sealed record PasswordResetConfirmRequest(string? Token, string? NewPassword);

/// <summary>Reset settings the IdP needs (<c>PasswordReset:*</c>).</summary>
public sealed class PasswordResetProxyOptions
{
    public const string SectionName = "PasswordReset";
}
