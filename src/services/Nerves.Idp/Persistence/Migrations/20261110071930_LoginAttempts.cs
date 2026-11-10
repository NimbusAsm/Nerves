using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nerves.Idp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LoginAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "login_attempt",
                schema: "idp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentifierHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Failures = table.Column<int>(type: "integer", nullable: false),
                    FirstFailureAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_attempt", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_login_attempt_IdentifierHash",
                schema: "idp",
                table: "login_attempt",
                column: "IdentifierHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "login_attempt",
                schema: "idp");
        }
    }
}
