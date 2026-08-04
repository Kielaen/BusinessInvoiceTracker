using Invoice.Domain.Auditing;
using Invoice.Domain.Customers;
using Invoice.Domain.Invoices;
using Microsoft.EntityFrameworkCore;

namespace Invoice.Infrastructure.Persistence;

internal static class DomainModelConfiguration
{
    public static void ConfigureDomainModel(this ModelBuilder builder)
    {
        ConfigureCustomers(builder);
        ConfigureInvoices(builder);
        ConfigureInvoiceLines(builder);
        ConfigurePayments(builder);
        ConfigureAudit(builder);
        ConfigureInvoiceNumberSequences(builder);
    }

    private static void ConfigureCustomers(ModelBuilder builder)
    {
        builder.Entity<Customer>(entity =>
        {
            entity.ToTable(
                "customers",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_customers_name_not_blank",
                        "length(btrim(\"Name\")) > 0");
                    table.HasCheckConstraint(
                        "CK_customers_email_not_blank",
                        "length(btrim(\"Email\")) > 0");
                    table.HasCheckConstraint(
                        "CK_customers_billing_required",
                        "length(btrim(\"BillingAddressLine1\")) > 0 AND " +
                        "length(btrim(\"BillingCity\")) > 0 AND " +
                        "length(btrim(\"BillingPostalCode\")) > 0");
                    table.HasCheckConstraint(
                        "CK_customers_country_code",
                        "\"BillingCountryCode\" ~ '^[A-Z]{2}$'");
                });
            entity.HasKey(customer => customer.Id);
            entity.HasAlternateKey(customer => new
            {
                customer.BusinessId,
                customer.Id
            });
            entity.Property(customer => customer.Name).HasMaxLength(160);
            entity.Property(customer => customer.Email).HasMaxLength(254);
            entity.Property(customer => customer.Phone).HasMaxLength(32);
            entity.Property(customer => customer.BillingAddressLine1)
                .HasMaxLength(200);
            entity.Property(customer => customer.BillingAddressLine2)
                .HasMaxLength(200);
            entity.Property(customer => customer.BillingCity).HasMaxLength(120);
            entity.Property(customer => customer.BillingPostalCode)
                .HasMaxLength(32);
            entity.Property(customer => customer.BillingCountryCode)
                .HasColumnType("character(2)");
            entity.Property(customer => customer.Version).IsConcurrencyToken();
            entity.HasIndex(customer => new
            {
                customer.BusinessId,
                customer.Email
            }).IsUnique();
            entity.HasOne<Domain.Businesses.Business>()
                .WithMany()
                .HasForeignKey(customer => customer.BusinessId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureInvoices(ModelBuilder builder)
    {
        builder.Entity<Domain.Invoices.Invoice>(entity =>
        {
            entity.ToTable(
                "invoices",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_invoices_dates",
                        "\"DueDate\" >= \"IssueDate\"");
                    table.HasCheckConstraint(
                        "CK_invoices_amounts",
                        "\"SubtotalAmount\" >= 0 AND \"TaxAmount\" >= 0 AND " +
                        "\"TotalAmount\" >= 0 AND \"PaidAmount\" >= 0 AND " +
                        "\"PaidAmount\" <= \"TotalAmount\" AND " +
                        "\"TotalAmount\" = \"SubtotalAmount\" + \"TaxAmount\"");
                    table.HasCheckConstraint(
                        "CK_invoices_currency",
                        "length(\"Currency\") = 3 AND \"Currency\" = upper(\"Currency\")");
                    table.HasCheckConstraint(
                        "CK_invoices_status_consistency",
                        "(\"Status\" = 'Draft' AND \"Number\" IS NULL AND " +
                        "\"NumberSequence\" IS NULL AND \"IssuedAtUtc\" IS NULL AND " +
                        "\"DueAtUtc\" IS NULL AND \"PaidAmount\" = 0) OR " +
                        "(\"Status\" = 'Issued' AND \"Number\" IS NOT NULL AND " +
                        "\"NumberSequence\" IS NOT NULL AND \"IssuedAtUtc\" IS NOT NULL AND " +
                        "\"DueAtUtc\" IS NOT NULL AND \"PaidAmount\" = 0) OR " +
                        "(\"Status\" = 'PartiallyPaid' AND \"Number\" IS NOT NULL AND " +
                        "\"NumberSequence\" IS NOT NULL AND \"IssuedAtUtc\" IS NOT NULL AND " +
                        "\"DueAtUtc\" IS NOT NULL AND \"PaidAmount\" > 0 AND " +
                        "\"PaidAmount\" < \"TotalAmount\") OR " +
                        "(\"Status\" = 'Paid' AND \"Number\" IS NOT NULL AND " +
                        "\"NumberSequence\" IS NOT NULL AND \"IssuedAtUtc\" IS NOT NULL AND " +
                        "\"DueAtUtc\" IS NOT NULL AND \"PaidAmount\" = \"TotalAmount\") OR " +
                        "(\"Status\" = 'Overdue' AND \"Number\" IS NOT NULL AND " +
                        "\"NumberSequence\" IS NOT NULL AND \"IssuedAtUtc\" IS NOT NULL AND " +
                        "\"DueAtUtc\" IS NOT NULL AND \"PaidAmount\" < \"TotalAmount\") OR " +
                        "(\"Status\" = 'Voided' AND \"PaidAmount\" = 0 AND " +
                        "\"VoidedAtUtc\" IS NOT NULL)");
                });
            entity.HasKey(invoice => invoice.Id);
            entity.HasAlternateKey(invoice => new
            {
                invoice.BusinessId,
                invoice.Id
            });
            entity.Property(invoice => invoice.Number).HasMaxLength(32);
            entity.Property(invoice => invoice.Currency)
                .HasColumnType("character(3)");
            entity.Property(invoice => invoice.Status)
                .HasConversion<string>()
                .HasMaxLength(32);
            entity.Property(invoice => invoice.SubtotalAmount)
                .HasPrecision(18, 2);
            entity.Property(invoice => invoice.TaxAmount)
                .HasPrecision(18, 2);
            entity.Property(invoice => invoice.TotalAmount)
                .HasPrecision(18, 2);
            entity.Property(invoice => invoice.PaidAmount)
                .HasPrecision(18, 2);
            entity.Property(invoice => invoice.Version).IsConcurrencyToken();
            entity.Ignore(invoice => invoice.OutstandingAmount);
            entity.HasIndex(invoice => new
            {
                invoice.BusinessId,
                invoice.Number
            }).IsUnique();
            entity.HasIndex(invoice => new
            {
                invoice.BusinessId,
                invoice.Status,
                invoice.DueAtUtc
            });
            entity.HasIndex(invoice => new
            {
                invoice.BusinessId,
                invoice.UpdatedAtUtc,
                invoice.Id
            });
            entity.HasOne<Domain.Businesses.Business>()
                .WithMany()
                .HasForeignKey(invoice => invoice.BusinessId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(invoice => new
                {
                    invoice.BusinessId,
                    invoice.CustomerId
                })
                .HasPrincipalKey(customer => new
                {
                    customer.BusinessId,
                    customer.Id
                })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(invoice => invoice.Lines)
                .WithOne()
                .HasForeignKey(line => new
                {
                    line.BusinessId,
                    line.InvoiceId
                })
                .HasPrincipalKey(invoice => new
                {
                    invoice.BusinessId,
                    invoice.Id
                })
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(invoice => invoice.Lines)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(invoice => invoice.Payments)
                .WithOne()
                .HasForeignKey(payment => new
                {
                    payment.BusinessId,
                    payment.InvoiceId
                })
                .HasPrincipalKey(invoice => new
                {
                    invoice.BusinessId,
                    invoice.Id
                })
                .OnDelete(DeleteBehavior.Restrict);
            entity.Navigation(invoice => invoice.Payments)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    private static void ConfigureInvoiceLines(ModelBuilder builder)
    {
        builder.Entity<InvoiceLine>(entity =>
        {
            entity.ToTable(
                "invoice_lines",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_invoice_lines_quantity",
                        "\"Quantity\" > 0");
                    table.HasCheckConstraint(
                        "CK_invoice_lines_amounts",
                        "\"UnitPriceAmount\" >= 0 AND \"NetAmount\" >= 0 AND " +
                        "\"TaxAmount\" >= 0 AND " +
                        "\"TotalAmount\" = \"NetAmount\" + \"TaxAmount\"");
                    table.HasCheckConstraint(
                        "CK_invoice_lines_tax",
                        "\"TaxRatePercentage\" >= 0 AND \"TaxRatePercentage\" <= 100");
                });
            entity.HasKey(line => line.Id);
            entity.Property(line => line.Description).HasMaxLength(500);
            entity.Property(line => line.Currency)
                .HasColumnType("character(3)");
            entity.Property(line => line.Quantity).HasPrecision(18, 4);
            entity.Property(line => line.UnitPriceAmount).HasPrecision(18, 2);
            entity.Property(line => line.TaxRatePercentage).HasPrecision(7, 4);
            entity.Property(line => line.NetAmount).HasPrecision(18, 2);
            entity.Property(line => line.TaxAmount).HasPrecision(18, 2);
            entity.Property(line => line.TotalAmount).HasPrecision(18, 2);
            entity.HasIndex(line => new
            {
                line.BusinessId,
                line.InvoiceId,
                line.Position
            }).IsUnique();
        });
    }

    private static void ConfigurePayments(ModelBuilder builder)
    {
        builder.Entity<Payment>(entity =>
        {
            entity.ToTable(
                "payments",
                table => table.HasCheckConstraint(
                    "CK_payments_amount_positive",
                    "\"Amount\" > 0"));
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.Property(payment => payment.Currency)
                .HasColumnType("character(3)");
            entity.Property(payment => payment.IdempotencyKey)
                .HasMaxLength(100);
            entity.HasIndex(payment => new
            {
                payment.BusinessId,
                payment.IdempotencyKey
            }).IsUnique();
        });
    }

    private static void ConfigureAudit(ModelBuilder builder)
    {
        builder.Entity<AuditEntry>(entity =>
        {
            entity.ToTable("audit_entries");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.AggregateType).HasMaxLength(80);
            entity.Property(entry => entry.Action).HasMaxLength(120);
            entity.Property(entry => entry.Details).HasColumnType("jsonb");
            entity.HasIndex(entry => new
            {
                entry.BusinessId,
                entry.AggregateId,
                entry.OccurredAtUtc
            });
            entity.HasIndex(entry => new
            {
                entry.BusinessId,
                entry.OccurredAtUtc,
                entry.Id
            });
            entity.HasOne<Domain.Businesses.Business>()
                .WithMany()
                .HasForeignKey(entry => entry.BusinessId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureInvoiceNumberSequences(ModelBuilder builder)
    {
        builder.Entity<InvoiceNumberSequence>(entity =>
        {
            entity.ToTable(
                "invoice_number_sequences",
                table => table.HasCheckConstraint(
                    "CK_invoice_number_sequences_next",
                    "\"NextValue\" >= 1 AND \"NextValue\" <= 100000000"));
            entity.HasKey(sequence => sequence.BusinessId);
            entity.HasOne<Domain.Businesses.Business>()
                .WithOne()
                .HasForeignKey<InvoiceNumberSequence>(
                    sequence => sequence.BusinessId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
