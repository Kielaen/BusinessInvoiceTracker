namespace Invoice.Infrastructure.Persistence;

public sealed class InvoiceNumberSequence
{
    public Guid BusinessId { get; init; }
    public long NextValue { get; set; }
}
