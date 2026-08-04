namespace Invoice.Infrastructure.Persistence;

public sealed class NotificationAttempt
{
    public Guid Id { get; set; }
    public Guid DeliveryId { get; set; }
    public int AttemptNumber { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string Outcome { get; set; } = NotificationAttemptOutcomes.Started;
    public string? Error { get; set; }
}

public static class NotificationAttemptOutcomes
{
    public const string Started = "Started";
    public const string Delivered = "Delivered";
    public const string Reconciled = "Reconciled";
    public const string TransientFailure = "TransientFailure";
    public const string PermanentFailure = "PermanentFailure";
}
