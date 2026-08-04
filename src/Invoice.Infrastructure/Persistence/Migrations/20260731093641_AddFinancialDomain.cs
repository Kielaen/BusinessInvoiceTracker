using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Invoice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialDomain : Migration
    {
        private static readonly string[] BusinessIdIdColumns =
            ["BusinessId", "Id"];
        private static readonly string[] AuditIndexColumns =
            ["BusinessId", "AggregateId", "OccurredAtUtc"];
        private static readonly string[] CustomerEmailIndexColumns =
            ["BusinessId", "Email"];
        private static readonly string[] InvoiceLineIndexColumns =
            ["BusinessId", "InvoiceId", "Position"];
        private static readonly string[] InvoiceCustomerIndexColumns =
            ["BusinessId", "CustomerId"];
        private static readonly string[] InvoiceNumberIndexColumns =
            ["BusinessId", "Number"];
        private static readonly string[] InvoiceStatusIndexColumns =
            ["BusinessId", "Status", "DueAtUtc"];
        private static readonly string[] PaymentIdempotencyIndexColumns =
            ["BusinessId", "IdempotencyKey"];
        private static readonly string[] PaymentInvoiceIndexColumns =
            ["BusinessId", "InvoiceId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Details = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_entries_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                    table.UniqueConstraint("AK_customers_BusinessId_Id", x => new { x.BusinessId, x.Id });
                    table.CheckConstraint("CK_customers_email_not_blank", "length(btrim(\"Email\")) > 0");
                    table.CheckConstraint("CK_customers_name_not_blank", "length(btrim(\"Name\")) > 0");
                    table.ForeignKey(
                        name: "FK_customers_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_number_sequences",
                columns: table => new
                {
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    NextValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_number_sequences", x => x.BusinessId);
                    table.CheckConstraint("CK_invoice_number_sequences_next", "\"NextValue\" >= 1 AND \"NextValue\" <= 100000000");
                    table.ForeignKey(
                        name: "FK_invoice_number_sequences_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    NumberSequence = table.Column<long>(type: "bigint", nullable: true),
                    Currency = table.Column<string>(type: "character(3)", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SubtotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VoidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.Id);
                    table.UniqueConstraint("AK_invoices_BusinessId_Id", x => new { x.BusinessId, x.Id });
                    table.CheckConstraint("CK_invoices_amounts", "\"SubtotalAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"TotalAmount\" >= 0 AND \"PaidAmount\" >= 0 AND \"PaidAmount\" <= \"TotalAmount\" AND \"TotalAmount\" = \"SubtotalAmount\" + \"TaxAmount\"");
                    table.CheckConstraint("CK_invoices_currency", "length(\"Currency\") = 3 AND \"Currency\" = upper(\"Currency\")");
                    table.CheckConstraint("CK_invoices_dates", "\"DueDate\" >= \"IssueDate\"");
                    table.CheckConstraint("CK_invoices_status_consistency", "(\"Status\" = 'Draft' AND \"Number\" IS NULL AND \"NumberSequence\" IS NULL AND \"IssuedAtUtc\" IS NULL AND \"DueAtUtc\" IS NULL AND \"PaidAmount\" = 0) OR (\"Status\" = 'Issued' AND \"Number\" IS NOT NULL AND \"NumberSequence\" IS NOT NULL AND \"IssuedAtUtc\" IS NOT NULL AND \"DueAtUtc\" IS NOT NULL AND \"PaidAmount\" = 0) OR (\"Status\" = 'PartiallyPaid' AND \"Number\" IS NOT NULL AND \"NumberSequence\" IS NOT NULL AND \"IssuedAtUtc\" IS NOT NULL AND \"DueAtUtc\" IS NOT NULL AND \"PaidAmount\" > 0 AND \"PaidAmount\" < \"TotalAmount\") OR (\"Status\" = 'Paid' AND \"Number\" IS NOT NULL AND \"NumberSequence\" IS NOT NULL AND \"IssuedAtUtc\" IS NOT NULL AND \"DueAtUtc\" IS NOT NULL AND \"PaidAmount\" = \"TotalAmount\") OR (\"Status\" = 'Overdue' AND \"Number\" IS NOT NULL AND \"NumberSequence\" IS NOT NULL AND \"IssuedAtUtc\" IS NOT NULL AND \"DueAtUtc\" IS NOT NULL AND \"PaidAmount\" < \"TotalAmount\") OR (\"Status\" = 'Voided' AND \"PaidAmount\" = 0 AND \"VoidedAtUtc\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_invoices_businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_customers_BusinessId_CustomerId",
                        columns: x => new { x.BusinessId, x.CustomerId },
                        principalTable: "customers",
                        principalColumns: BusinessIdIdColumns,
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character(3)", nullable: false),
                    UnitPriceAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRatePercentage = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_lines", x => x.Id);
                    table.CheckConstraint("CK_invoice_lines_amounts", "\"UnitPriceAmount\" >= 0 AND \"NetAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"TotalAmount\" = \"NetAmount\" + \"TaxAmount\"");
                    table.CheckConstraint("CK_invoice_lines_quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_invoice_lines_tax", "\"TaxRatePercentage\" >= 0 AND \"TaxRatePercentage\" <= 100");
                    table.ForeignKey(
                        name: "FK_invoice_lines_invoices_BusinessId_InvoiceId",
                        columns: x => new { x.BusinessId, x.InvoiceId },
                        principalTable: "invoices",
                        principalColumns: BusinessIdIdColumns,
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character(3)", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.CheckConstraint("CK_payments_amount_positive", "\"Amount\" > 0");
                    table.ForeignKey(
                        name: "FK_payments_invoices_BusinessId_InvoiceId",
                        columns: x => new { x.BusinessId, x.InvoiceId },
                        principalTable: "invoices",
                        principalColumns: BusinessIdIdColumns,
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_BusinessId_AggregateId_OccurredAtUtc",
                table: "audit_entries",
                columns: AuditIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_customers_BusinessId_Email",
                table: "customers",
                columns: CustomerEmailIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_lines_BusinessId_InvoiceId_Position",
                table: "invoice_lines",
                columns: InvoiceLineIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_BusinessId_CustomerId",
                table: "invoices",
                columns: InvoiceCustomerIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_BusinessId_Number",
                table: "invoices",
                columns: InvoiceNumberIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_BusinessId_Status_DueAtUtc",
                table: "invoices",
                columns: InvoiceStatusIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_payments_BusinessId_IdempotencyKey",
                table: "payments",
                columns: PaymentIdempotencyIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_BusinessId_InvoiceId",
                table: "payments",
                columns: PaymentInvoiceIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entries");

            migrationBuilder.DropTable(
                name: "invoice_lines");

            migrationBuilder.DropTable(
                name: "invoice_number_sequences");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "customers");
        }
    }
}
