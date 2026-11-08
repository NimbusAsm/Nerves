using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nerves.Authorization.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "group",
                schema: "authz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Realm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "group_member",
                schema: "authz",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_member", x => new { x.GroupId, x.AccountId });
                    table.ForeignKey(
                        name: "FK_group_member_group_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "authz",
                        principalTable: "group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_group_Realm_Code",
                schema: "authz",
                table: "group",
                columns: new[] { "Realm", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_member_AccountId",
                schema: "authz",
                table: "group_member",
                column: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "group_member",
                schema: "authz");

            migrationBuilder.DropTable(
                name: "group",
                schema: "authz");
        }
    }
}
