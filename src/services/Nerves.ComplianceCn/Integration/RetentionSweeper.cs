using Microsoft.EntityFrameworkCore;
using Nerves.ComplianceCn.Domain;
using Nerves.ComplianceCn.Options;
using Nerves.ComplianceCn.Persistence;

namespace Nerves.ComplianceCn.Integration;

/// <summary>Counts produced by one retention sweep.</summary>
public sealed record RetentionSweepResult(
    int PoliciesApplied,
    int ConsentsRemoved,
    int ErasuresAnonymized,
    int ErasuresRemoved,
    bool Skipped)
{
    public static RetentionSweepResult SkippedRun { get; } =
        new(0, 0, 0, 0, true);

    public bool HasChanges => ConsentsRemoved + ErasuresAnonymized + ErasuresRemoved > 0;
}

/// <summary>
/// Deletes or anonymizes data whose retention window has elapsed. The sweep is
/// deliberately conservative: a category with no stored policy is skipped, and
/// erasure records are anonymized before being removed so the audit anchor is
/// not lost silently.
/// </summary>
public sealed class RetentionSweeper(ComplianceDbContext db)
{
    public async Task<RetentionSweepResult> SweepAsync(
        RetentionOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return RetentionSweepResult.SkippedRun;
        }

        var batch = options.ClampBatch();
        var policies = await db.RetentionPolicies
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Category, StringComparer.OrdinalIgnoreCase, cancellationToken);

        if (!policies.TryGetValue(options.ConsentCategory, out var consentPolicy)
            && !policies.TryGetValue(options.ErasureCategory, out var erasurePolicy))
        {
            return RetentionSweepResult.SkippedRun;
        }

        var applied = 0;
        var consentsRemoved = 0;
        var erasuresAnonymized = 0;
        var erasuresRemoved = 0;

        if (policies.TryGetValue(options.ConsentCategory, out consentPolicy))
        {
            applied++;
            var cutoff = now.AddDays(-consentPolicy.RetentionDays);
            var expiredConsents = await db.Consents
                .Where(x => x.GrantedAt < cutoff)
                .OrderBy(x => x.GrantedAt)
                .Take(batch)
                .ToListAsync(cancellationToken);

            db.Consents.RemoveRange(expiredConsents);
            consentsRemoved = expiredConsents.Count;
        }

        if (policies.TryGetValue(options.ErasureCategory, out erasurePolicy))
        {
            applied++;
            var cutoff = now.AddDays(-erasurePolicy.RetentionDays);

            // Completed requests are the audit anchor: anonymize the subject
            // first, then delete the record once the same window elapses again.
            var completed = await db.ErasureRequests
                .Where(x => x.CompletedAt != null && x.CompletedAt < cutoff && x.SubjectId != ErasureRequest.AnonymizedSubject)
                .OrderBy(x => x.CompletedAt)
                .Take(batch)
                .ToListAsync(cancellationToken);

            foreach (var request in completed)
            {
                request.Anonymize(now);
            }

            erasuresAnonymized = completed.Count;

            var anonymized = await db.ErasureRequests
                .Where(x => x.SubjectId == ErasureRequest.AnonymizedSubject && x.CompletedAt < cutoff)
                .OrderBy(x => x.CompletedAt)
                .Take(batch)
                .ToListAsync(cancellationToken);

            db.ErasureRequests.RemoveRange(anonymized);
            erasuresRemoved = anonymized.Count;
        }

        if (consentsRemoved + erasuresAnonymized + erasuresRemoved > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new RetentionSweepResult(applied, consentsRemoved, erasuresAnonymized, erasuresRemoved, false);
    }
}
