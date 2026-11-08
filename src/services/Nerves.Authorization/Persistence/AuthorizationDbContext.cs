using Microsoft.EntityFrameworkCore;
using Nerves.Authorization.Domain;

namespace Nerves.Authorization.Persistence;

/// <summary>Owns the <c>authz</c> schema: roles, permissions and assignments.</summary>
public sealed class AuthorizationDbContext(DbContextOptions<AuthorizationDbContext> options) : DbContext(options)
{
    public const string Schema = "authz";

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    public DbSet<SubjectVersion> SubjectVersions => Set<SubjectVersion>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Group>(builder =>
        {
            builder.ToTable("group");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Realm).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Code).HasMaxLength(128).IsRequired();
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.HasIndex(x => new { x.Realm, x.Code }).IsUnique();
        });

        modelBuilder.Entity<GroupMember>(builder =>
        {
            builder.ToTable("group_member");
            builder.HasKey(x => new { x.GroupId, x.AccountId });
            builder.HasIndex(x => x.AccountId);
        });

        modelBuilder.Entity<Role>(builder =>
        {
            builder.ToTable("role");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Realm).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.HasIndex(x => new { x.Realm, x.Code }).IsUnique();
            builder.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<Permission>(builder =>
        {
            builder.ToTable("permission");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Code).HasMaxLength(128).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
            builder.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(builder =>
        {
            builder.ToTable("role_permission");
            builder.HasKey(x => new { x.RoleId, x.PermissionId });
        });

        modelBuilder.Entity<RoleAssignment>(builder =>
        {
            builder.ToTable("role_assignment");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.SubjectType).HasConversion<int>().IsRequired();
            builder.HasIndex(x => new { x.SubjectType, x.SubjectId });
            builder.HasIndex(x => x.RoleId);
        });

        modelBuilder.Entity<SubjectVersion>(builder =>
        {
            builder.ToTable("subject_version");
            builder.HasKey(x => x.SubjectId);
        });
    }
}
