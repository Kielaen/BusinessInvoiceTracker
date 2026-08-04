using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Invoice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialWalkingSkeleton : Migration
    {
        private static readonly string[] OutboxPublishIndexColumns =
            ["PublishedAtUtc", "OccurredAtUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "automation_receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProbeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_receipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "delivery_probes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_probes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => x.MessageId);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishAttempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_automation_receipts_MessageId",
                table: "automation_receipts",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_automation_receipts_ProbeId",
                table: "automation_receipts",
                column: "ProbeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_PublishedAtUtc_OccurredAtUtc",
                table: "outbox_messages",
                columns: OutboxPublishIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "automation_receipts");

            migrationBuilder.DropTable(
                name: "delivery_probes");

            migrationBuilder.DropTable(
                name: "inbox_messages");

            migrationBuilder.DropTable(
                name: "outbox_messages");
        }
    }
}
