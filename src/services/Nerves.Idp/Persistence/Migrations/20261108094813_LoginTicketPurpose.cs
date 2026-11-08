using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nerves.Idp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LoginTicketPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                schema: "idp",
                table: "login_ticket",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Purpose",
                schema: "idp",
                table: "login_ticket");
        }
    }
}
