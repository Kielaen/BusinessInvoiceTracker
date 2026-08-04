using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Invoice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOverdueNotificationDelivery : Migration
    {
        private static readonly string[] AttemptIndexColumns = ["DeliveryId", "AttemptNumber"];
        private static readonly string[] InvoiceIndexColumns = ["InvoiceId", "WorkflowGeneration"];
        private static readonly string[] StatusIndexColumns = ["Status", "CreatedAtUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowGeneration = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_deliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notification_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_attempts_notification_deliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "notification_deliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_attempts_DeliveryId_AttemptNumber",
                table: "notification_attempts",
                columns: AttemptIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_InvoiceId_WorkflowGeneration",
                table: "notification_deliveries",
                columns: InvoiceIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_Status_CreatedAtUtc",
                table: "notification_deliveries",
                columns: StatusIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_attempts");

            migrationBuilder.DropTable(
                name: "notification_deliveries");
        }
    }
}
