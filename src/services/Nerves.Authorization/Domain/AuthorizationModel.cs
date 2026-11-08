namespace Nerves.Authorization.Domain;

/// <summary>What kind of subject a role is granted to.</summary>
public enum SubjectType
{
    Account = 1,
    LegalEntity = 2,
    Group = 3,
}

/// <summary>A named set of permissions, optionally scoped to one application.</summary>
public sealed class Role
{
    private Role() => Code = Name = Realm = string.Empty;

    private Role(Guid id, string realm, string code, string name, Guid? applicationId, DateTimeOffset now)
    {
        Id = id;
        Realm = realm;
        Code = code;
        Name = name;
        ApplicationId = applicationId;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Realm { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public Guid? ApplicationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public ICollection<RolePermission> Permissions { get; private set; } = new List<RolePermission>();

    public static Role Create(string realm, string code, string name, Guid? applicationId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Role(Guid.CreateVersion7(now), realm.Trim().ToLowerInvariant(), code.Trim(), name.Trim(), applicationId, now);
    }

    public void Rename(string name) => Name = name.Trim();
}

/// <summary>A permission code, for example <c>crm:invoice:read</c>.</summary>
public sealed class Permission
{
    private Permission() => Code = Description = string.Empty;

    private Permission(Guid id, Guid? applicationId, string code, string description)
    {
        Id = id;
        ApplicationId = applicationId;
        Code = code;
        Description = description;
    }

    public Guid Id { get; private set; }

    public Guid? ApplicationId { get; private set; }

    public string Code { get; private set; }

    public string Description { get; private set; }

    public static Permission Create(Guid? applicationId, string code, string description) =>
        new(Guid.CreateVersion7(), applicationId, code.Trim().ToLowerInvariant(), description.Trim());
}

/// <summary>Join row between a role and a permission.</summary>
public sealed class RolePermission
{
    private RolePermission() { }

    private RolePermission(Guid roleId, Guid permissionId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }

    public static RolePermission Create(Guid roleId, Guid permissionId) => new(roleId, permissionId);
}

/// <summary>A role granted to an account or a legal entity.</summary>
public sealed class RoleAssignment
{
    private RoleAssignment() { }

    private RoleAssignment(Guid id, Guid roleId, SubjectType subjectType, Guid subjectId, DateTimeOffset now)
    {
        Id = id;
        RoleId = roleId;
        SubjectType = subjectType;
        SubjectId = subjectId;
        GrantedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid RoleId { get; private set; }

    public SubjectType SubjectType { get; private set; }

    public Guid SubjectId { get; private set; }

    public DateTimeOffset GrantedAt { get; private set; }

    public static RoleAssignment Create(Guid roleId, SubjectType subjectType, Guid subjectId, DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), roleId, subjectType, subjectId, now);
}

/// <summary>Monotonic version used to invalidate cached authorization decisions.</summary>
public sealed class SubjectVersion
{
    private SubjectVersion() { }

    private SubjectVersion(Guid subjectId, long version)
    {
        SubjectId = subjectId;
        Version = version;
    }

    public Guid SubjectId { get; private set; }

    public long Version { get; private set; }

    public static SubjectVersion Create(Guid subjectId) => new(subjectId, 1);

    public void Bump() => Version++;
}

/// <summary>A named group of accounts that can receive roles as a unit.</summary>
public sealed class Group
{
    private Group() => Realm = Code = Name = string.Empty;

    private Group(Guid id, string realm, string code, string name, DateTimeOffset now)
    {
        Id = id;
        Realm = realm;
        Code = code;
        Name = name;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Realm { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public ICollection<GroupMember> Members { get; private set; } = new List<GroupMember>();

    public static Group Create(string realm, string code, string name, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Group(
            Guid.CreateVersion7(now),
            realm.Trim().ToLowerInvariant(),
            code.Trim(),
            name.Trim(),
            now);
    }

    public void Rename(string name) => Name = name.Trim();
}

/// <summary>Join row between a group and an account.</summary>
public sealed class GroupMember
{
    private GroupMember() { }

    private GroupMember(Guid groupId, Guid accountId, DateTimeOffset now)
    {
        GroupId = groupId;
        AccountId = accountId;
        AddedAt = now;
    }

    public Guid GroupId { get; private set; }

    public Guid AccountId { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public static GroupMember Create(Guid groupId, Guid accountId, DateTimeOffset now) =>
        new(groupId, accountId, now);
}
