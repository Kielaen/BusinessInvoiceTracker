namespace Invoice.Infrastructure.Persistence;

public sealed class DeliveryProbe
{
    public Guid Id { get; init; }
    public required string RequestedBy { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}
