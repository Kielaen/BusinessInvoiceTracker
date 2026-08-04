namespace Invoice.Infrastructure.Persistence;

public sealed class AutomationDeadLetter
{
    public required string MessageId { get; init; }
    public required string EventType { get; init; }
    public required string Payload { get; init; }
    public DateTimeOffset ReceivedAtUtc { get; init; }
    public DateTimeOffset? ResolvedAtUtc { get; set; }
}
