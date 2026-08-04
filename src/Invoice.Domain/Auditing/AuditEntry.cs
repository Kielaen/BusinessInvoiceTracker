namespace Invoice.Domain.Auditing;

public sealed class AuditEntry
{
    private AuditEntry()
    {
    }

    public AuditEntry(
        Guid id,
        Guid businessId,
        string aggregateType,
        Guid aggregateId,
        string action,
        Guid? actorUserId,
        DateTimeOffset occurredAtUtc,
        string details)
    {
        Id = id;
        BusinessId = businessId;
        AggregateType = aggregateType;
        AggregateId = aggregateId;
        Action = action;
        ActorUserId = actorUserId;
        OccurredAtUtc = occurredAtUtc;
        Details = details;
    }

    public Guid Id { get; private set; }
    public Guid BusinessId { get; private set; }
    public string AggregateType { get; private set; } = string.Empty;
    public Guid AggregateId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public Guid? ActorUserId { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string Details { get; private set; } = "{}";
}
