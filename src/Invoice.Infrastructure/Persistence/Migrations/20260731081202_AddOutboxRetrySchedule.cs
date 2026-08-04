using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Invoice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRetrySchedule : Migration
    {
        private static readonly string[] ScheduledOutboxIndexColumns =
            ["PublishedAtUtc", "NextAttemptAtUtc", "OccurredAtUtc"];

        private static readonly string[] OriginalOutboxIndexColumns =
            ["PublishedAtUtc", "OccurredAtUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_PublishedAtUtc_OccurredAtUtc",
                table: "outbox_messages");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAtUtc",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_PublishedAtUtc_NextAttemptAtUtc_OccurredAtU~",
                table: "outbox_messages",
                columns: ScheduledOutboxIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_PublishedAtUtc_NextAttemptAtUtc_OccurredAtU~",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                table: "outbox_messages");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_PublishedAtUtc_OccurredAtUtc",
                table: "outbox_messages",
                columns: OriginalOutboxIndexColumns);
        }
    }
}
