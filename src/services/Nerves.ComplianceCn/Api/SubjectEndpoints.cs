using Microsoft.EntityFrameworkCore;
using Nerves.ComplianceCn.Domain;
using Nerves.ComplianceCn.Integration;
using Nerves.ComplianceCn.Persistence;
using Nerves.ServiceDefaults.Security;

namespace Nerves.ComplianceCn.Api;

/// <summary>Data subject access and erasure endpoints.</summary>
public static class SubjectEndpoints
{
    public static RouteGroupBuilder MapSubjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/compliance/subjects")
            .RequireAuthorization(BearerAuthentication.AdminPolicy);

        group.MapGet("/{subjectId}/export", ExportAsync);
        group.MapPost("/{subjectId}/erase", EraseAsync);
        group.MapGet("/{subjectId}/erasures", ListErasuresAsync);

        return group;
    }

    private static async Task<IResult> ExportAsync(
        string subjectId,
        ComplianceDbContext db,
        IdentityClient identity,
        CancellationToken cancellationToken)
    {
        var consents = await db.Consents.AsNoTracking()
            .Where(x => x.SubjectId == subjectId)
            .OrderBy(x => x.GrantedAt)
            .ToListAsync(cancellationToken);

        var account = await identity.GetAccountAsync(subjectId, cancellationToken);
        var policies = await db.RetentionPolicies.AsNoTracking().ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            subjectId,
            exportedAt = DateTimeOffset.UtcNow,
            account = account is null
                ? null
                : new
                {
                    account.AccountId,
                    account.Realm,
                    account.Username,
                    account.DisplayName,
                    account.Status,
                },
            consents = consents.Select(x => new
            {
                x.Purpose,
                x.Version,
                x.GrantedAt,
                x.ExpiresAt,
                x.RevokedAt,
                x.RevocationReason,
            }),
            retentionPolicies = policies.Select(x => new { x.Category, x.RetentionDays, x.LegalBasis }),
        });
    }

    private static async Task<IResult> EraseAsync(
        string subjectId,
        EraseRequestBody? body,
        ComplianceDbContext db,
        IdentityClient identity,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var request = Domain.ErasureRequest.Create(subjectId, body?.Reason ?? string.Empty, now);
        db.ErasureRequests.Add(request);
        db.OutboxEvents.Add(OutboxEvent.Create(
            "compliance.erasure.requested",
            "account",
            subjectId,
            "{}",
            now));

        var active = await db.Consents
            .Where(x => x.SubjectId == subjectId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var consent in active)
        {
            consent.Revoke(now, "data subject erasure");
        }

        await db.SaveChangesAsync(cancellationToken);

        var disabled = Guid.TryParse(subjectId, out _)
            && await identity.DisableAsync(subjectId, "data subject erasure", cancellationToken);

        if (disabled || !Guid.TryParse(subjectId, out _))
        {
            request.Complete(now);
        }
        else
        {
            request.Fail(now);
        }

        db.OutboxEvents.Add(OutboxEvent.Create(
            "compliance.erasure.completed",
            "account",
            subjectId,
            "{\"status\":\"" + request.Status + "\"}",
            now));

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            request.Id,
            request.SubjectId,
            request.Status,
            consentRevoked = active.Count,
            accountDisabled = disabled,
        });
    }

    private static async Task<IResult> ListErasuresAsync(
        string subjectId,
        ComplianceDbContext db,
        CancellationToken cancellationToken)
    {
        var requests = await db.ErasureRequests.AsNoTracking()
            .Where(x => x.SubjectId == subjectId)
            .OrderByDescending(x => x.RequestedAt)
            .ToListAsync(cancellationToken);

        return Results.Ok(requests.Select(x => new
        {
            x.Id,
            x.Status,
            x.Reason,
            x.RequestedAt,
            x.CompletedAt,
        }));
    }
}

/// <summary>Erasure payload.</summary>
public sealed record EraseRequestBody(string? Reason);
