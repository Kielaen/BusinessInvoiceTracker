namespace Invoice.Infrastructure.Persistence;

public sealed class AutomationReceipt
{
    public Guid Id { get; init; }
    public Guid MessageId { get; init; }
    public Guid ProbeId { get; init; }
    public required string RequestedBy { get; init; }
    public DateTimeOffset ProcessedAtUtc { get; init; }
}
