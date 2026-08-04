using Invoice.Domain.Common;
using Invoice.Domain.Financial;

namespace Invoice.Domain.Invoices;

public sealed class InvoiceLine
{
    private InvoiceLine()
    {
    }

    private InvoiceLine(
        Guid id,
        Guid businessId,
        Guid invoiceId,
        InvoiceLineDraft draft,
        CurrencyCode invoiceCurrency,
        int position)
    {
        if (FinancialMath.GetScale(draft.Quantity) > FinancialMath.QuantityScale)
        {
            throw new DomainRuleViolationException(
                "invoice_line.quantity_scale",
                "Quantity supports at most four decimal places.");
        }

        if (draft.Quantity <= 0m || draft.Quantity > 1_000_000m)
        {
            throw new DomainRuleViolationException(
                "invoice_line.quantity_invalid",
                "Quantity must be positive and within the supported range.");
        }

        var description = draft.Description?.Trim();
        if (string.IsNullOrWhiteSpace(description) || description.Length > 500)
        {
            throw new DomainRuleViolationException(
                "invoice_line.description_invalid",
                "Line description is required and cannot exceed 500 characters.");
        }

        draft.UnitPrice.RequireSameCurrency(Money.Zero(invoiceCurrency));

        Id = id;
        BusinessId = businessId;
        InvoiceId = invoiceId;
        Position = position;
        Description = description;
        Quantity = draft.Quantity;
        Currency = invoiceCurrency.Value;
        UnitPriceAmount = draft.UnitPrice.Amount;
        TaxRatePercentage = draft.TaxRate.Percentage;
        NetAmount = FinancialMath.RoundMoney(
            checked(Quantity * UnitPriceAmount));
        TaxAmount = FinancialMath.RoundMoney(
            checked(NetAmount * TaxRatePercentage / 100m));
        TotalAmount = checked(NetAmount + TaxAmount);

        if (TotalAmount > FinancialMath.MaximumMoney)
        {
            throw new DomainRuleViolationException(
                "invoice_line.total_out_of_range",
                "Line total exceeds the supported monetary range.");
        }
    }

    public Guid Id { get; private set; }
    public Guid BusinessId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public int Position { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public decimal UnitPriceAmount { get; private set; }
    public decimal TaxRatePercentage { get; private set; }
    public decimal NetAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }

    internal static InvoiceLine Create(
        Guid businessId,
        Guid invoiceId,
        InvoiceLineDraft draft,
        CurrencyCode invoiceCurrency,
        int position) =>
        new(
            Guid.NewGuid(),
            businessId,
            invoiceId,
            draft,
            invoiceCurrency,
            position);
}
