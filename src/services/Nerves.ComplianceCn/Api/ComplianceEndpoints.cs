using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nerves.ComplianceCn.Domain;
using Nerves.ComplianceCn.Persistence;
using Nerves.ServiceDefaults.Security;

namespace Nerves.ComplianceCn.Api;

/// <summary>
/// China-facing compliance surface: consent records and retention policies.
/// Naming and endpoint shape follow the planned <c>Compliance.CN</c> module.
/// </summary>
public static class ComplianceEndpoints
{
    public static RouteGroupBuilder MapComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/compliance")
            .RequireAuthorization(BearerAuthentication.AdminPolicy);

        group.MapGet("/consents", ListConsentsAsync);
        group.MapPost("/consents", GrantAsync);
        group.MapPost("/consents/{id:guid}/revoke", RevokeAsync);

        group.MapGet("/retention-policies", ListPoliciesAsync);
        group.MapPut("/retention-policies/{category}", UpsertPolicyAsync);

        group.MapGet("/summary", SummaryAsync);

        return group;
    }

    private static async Task<IResult> ListConsentsAsync(
        ComplianceDbContext db,
        string? subjectId = null,
        string? purpose = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.Consents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(subjectId)) query = query.Where(x => x.SubjectId == subjectId);
        if (!string.IsNullOrWhiteSpace(purpose)) query = query.Where(x => x.Purpose == purpose);

        var consents = await query
            .OrderByDescending(x => x.GrantedAt)
            .Take(200)
            .Select(x => new
            {
                x.Id,
                x.SubjectId,
                x.Purpose,
                x.Version,
                x.GrantedAt,
                x.ExpiresAt,
                x.RevokedAt,
                x.RevocationReason,
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(consents);
    }

    private static async Task<IResult> GrantAsync(
        GrantConsentRequest request,
        ComplianceDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SubjectId)
            || string.IsNullOrWhiteSpace(request.Purpose)
            || string.IsNullOrWhiteSpace(request.Version))
        {
            return Results.Problem("subjectId, purpose and version are required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var now = DateTimeOffset.UtcNow;
        var consent = Consent.Grant(
            request.SubjectId, request.Purpose, request.Version, now, request.ExpiresAt);

        db.Consents.Add(consent);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/v1/compliance/consents/{consent.Id}", new
        {
            consent.Id,
            consent.SubjectId,
            consent.Purpose,
            consent.Version,
            consent.GrantedAt,
            consent.ExpiresAt,
        });
    }

    private static async Task<IResult> RevokeAsync(
        Guid id,
        [FromBody] RevokeConsentRequest? request,
        ComplianceDbContext db,
        CancellationToken cancellationToken)
    {
        var consent = await db.Consents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (consent is null)
        {
            return Results.NotFound();
        }

        consent.Revoke(DateTimeOffset.UtcNow, request?.Reason);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { consent.Id, consent.RevokedAt, consent.RevocationReason });
    }

    private static async Task<IResult> ListPoliciesAsync(
        ComplianceDbContext db,
        CancellationToken cancellationToken)
    {
        var policies = await db.RetentionPolicies.AsNoTracking()
            .OrderBy(x => x.Category)
            .Select(x => new { x.Id, x.Category, x.RetentionDays, x.LegalBasis, x.UpdatedAt })
            .ToListAsync(cancellationToken);

        return Results.Ok(policies);
    }

    private static async Task<IResult> UpsertPolicyAsync(
        string category,
        UpsertPolicyRequest request,
        ComplianceDbContext db,
        CancellationToken cancellationToken)
    {
        if (request.RetentionDays <= 0)
        {
            return Results.Problem("retentionDays must be positive.", statusCode: StatusCodes.Status400BadRequest);
        }

        var policy = await db.RetentionPolicies.FirstOrDefaultAsync(x => x.Category == category, cancellationToken);

        if (policy is null)
        {
            policy = RetentionPolicy.Create(category, request.RetentionDays, request.LegalBasis);
            db.RetentionPolicies.Add(policy);
        }
        else
        {
            policy.Update(request.RetentionDays, request.LegalBasis);
        }

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { policy.Id, policy.Category, policy.RetentionDays, policy.LegalBasis });
    }

    private static async Task<IResult> SummaryAsync(
        ComplianceDbContext db,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var total = await db.Consents.CountAsync(cancellationToken);
        var active = await db.Consents.CountAsync(
            x => x.RevokedAt == null && (x.ExpiresAt == null || x.ExpiresAt > now),
            cancellationToken);
        var revoked = await db.Consents.CountAsync(x => x.RevokedAt != null, cancellationToken);
        var policies = await db.RetentionPolicies.CountAsync(cancellationToken);

        return Results.Ok(new { consents = total, active, revoked, retentionPolicies = policies });
    }
}

/// <summary>Consent payload.</summary>
public sealed record GrantConsentRequest(string SubjectId, string Purpose, string Version, DateTimeOffset? ExpiresAt);

/// <summary>Revocation payload.</summary>
public sealed record RevokeConsentRequest(string? Reason);

/// <summary>Retention policy payload.</summary>
public sealed record UpsertPolicyRequest(int RetentionDays, string? LegalBasis);
