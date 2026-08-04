namespace Invoice.Infrastructure.Persistence;

public sealed class InboxMessage
{
    public Guid MessageId { get; init; }
    public DateTimeOffset ReceivedAtUtc { get; init; }
}
