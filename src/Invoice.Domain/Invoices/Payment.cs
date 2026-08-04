using Invoice.Domain.Financial;

namespace Invoice.Domain.Invoices;

public sealed class Payment
{
    private Payment()
    {
    }

    internal Payment(
        Guid id,
        Guid businessId,
        Guid invoiceId,
        Money amount,
        DateTimeOffset receivedAtUtc,
        string idempotencyKey)
    {
        Id = id;
        BusinessId = businessId;
        InvoiceId = invoiceId;
        Amount = amount.Amount;
        Currency = amount.Currency.Value;
        ReceivedAtUtc = receivedAtUtc;
        IdempotencyKey = idempotencyKey;
    }

    public Guid Id { get; private set; }
    public Guid BusinessId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
}
