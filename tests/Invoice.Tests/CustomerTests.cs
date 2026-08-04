using Invoice.Domain.Common;
using Invoice.Domain.Customers;

namespace Invoice.Tests;

public sealed class CustomerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateNormalizesContactAndBillingDetails()
    {
        var customer = Create(
            name: "  Example Customer  ",
            email: "BILLING@EXAMPLE.TEST",
            phone: "  +27 11 555 0100  ",
            country: " za ");

        Assert.Equal("Example Customer", customer.Name);
        Assert.Equal("billing@example.test", customer.Email);
        Assert.Equal("+27 11 555 0100", customer.Phone);
        Assert.Equal("ZA", customer.BillingCountryCode);
        Assert.True(customer.IsActive);
        Assert.NotEqual(Guid.Empty, customer.Version);
    }

    [Fact]
    public void CreateRejectsMissingBillingFieldsAndInvalidCountry()
    {
        Assert.Throws<DomainRuleViolationException>(() => Create(address1: " "));
        Assert.Throws<DomainRuleViolationException>(() => Create(city: " "));
        Assert.Throws<DomainRuleViolationException>(() => Create(postalCode: " "));
        Assert.Throws<DomainRuleViolationException>(() => Create(country: "South Africa"));
    }

    [Fact]
    public void UpdateChangesVersionAndPreservesTenantIdentity()
    {
        var customer = Create();
        var originalVersion = customer.Version;
        var originalBusiness = customer.BusinessId;

        customer.Update(
            "Updated Customer",
            EmailAddress.Parse("updated@example.test"),
            null,
            "2 New Street",
            "Suite 4",
            "Cape Town",
            "8001",
            "ZA",
            Now.AddMinutes(1));

        Assert.NotEqual(originalVersion, customer.Version);
        Assert.Equal(originalBusiness, customer.BusinessId);
        Assert.Equal("Updated Customer", customer.Name);
        Assert.Equal("Cape Town", customer.BillingCity);
    }

    private static Customer Create(
        string name = "Example Customer",
        string email = "billing@example.test",
        string? phone = null,
        string address1 = "1 Main Road",
        string? address2 = null,
        string city = "Johannesburg",
        string postalCode = "2000",
        string country = "ZA") => Customer.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            name,
            EmailAddress.Parse(email),
            phone,
            address1,
            address2,
            city,
            postalCode,
            country,
            Now);
}
