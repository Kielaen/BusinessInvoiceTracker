using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Invoice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationRetryCycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCycleStart",
                table: "notification_deliveries",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptCycleStart",
                table: "notification_deliveries");
        }
    }
}
