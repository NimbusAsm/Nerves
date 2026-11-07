using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nerves.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "legal_entity",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    id_doc_type = table.Column<int>(type: "integer", nullable: false),
                    id_doc_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    id_doc_country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    VerificationState = table.Column<int>(type: "integer", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProviderReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_entity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "network_account",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Realm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DisabledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_network_account", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_legal_entity_id_doc_hash",
                schema: "identity",
                table: "legal_entity",
                column: "id_doc_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_legal_entity_VerificationState",
                schema: "identity",
                table: "legal_entity",
                column: "VerificationState");

            migrationBuilder.CreateIndex(
                name: "IX_network_account_Email",
                schema: "identity",
                table: "network_account",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_network_account_LegalEntityId",
                schema: "identity",
                table: "network_account",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_network_account_PhoneNumber",
                schema: "identity",
                table: "network_account",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_network_account_Realm_Username",
                schema: "identity",
                table: "network_account",
                columns: new[] { "Realm", "Username" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "legal_entity",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "network_account",
                schema: "identity");
        }
    }
}
