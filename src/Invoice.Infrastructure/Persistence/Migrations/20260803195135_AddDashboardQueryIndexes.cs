using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Invoice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardQueryIndexes : Migration
    {
        private static readonly string[] InvoiceOrderColumns =
            ["BusinessId", "UpdatedAtUtc", "Id"];
        private static readonly string[] AuditOrderColumns =
            ["BusinessId", "OccurredAtUtc", "Id"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_invoices_BusinessId_UpdatedAtUtc_Id",
                table: "invoices",
                columns: InvoiceOrderColumns);

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_BusinessId_OccurredAtUtc_Id",
                table: "audit_entries",
                columns: AuditOrderColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_invoices_BusinessId_UpdatedAtUtc_Id",
                table: "invoices");

            migrationBuilder.DropIndex(
                name: "IX_audit_entries_BusinessId_OccurredAtUtc_Id",
                table: "audit_entries");
        }
    }
}
