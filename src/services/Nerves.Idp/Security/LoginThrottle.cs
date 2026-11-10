using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nerves.Idp.Domain;
using Nerves.Idp.Persistence;

namespace Nerves.Idp.Security;

/// <summary>
/// Persistent login throttling shared by all IdP replicas: five failures inside
/// fifteen minutes lock the identifier for fifteen minutes.
/// </summary>
public sealed class LoginThrottle(IdpDbContext db)
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    public async Task<bool> IsLockedAsync(string identifier, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var hash = Hash(identifier);
        var attempt = await db.LoginAttempts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdentifierHash == hash, cancellationToken);

        return attempt?.IsLocked(now) ?? false;
    }

    public async Task RecordFailureAsync(
        string identifier,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var hash = Hash(identifier);
        var attempt = await db.LoginAttempts
            .FirstOrDefaultAsync(x => x.IdentifierHash == hash, cancellationToken);

        if (attempt is null)
        {
            attempt = LoginAttempt.Create(hash, now);
            db.LoginAttempts.Add(attempt);
        }

        attempt.RegisterFailure(now, MaxFailures, Window, Lockout);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another replica inserted the same row first; retry once against it.
            db.ChangeTracker.Clear();
            var existing = await db.LoginAttempts
                .FirstOrDefaultAsync(x => x.IdentifierHash == hash, cancellationToken);

            if (existing is not null)
            {
                existing.RegisterFailure(now, MaxFailures, Window, Lockout);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    public async Task ResetAsync(string identifier, CancellationToken cancellationToken)
    {
        var hash = Hash(identifier);
        var attempt = await db.LoginAttempts
            .FirstOrDefaultAsync(x => x.IdentifierHash == hash, cancellationToken);

        if (attempt is null)
        {
            return;
        }

        attempt.Reset();
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string Hash(string identifier) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identifier.Trim().ToLowerInvariant())))
            .ToLowerInvariant();
}
