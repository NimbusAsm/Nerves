using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nerves.ComplianceCn.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetentionOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_event",
                schema: "compliance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TargetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_event", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_event_DispatchedAt_OccurredAt",
                schema: "compliance",
                table: "outbox_event",
                columns: new[] { "DispatchedAt", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_event",
                schema: "compliance");
        }
    }
}
