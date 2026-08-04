using Invoice.Domain.Financial;

namespace Invoice.Domain.Invoices;

public sealed record InvoiceLineDraft(
    string Description,
    decimal Quantity,
    Money UnitPrice,
    TaxRate TaxRate);
