namespace Invoice.Application;

public sealed record WalkingSkeletonEvent(
    Guid MessageId,
    Guid ProbeId,
    string RequestedBy,
    DateTimeOffset OccurredAtUtc);
