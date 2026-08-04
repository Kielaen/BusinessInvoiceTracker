using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Invoice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableWorkflowCommands : Migration
    {
        private static readonly string[] DispatchIndexColumns =
            ["DispatchedAtUtc", "FailedAtUtc", "NextAttemptAtUtc", "LeaseUntilUtc"];

        private static readonly string[] InvoiceIndexColumns = ["InvoiceId", "CreatedAtUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workflow_commands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DispatchedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LeaseOwner = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_commands", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_commands_DispatchedAtUtc_FailedAtUtc_NextAttemptAt~",
                table: "workflow_commands",
                columns: DispatchIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_commands_EventMessageId",
                table: "workflow_commands",
                column: "EventMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_commands_InvoiceId_CreatedAtUtc",
                table: "workflow_commands",
                columns: InvoiceIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workflow_commands");
        }
    }
}
