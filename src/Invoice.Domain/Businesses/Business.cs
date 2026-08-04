namespace Invoice.Domain.Businesses;

public sealed class Business
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public ICollection<BusinessMembership> Memberships { get; } =
        new List<BusinessMembership>();
}
