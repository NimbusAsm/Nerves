using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nerves.Authorization.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "authz");

            migrationBuilder.CreateTable(
                name: "permission",
                schema: "authz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permission", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "role",
                schema: "authz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Realm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "role_assignment",
                schema: "authz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectType = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_assignment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "subject_version",
                schema: "authz",
                columns: table => new
                {
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subject_version", x => x.SubjectId);
                });

            migrationBuilder.CreateTable(
                name: "role_permission",
                schema: "authz",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permission", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_role_permission_role_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "authz",
                        principalTable: "role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_permission_Code",
                schema: "authz",
                table: "permission",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_Realm_Code",
                schema: "authz",
                table: "role",
                columns: new[] { "Realm", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_role_assignment_RoleId",
                schema: "authz",
                table: "role_assignment",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_role_assignment_SubjectType_SubjectId",
                schema: "authz",
                table: "role_assignment",
                columns: new[] { "SubjectType", "SubjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "permission",
                schema: "authz");

            migrationBuilder.DropTable(
                name: "role_assignment",
                schema: "authz");

            migrationBuilder.DropTable(
                name: "role_permission",
                schema: "authz");

            migrationBuilder.DropTable(
                name: "subject_version",
                schema: "authz");

            migrationBuilder.DropTable(
                name: "role",
                schema: "authz");
        }
    }
}
