using Microsoft.EntityFrameworkCore;
using Nerves.Idp.Persistence;

namespace Nerves.Idp.Federation;

/// <summary>Persists federation state so a restart does not break a login.</summary>
public sealed class FederationStateStore(IdpDbContext db)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public async Task<FederationState> CreateAsync(string provider, string? returnUrl, CancellationToken cancellationToken)
    {
        var state = FederationState.Create(provider, returnUrl, Lifetime);
        db.FederationStates.Add(state);
        await db.SaveChangesAsync(cancellationToken);
        return state;
    }

    public async Task<FederationState?> ConsumeAsync(string rawState, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(rawState, out var id))
        {
            return null;
        }

        var state = await db.FederationStates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (state is null || !state.IsUsable(DateTimeOffset.UtcNow))
        {
            return null;
        }

        state.Consume();
        await db.SaveChangesAsync(cancellationToken);
        return state;
    }
}
