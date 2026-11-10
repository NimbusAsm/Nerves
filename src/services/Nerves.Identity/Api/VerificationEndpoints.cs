using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nerves.Identity.Domain;
using Nerves.Identity.Integration;
using Nerves.Identity.Persistence;
using Nerves.Identity.Verification;
using Nerves.ServiceDefaults.Security;

namespace Nerves.Identity.Api;

/// <summary>
/// Asynchronous real-name verification through a provider. Starting a
/// verification records an attempt and leaves the legal entity pending until
/// the provider (or an administrator) records the outcome.
/// </summary>
public static class VerificationEndpoints
{
    public static RouteGroupBuilder MapVerificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/legal-entities")
            .RequireAuthorization(BearerAuthentication.AdminPolicy);

        group.MapPost("/{id:guid}/verification/start", StartAsync);
        group.MapGet("/{id:guid}/verification", GetAsync);

        // The provider can never hold a Nerves admin token, so the callback is
        // anonymous. Authenticity comes from the attempt reference, which is
        // only ever sent to the provider and is unique per attempt.
        app.MapPost("/api/v1/legal-entities/{id:guid}/verification/callback", CallbackAsync)
            .AllowAnonymous()
            .RequireRateLimiting("verification-callback");

        return group;
    }

    private static async Task<IResult> StartAsync(
        Guid id,
        VerificationStartRequest? request,
        IdentityDbContext db,
        VerificationProviderFactory providers,
        Microsoft.Extensions.Options.IOptions<VerificationOptions> options,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var entity = await db.LegalEntities.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound();
        }

        if (entity.VerificationState is not (VerificationState.Pending or VerificationState.Rejected))
        {
            return Results.Problem(
                "only a pending or rejected legal entity can start a verification",
                statusCode: StatusCodes.Status409Conflict);
        }

        var provider = providers.Resolve(request?.Provider);
        if (provider is null)
        {
            return Results.Problem(
                $"unknown verification provider '{request?.Provider}'",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var now = DateTimeOffset.UtcNow;
        var callbackUrl = BuildCallbackUrl(options.Value, http, id);

        var attempt = VerificationAttempt.Create(id, provider.Name, null, now);
        if (attempt.IsFailure)
        {
            return Results.Problem(attempt.Reason.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        db.VerificationAttempts.Add(attempt.Value);
        await db.SaveChangesAsync(cancellationToken);

        var response = await provider.StartAsync(
            new VerificationProviderRequest(
                entity.Id,
                entity.Type.ToString(),
                entity.RealName.DocumentType.ToString(),
                entity.RealName.Hash,
                entity.RealName.Country,
                entity.DisplayName,
                callbackUrl,
                now),
            cancellationToken);

        var outcome = ApplyOutcome(entity, attempt.Value, response, now);

        db.OutboxEvents.Add(OutboxEvent.Create(
            "realname.verification.started",
            null,
            entity.Id,
            JsonSerializer.Serialize(new
            {
                legalEntityId = entity.Id,
                attemptId = attempt.Value.Id,
                provider = provider.Name,
                outcome = attempt.Value.Status.ToString(),
            }),
            now));

        // A provider may answer synchronously (verified/rejected). Fan the state
        // change out exactly like the asynchronous callback path does, otherwise
        // synchronously verified entities would never notify their accounts.
        if (outcome is not null)
        {
            await FanOutAsync(db, entity, now);
        }

        await db.SaveChangesAsync(cancellationToken);

        await audit.AppendAsync(
            "legal-entity.verification.started",
            "admin",
            null,
            "legal-entity",
            entity.Id.ToString(),
            "success",
            http.TraceIdentifier,
            JsonSerializer.Serialize(new { provider = provider.Name }),
            cancellationToken);

        return Results.Accepted(
            $"/api/v1/legal-entities/{entity.Id}/verification",
            ToResponse(entity, attempt.Value));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var entity = await db.LegalEntities.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return Results.NotFound();
        }

        var attempt = await db.VerificationAttempts.AsNoTracking()
            .Where(x => x.LegalEntityId == id)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Results.Ok(ToResponse(entity, attempt));
    }

    /// <summary>
    /// Callback used by asynchronous providers. It is anonymous because the
    /// provider cannot hold a Nerves token; authenticity is established by the
    /// attempt reference, which is only ever sent to the provider.
    /// </summary>
    private static async Task<IResult> CallbackAsync(
        Guid id,
        VerificationCallbackRequest request,
        IdentityDbContext db,
        Microsoft.Extensions.Options.IOptions<VerificationOptions> options,
        AuditClient audit,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reference) || request.Reference.Length > 200)
        {
            return Results.Problem("reference is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!Enum.TryParse<VerificationAttemptStatus>(request.Status, ignoreCase: true, out var status)
            || status is VerificationAttemptStatus.Pending)
        {
            return Results.Problem(
                "status must be one of verified, rejected, expired or failed",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var entity = await db.LegalEntities.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound();
        }

        var attempt = await db.VerificationAttempts
            .Where(x => x.LegalEntityId == id && x.Reference == request.Reference)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (attempt is null)
        {
            return Results.NotFound();
        }

        // A provider may retry a callback; a completed attempt answers the
        // same outcome idempotently instead of failing the second delivery.
        if (!attempt.IsPending)
        {
            return attempt.Status == status
                ? Results.Ok(ToResponse(entity, attempt))
                : Results.Problem(
                    "verification attempt is already completed with a different outcome",
                    statusCode: StatusCodes.Status409Conflict);
        }

        var now = DateTimeOffset.UtcNow;
        if (now > attempt.CreatedAt.AddMinutes(options.Value.AttemptLifetimeMinutes))
        {
            attempt.Complete(VerificationAttemptStatus.Expired, "provider callback arrived after the attempt expired", now);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Problem("verification attempt has expired", statusCode: StatusCodes.Status410Gone);
        }

        var applied = ApplyEntityTransition(entity, status, request.Reference, now);
        if (applied.IsFailure)
        {
            return Results.Problem(applied.Reason.Message, statusCode: StatusCodes.Status409Conflict);
        }

        var completed = attempt.Complete(status, request.Message, now);
        if (completed.IsFailure)
        {
            return Results.Problem(completed.Reason.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await FanOutAsync(db, entity, now);
        await db.SaveChangesAsync(cancellationToken);

        await audit.AppendAsync(
            "legal-entity." + status.ToString().ToLowerInvariant(),
            "provider",
            attempt.Provider,
            "legal-entity",
            entity.Id.ToString(),
            "success",
            http.TraceIdentifier,
            JsonSerializer.Serialize(new { attemptId = attempt.Id }),
            cancellationToken);

        return Results.Ok(ToResponse(entity, attempt));
    }

    /// <summary>
    /// Applies a synchronous provider outcome. Returns the new verification
    /// state when the entity actually changed, otherwise null.
    /// </summary>
    private static VerificationState? ApplyOutcome(
        LegalEntity entity,
        VerificationAttempt attempt,
        VerificationProviderResponse response,
        DateTimeOffset now)
    {
        switch (response.Outcome)
        {
            case VerificationProviderOutcome.Verified:
                attempt.AttachReference(response.Reference, now);
                attempt.Complete(VerificationAttemptStatus.Verified, response.Message, now);
                if (entity.MarkVerified(response.Reference, now).IsFailure)
                {
                    return null;
                }

                return entity.VerificationState;

            case VerificationProviderOutcome.Rejected:
                attempt.AttachReference(response.Reference, now);
                attempt.Complete(VerificationAttemptStatus.Rejected, response.Message, now);
                if (entity.MarkRejected(response.Reference, now).IsFailure)
                {
                    return null;
                }

                return entity.VerificationState;

            case VerificationProviderOutcome.Failed:
                attempt.Complete(VerificationAttemptStatus.Failed, response.Message, now);
                return null;

            default:
                attempt.AttachReference(response.Reference, now);
                return null;
        }
    }

    private static Nerves.Core.Result ApplyEntityTransition(
        LegalEntity entity,
        VerificationAttemptStatus status,
        string reference,
        DateTimeOffset now) => status switch
        {
            VerificationAttemptStatus.Verified => entity.MarkVerified(reference, now),
            VerificationAttemptStatus.Rejected => entity.MarkRejected(reference, now),
            VerificationAttemptStatus.Expired => entity.Expire(now),
            _ => Nerves.Core.Result.Success(),
        };

    private static async Task FanOutAsync(IdentityDbContext db, LegalEntity entity, DateTimeOffset now)
    {
        var accounts = await db.NetworkAccounts
            .Where(x => x.LegalEntityId == entity.Id)
            .Select(x => x.Id)
            .ToListAsync();

        var payload = JsonSerializer.Serialize(new
        {
            legalEntityId = entity.Id,
            state = entity.VerificationState.ToString(),
        });

        foreach (var accountId in accounts)
        {
            db.OutboxEvents.Add(OutboxEvent.Create("realname.changed", accountId, entity.Id, payload, now));
        }
    }

    private static string BuildCallbackUrl(VerificationOptions options, HttpContext http, Guid id)
    {
        var baseUrl = string.IsNullOrWhiteSpace(options.PublicBaseUrl)
            ? http.Request.Scheme + "://" + http.Request.Host
            : options.PublicBaseUrl.TrimEnd('/');

        return baseUrl + "/api/v1/legal-entities/" + id + "/verification/callback";
    }

    private static VerificationResponse ToResponse(LegalEntity entity, VerificationAttempt? attempt) => new(
        entity.Id.ToString(),
        entity.VerificationState.ToString(),
        entity.ProviderReference,
        attempt is null
            ? null
            : new VerificationAttemptResponse(
                attempt.Id.ToString(),
                attempt.Provider,
                attempt.Status.ToString(),
                attempt.Reference,
                attempt.Message,
                attempt.CreatedAt,
                attempt.CompletedAt));
}

/// <summary>Request body for starting a verification.</summary>
public sealed record VerificationStartRequest(string? Provider);

/// <summary>Request body for a provider callback.</summary>
public sealed record VerificationCallbackRequest(string? Reference, string? Status, string? Message);

/// <summary>Verification state of one legal entity plus its latest attempt.</summary>
public sealed record VerificationResponse(
    string LegalEntityId,
    string State,
    string? ProviderReference,
    VerificationAttemptResponse? Attempt);

/// <summary>One verification attempt as returned by the API.</summary>
public sealed record VerificationAttemptResponse(
    string Id,
    string Provider,
    string Status,
    string? Reference,
    string? Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
