using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nerves.ComplianceCn.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "compliance");

            migrationBuilder.CreateTable(
                name: "consent",
                schema: "compliance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "retention_policy",
                schema: "compliance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    LegalBasis = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_policy", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_consent_SubjectId_Purpose",
                schema: "compliance",
                table: "consent",
                columns: new[] { "SubjectId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_retention_policy_Category",
                schema: "compliance",
                table: "retention_policy",
                column: "Category",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consent",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "retention_policy",
                schema: "compliance");
        }
    }
}
