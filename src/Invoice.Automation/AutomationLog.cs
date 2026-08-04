namespace Invoice.Automation;

internal static partial class AutomationLog
{
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Ignored duplicate walking-skeleton message {MessageId}.")]
    public static partial void Duplicate(ILogger logger, Guid messageId);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Processed walking-skeleton message {MessageId} for probe {ProbeId}.")]
    public static partial void Processed(
        ILogger logger,
        Guid messageId,
        Guid probeId);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Warning,
        Message = "Discarded malformed walking-skeleton message {CloudEventId}.")]
    public static partial void InvalidWalkingSkeleton(
        ILogger logger,
        string cloudEventId);

    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Information,
        Message = "Accepted lifecycle event {MessageId} for business {BusinessId}, invoice {InvoiceId}, type {EventType}.")]
    public static partial void AcceptedLifecycle(
        ILogger logger,
        Guid messageId,
        Guid businessId,
        Guid invoiceId,
        string eventType);

    [LoggerMessage(
        EventId = 8002,
        Level = LogLevel.Information,
        Message = "Ignored duplicate lifecycle event {MessageId}.")]
    public static partial void DuplicateLifecycle(
        ILogger logger,
        Guid messageId);

    [LoggerMessage(
        EventId = 8003,
        Level = LogLevel.Warning,
        Message = "Rejected invalid lifecycle CloudEvent {CloudEventId} of type {EventType}.")]
    public static partial void InvalidLifecycle(
        ILogger logger,
        string cloudEventId,
        string eventType);

    [LoggerMessage(
        EventId = 8004,
        Level = LogLevel.Error,
        Message = "Persisted lifecycle dead letter {CloudEventId} of type {EventType} for operator action.")]
    public static partial void DeadLettered(
        ILogger logger,
        string cloudEventId,
        string eventType);
}
