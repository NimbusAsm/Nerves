using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nerves.Idp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistIntegrationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_setting",
                schema: "idp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_setting", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_integration_setting_Key",
                schema: "idp",
                table: "integration_setting",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_setting",
                schema: "idp");
        }
    }
}
