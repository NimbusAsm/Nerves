using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nerves.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LegalEntityStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisabledAt",
                schema: "identity",
                table: "legal_entity",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "identity",
                table: "legal_entity",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisabledAt",
                schema: "identity",
                table: "legal_entity");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "identity",
                table: "legal_entity");
        }
    }
}
