using Nerves.Core;

namespace Nerves.Directory.Domain;

/// <summary>Membership lifecycle.</summary>
public enum MembershipStatus
{
    Active = 1,
    Suspended = 2,
    Removed = 3,
}

/// <summary>An organization inside one realm (department, customer, school).</summary>
public sealed class Organization
{
    private Organization()
    {
        Realm = string.Empty;
        Name = string.Empty;
        Slug = string.Empty;
    }

    private Organization(Guid id, string realm, string name, string slug, string? description, Guid? parentId, DateTimeOffset now)
    {
        Id = id;
        Realm = realm;
        Name = name;
        Slug = slug;
        Description = description;
        ParentId = parentId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Realm { get; private set; }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    public string? Description { get; private set; }

    public Guid? ParentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<Organization> Create(
        string realm,
        string name,
        string slug,
        string? description,
        Guid? parentId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(realm) || realm.Length > 64)
        {
            return Result.Failure<Organization>(Reason.Validation("realm must be between 1 and 64 characters"));
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            return Result.Failure<Organization>(Reason.Validation("name must be between 1 and 200 characters"));
        }

        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 64)
        {
            return Result.Failure<Organization>(Reason.Validation("slug must be between 1 and 64 characters"));
        }

        if (slug.Any(c => !(char.IsLetterOrDigit(c) || c is '-' or '_')))
        {
            return Result.Failure<Organization>(Reason.Validation("slug may only contain letters, digits, '-' and '_'"));
        }

        return Result.Success(new Organization(
            IdGenerator.NewId(now),
            realm.Trim().ToLowerInvariant(),
            name.Trim(),
            slug.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            parentId,
            now));
    }

    public void Rename(string name, string? description, DateTimeOffset now)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAt = now;
    }
}
