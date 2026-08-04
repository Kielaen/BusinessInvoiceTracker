namespace Invoice.Infrastructure.Persistence;

public sealed class NotificationDelivery
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Guid InvoiceId { get; set; }
    public string WorkflowGeneration { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Status { get; set; } = NotificationDeliveryStatuses.Pending;
    public string ProviderMessageId { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public int AttemptCycleStart { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public DateTimeOffset? FailedAtUtc { get; set; }
    public string? LastError { get; set; }
}

public static class NotificationDeliveryStatuses
{
    public const string Pending = "Pending";
    public const string Delivered = "Delivered";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}
