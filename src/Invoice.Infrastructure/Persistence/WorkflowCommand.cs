namespace Invoice.Infrastructure.Persistence;

public sealed class WorkflowCommand
{
    public Guid Id { get; init; }
    public Guid EventMessageId { get; init; }
    public Guid BusinessId { get; init; }
    public Guid InvoiceId { get; init; }
    public required string Kind { get; init; }
    public required string Payload { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public DateTimeOffset? DispatchedAtUtc { get; set; }
    public DateTimeOffset? FailedAtUtc { get; set; }
    public int Attempts { get; set; }
    public Guid? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntilUtc { get; set; }
    public string? LastError { get; set; }
}
