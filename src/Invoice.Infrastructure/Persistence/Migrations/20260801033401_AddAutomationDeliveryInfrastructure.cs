using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Invoice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationDeliveryInfrastructure : Migration
    {
        private static readonly string[] DeadLetterIndexColumns = ["ResolvedAtUtc", "ReceivedAtUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "outbox_messages",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseOwner",
                table: "outbox_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseUntilUtc",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "automation_dead_letters",
                columns: table => new
                {
                    MessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_dead_letters", x => x.MessageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_automation_dead_letters_ResolvedAtUtc_ReceivedAtUtc",
                table: "automation_dead_letters",
                columns: DeadLetterIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "automation_dead_letters");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LeaseOwner",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LeaseUntilUtc",
                table: "outbox_messages");
        }
    }
}
