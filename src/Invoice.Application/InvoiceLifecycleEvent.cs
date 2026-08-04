namespace Invoice.Application;

public sealed record InvoiceLifecycleEvent(
    Guid MessageId,
    Guid BusinessId,
    Guid InvoiceId,
    string? InvoiceNumber,
    string Status,
    string Currency,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    DateTimeOffset? DueAtUtc,
    decimal? Amount,
    string? IdempotencyKey,
    DateTimeOffset OccurredAtUtc);
