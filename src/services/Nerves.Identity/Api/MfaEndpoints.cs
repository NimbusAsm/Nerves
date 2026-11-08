using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Domain;
using Nerves.Identity.Persistence;
using Nerves.Identity.Security;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Identity.Api;

/// <summary>Management API for multi-factor credentials.</summary>
public static class MfaEndpoints
{
    public static RouteGroupBuilder MapMfaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/accounts/{accountId:guid}/mfa")
            .RequireAuthorization(BearerAuthentication.AdminPolicy);

        group.MapGet("/", ListAsync);
        group.MapPost("/totp", EnrollAsync);
        group.MapPost("/totp/activate", ActivateAsync);
        group.MapDelete("/totp", RemoveAsync);
        group.MapGet("/recovery-codes", CountRecoveryCodesAsync);
        group.MapPost("/recovery-codes", GenerateRecoveryCodesAsync);
        group.MapDelete("/recovery-codes", RemoveRecoveryCodesAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(
        Guid accountId,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var factors = await db.AccountCredentials.AsNoTracking()
            .Where(x => x.AccountId == accountId && x.Type != CredentialType.Password)
            .Select(x => new { type = x.Type.ToString(), active = x.ActivatedAt != null, createdAt = x.CreatedAt })
            .ToListAsync(cancellationToken);

        return Results.Ok(factors);
    }

    private static async Task<IResult> EnrollAsync(
        Guid accountId,
        IdentityDbContext db,
        TotpService totp,
        TotpSecretProtector protector,
        CancellationToken cancellationToken)
    {
        var account = await db.NetworkAccounts.FirstOrDefaultAsync(x => x.Id == accountId, cancellationToken);
        if (account is null)
        {
            return Results.NotFound();
        }

        var existing = await db.AccountCredentials.FirstOrDefaultAsync(
            x => x.AccountId == accountId && x.Type == CredentialType.Totp, cancellationToken);

        var secret = totp.GenerateSecret();
        var protectedSecret = protector.Protect(secret);
        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            var created = AccountCredential.Create(
                accountId, CredentialType.Totp, protectedSecret, "totp-dp", now);

            if (created.IsFailure)
            {
                return Results.Problem(created.Reason.Message, statusCode: StatusCodes.Status400BadRequest);
            }

            db.AccountCredentials.Add(created.Value);
        }
        else
        {
            existing.ReplaceSecret(protectedSecret, "totp-dp", now);
        }

        await db.SaveChangesAsync(cancellationToken);

        var issuer = "Nerves";
        var uri = totp.BuildOtpAuthUri(issuer, account.Email ?? account.Username, secret);

        // The raw secret is returned exactly once, for the authenticator app.
        return Results.Ok(new
        {
            secret,
            otpauthUri = uri,
            algorithm = "SHA1",
            digits = 6,
            periodSeconds = 30,
            active = false,
        });
    }

    private static async Task<IResult> ActivateAsync(
        Guid accountId,
        [FromBody] ActivateTotpRequest request,
        IdentityDbContext db,
        TotpService totp,
        TotpSecretProtector protector,
        CancellationToken cancellationToken)
    {
        var credential = await db.AccountCredentials.FirstOrDefaultAsync(
            x => x.AccountId == accountId && x.Type == CredentialType.Totp, cancellationToken);

        if (credential is null)
        {
            return Results.NotFound();
        }

        var secret = protector.TryUnprotect(credential.Secret);
        if (secret is null || !totp.Verify(secret, request.Code))
        {
            await Task.CompletedTask;
            return Results.Problem("Invalid verification code.", statusCode: StatusCodes.Status400BadRequest);
        }

        credential.Activate(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { active = true });
    }

    private static async Task<IResult> RemoveAsync(
        Guid accountId,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var credential = await db.AccountCredentials.FirstOrDefaultAsync(
            x => x.AccountId == accountId && x.Type == CredentialType.Totp, cancellationToken);

        if (credential is null)
        {
            return Results.NotFound();
        }

        db.AccountCredentials.Remove(credential);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> CountRecoveryCodesAsync(
        Guid accountId,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var remaining = await db.AccountCredentials.CountAsync(
            x => x.AccountId == accountId && x.Type == CredentialType.RecoveryCode,
            cancellationToken);

        return Results.Ok(new { remaining });
    }

    private static async Task<IResult> GenerateRecoveryCodesAsync(
        Guid accountId,
        IdentityDbContext db,
        RecoveryCodes generator,
        CancellationToken cancellationToken)
    {
        if (!await db.NetworkAccounts.AnyAsync(x => x.Id == accountId, cancellationToken))
        {
            return Results.NotFound();
        }

        var existing = await db.AccountCredentials
            .Where(x => x.AccountId == accountId && x.Type == CredentialType.RecoveryCode)
            .ToListAsync(cancellationToken);

        db.AccountCredentials.RemoveRange(existing);

        var codes = generator.Generate();
        var now = DateTimeOffset.UtcNow;

        foreach (var code in codes)
        {
            var credential = AccountCredential.Create(
                accountId, CredentialType.RecoveryCode, RecoveryCodes.HashCode(code), "sha256", now).Value;
            credential.Activate(now);
            db.AccountCredentials.Add(credential);
        }

        await db.SaveChangesAsync(cancellationToken);

        // Plaintext codes are returned once; only their hashes are stored.
        return Results.Ok(new { codes, remaining = codes.Count });
    }

    private static async Task<IResult> RemoveRecoveryCodesAsync(
        Guid accountId,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var existing = await db.AccountCredentials
            .Where(x => x.AccountId == accountId && x.Type == CredentialType.RecoveryCode)
            .ToListAsync(cancellationToken);

        db.AccountCredentials.RemoveRange(existing);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }
}

/// <summary>Activation payload.</summary>
public sealed record ActivateTotpRequest(string Code);
