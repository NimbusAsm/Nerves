using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nerves.Directory.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "directory");

            migrationBuilder.CreateTable(
                name: "invitation",
                schema: "directory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "membership",
                schema: "directory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organization",
                schema: "directory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Realm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invitation_OrganizationId",
                schema: "directory",
                table: "invitation",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_invitation_TokenHash",
                schema: "directory",
                table: "invitation",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_membership_AccountId",
                schema: "directory",
                table: "membership",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_membership_OrganizationId_AccountId",
                schema: "directory",
                table: "membership",
                columns: new[] { "OrganizationId", "AccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_ParentId",
                schema: "directory",
                table: "organization",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_organization_Realm_Slug",
                schema: "directory",
                table: "organization",
                columns: new[] { "Realm", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invitation",
                schema: "directory");

            migrationBuilder.DropTable(
                name: "membership",
                schema: "directory");

            migrationBuilder.DropTable(
                name: "organization",
                schema: "directory");
        }
    }
}
