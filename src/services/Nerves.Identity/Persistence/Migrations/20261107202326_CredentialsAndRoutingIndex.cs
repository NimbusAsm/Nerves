using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nerves.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CredentialsAndRoutingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_credential",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Secret = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Algorithm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_credential", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "routing_index",
                schema: "identity",
                columns: table => new
                {
                    IdentifierHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdentifierType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Realm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routing_index", x => x.IdentifierHash);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_credential_AccountId",
                schema: "identity",
                table: "account_credential",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_routing_index_AccountId",
                schema: "identity",
                table: "routing_index",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_routing_index_Realm",
                schema: "identity",
                table: "routing_index",
                column: "Realm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_credential",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "routing_index",
                schema: "identity");
        }
    }
}
