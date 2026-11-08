using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nerves.Idp.Domain;
using Nerves.Idp.Persistence;

namespace Nerves.Idp.Security;

/// <summary>Issues and redeems single-use login tickets.</summary>
public sealed class LoginTickets(IdpDbContext db)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(90);

    public async Task<string> IssueAsync(
        string realm,
        Guid accountId,
        Guid? legalEntityId,
        string displayName,
        string? email,
        CancellationToken cancellationToken)
    {
        var raw = Base64Url(RandomNumberGenerator.GetBytes(32));
        var ticket = LoginTicket.Issue(Hash(raw), realm, accountId, legalEntityId, displayName, email, Lifetime);

        db.LoginTickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);

        return raw;
    }

    public async Task<LoginTicket?> RedeemAsync(string raw, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var hash = Hash(raw);
        var ticket = await db.LoginTickets.FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        if (ticket is null || !ticket.IsUsable(DateTimeOffset.UtcNow))
        {
            return null;
        }

        ticket.Consume();
        await db.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Base64Url(byte[] bytes) =>
        System.Buffers.Text.Base64Url.EncodeToString(bytes);
}
