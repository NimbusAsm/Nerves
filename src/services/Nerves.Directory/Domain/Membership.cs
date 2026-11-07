using Nerves.Core;

namespace Nerves.Directory.Domain;

/// <summary>Binds an account to an organization with a role name.</summary>
public sealed class Membership
{
    private Membership()
    {
        Role = string.Empty;
    }

    private Membership(Guid id, Guid organizationId, Guid accountId, string role, DateTimeOffset now)
    {
        Id = id;
        OrganizationId = organizationId;
        AccountId = accountId;
        Role = role;
        Status = MembershipStatus.Active;
        JoinedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid AccountId { get; private set; }

    public string Role { get; private set; }

    public MembershipStatus Status { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public static Membership Create(Guid organizationId, Guid accountId, string role, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        return new Membership(IdGenerator.NewId(now), organizationId, accountId, role.Trim().ToLowerInvariant(), now);
    }

    public void Suspend() => Status = MembershipStatus.Suspended;

    public void Remove() => Status = MembershipStatus.Removed;

    public void ChangeRole(string role) => Role = role.Trim().ToLowerInvariant();
}

/// <summary>
/// An invitation to join an organization. Only the hash of the accept token is
/// stored, so a database leak cannot be replayed.
/// </summary>
public sealed class Invitation
{
    private Invitation()
    {
        EmailHash = string.Empty;
        Role = string.Empty;
        TokenHash = string.Empty;
    }

    private Invitation(Guid id, Guid organizationId, string emailHash, string role, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        Id = id;
        OrganizationId = organizationId;
        EmailHash = emailHash;
        Role = role;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string EmailHash { get; private set; }

    public string Role { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public Guid? AcceptedByAccountId { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public static Invitation Create(
        Guid organizationId,
        string emailHash,
        string role,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new Invitation(IdGenerator.NewId(now), organizationId, emailHash, role.Trim().ToLowerInvariant(), tokenHash, now.Add(lifetime), now);
    }

    public Result Accept(Guid accountId, DateTimeOffset now)
    {
        if (AcceptedAt is not null)
        {
            return Result.Failure(Reason.Conflict("invitation already accepted"));
        }

        if (now > ExpiresAt)
        {
            return Result.Failure(Reason.Conflict("invitation expired"));
        }

        AcceptedByAccountId = accountId;
        AcceptedAt = now;
        return Result.Success();
    }
}
