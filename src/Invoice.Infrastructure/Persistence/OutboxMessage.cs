namespace Invoice.Infrastructure.Persistence;

public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public required string Type { get; init; }
    public required string Payload { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public int PublishAttempts { get; set; }
    public Guid? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public string? LastError { get; set; }
}
