using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Invoice.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerContactDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingAddressLine1",
                table: "customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BillingAddressLine2",
                table: "customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCity",
                table: "customers",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BillingCountryCode",
                table: "customers",
                type: "character(2)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BillingPostalCode",
                table: "customers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "customers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_customers_billing_required",
                table: "customers",
                sql: "length(btrim(\"BillingAddressLine1\")) > 0 AND length(btrim(\"BillingCity\")) > 0 AND length(btrim(\"BillingPostalCode\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_customers_country_code",
                table: "customers",
                sql: "\"BillingCountryCode\" ~ '^[A-Z]{2}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_customers_billing_required",
                table: "customers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_customers_country_code",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "BillingAddressLine1",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "BillingAddressLine2",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "BillingCity",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "BillingCountryCode",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "BillingPostalCode",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "customers");
        }
    }
}
