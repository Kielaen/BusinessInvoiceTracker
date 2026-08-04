namespace Invoice.Domain.Businesses;

public sealed class BusinessMembership
{
    public Guid BusinessId { get; init; }
    public Guid UserId { get; init; }
    public BusinessRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public Business Business { get; init; } = null!;
}
