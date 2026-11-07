using Grpc.Net.Client;
using Microsoft.Extensions.Options;
using Nerves.Contracts.Identity.V1;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Gateway.Api;

/// <summary>
/// Development login for the admin console: verifies credentials against the
/// identity service and returns the configured management token.
/// Production replaces this with the BFF cookie + CSRF flow (PLAN §9.2).
/// </summary>
public static class LoginEndpoints
{
    public static IEndpointRouteBuilder MapLoginEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/auth/login", async (
            LoginRequest request,
            IdentityService.IdentityServiceClient identity,
            IOptions<AdminTokenOptions> tokenOptions,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.Problem("Username and password are required.", statusCode: StatusCodes.Status400BadRequest);
            }

            var token = tokenOptions.Value.AdminToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                return Results.Problem(
                    "Management API token is not configured.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var realm = configuration["Admin:Realm"] ?? "nerves";
            var check = await identity.VerifyCredentialAsync(
                new VerifyCredentialRequest
                {
                    Identifier = request.Username,
                    Password = request.Password,
                    Realm = realm,
                },
                cancellationToken: cancellationToken);

            if (!check.Verified)
            {
                return Results.Json(
                    new { error = "invalid_credentials", message = "用户名或口令不正确。" },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return Results.Ok(new
            {
                token,
                user = new
                {
                    id = check.AccountId,
                    name = string.IsNullOrEmpty(check.DisplayName) ? request.Username : check.DisplayName,
                    email = check.Email,
                    legalEntityId = check.LegalEntityId,
                },
            });
        });

        return app;
    }
}

/// <summary>Admin console login payload.</summary>
public sealed record LoginRequest(string Username, string Password);
