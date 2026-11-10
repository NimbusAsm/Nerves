using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nerves.Notification.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelsAndEventConsumer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Channel",
                schema: "notification",
                table: "notification_message",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceEventId",
                schema: "notification",
                table: "notification_message",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "event_consumer_state",
                schema: "notification",
                columns: table => new
                {
                    Consumer = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastOccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_consumer_state", x => x.Consumer);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_message_Channel",
                schema: "notification",
                table: "notification_message",
                column: "Channel");

            migrationBuilder.CreateIndex(
                name: "IX_notification_message_SourceEventId",
                schema: "notification",
                table: "notification_message",
                column: "SourceEventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_consumer_state",
                schema: "notification");

            migrationBuilder.DropIndex(
                name: "IX_notification_message_Channel",
                schema: "notification",
                table: "notification_message");

            migrationBuilder.DropIndex(
                name: "IX_notification_message_SourceEventId",
                schema: "notification",
                table: "notification_message");

            migrationBuilder.DropColumn(
                name: "Channel",
                schema: "notification",
                table: "notification_message");

            migrationBuilder.DropColumn(
                name: "SourceEventId",
                schema: "notification",
                table: "notification_message");
        }
    }
}
