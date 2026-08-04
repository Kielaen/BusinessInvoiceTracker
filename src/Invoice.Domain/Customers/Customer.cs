using Invoice.Domain.Common;

namespace Invoice.Domain.Customers;

public sealed class Customer
{
    private Customer()
    {
    }

    private Customer(
        Guid id,
        Guid businessId,
        string name,
        EmailAddress email,
        string? phone,
        string billingAddressLine1,
        string? billingAddressLine2,
        string billingCity,
        string billingPostalCode,
        string billingCountryCode,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        BusinessId = businessId;
        SetDetails(
            name,
            email,
            phone,
            billingAddressLine1,
            billingAddressLine2,
            billingCity,
            billingPostalCode,
            billingCountryCode);
        IsActive = true;
        Version = Guid.NewGuid();
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid BusinessId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string BillingAddressLine1 { get; private set; } = string.Empty;
    public string? BillingAddressLine2 { get; private set; }
    public string BillingCity { get; private set; } = string.Empty;
    public string BillingPostalCode { get; private set; } = string.Empty;
    public string BillingCountryCode { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public Guid Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Customer Create(
        Guid id,
        Guid businessId,
        string name,
        EmailAddress email,
        string? phone,
        string billingAddressLine1,
        string? billingAddressLine2,
        string billingCity,
        string billingPostalCode,
        string billingCountryCode,
        DateTimeOffset now)
    {
        if (id == Guid.Empty || businessId == Guid.Empty)
        {
            throw new DomainRuleViolationException(
                "customer.identity_required",
                "Customer and business identifiers are required.");
        }

        ArgumentNullException.ThrowIfNull(email);
        return new Customer(
            id,
            businessId,
            name,
            email,
            phone,
            billingAddressLine1,
            billingAddressLine2,
            billingCity,
            billingPostalCode,
            billingCountryCode,
            now);
    }

    public void Update(
        string name,
        EmailAddress email,
        string? phone,
        string billingAddressLine1,
        string? billingAddressLine2,
        string billingCity,
        string billingPostalCode,
        string billingCountryCode,
        DateTimeOffset now)
    {
        SetDetails(
            name,
            email,
            phone,
            billingAddressLine1,
            billingAddressLine2,
            billingCity,
            billingPostalCode,
            billingCountryCode);
        Touch(now);
    }

    public bool HasSameDetails(Customer other) =>
        Name == other.Name &&
        Email == other.Email &&
        Phone == other.Phone &&
        BillingAddressLine1 == other.BillingAddressLine1 &&
        BillingAddressLine2 == other.BillingAddressLine2 &&
        BillingCity == other.BillingCity &&
        BillingPostalCode == other.BillingPostalCode &&
        BillingCountryCode == other.BillingCountryCode;

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        Touch(now);
    }

    private void SetName(string name)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 160)
        {
            throw new DomainRuleViolationException(
                "customer.name_invalid",
                "Customer name is required and cannot exceed 160 characters.");
        }

        Name = normalized;
    }

    private void SetDetails(
        string name,
        EmailAddress email,
        string? phone,
        string billingAddressLine1,
        string? billingAddressLine2,
        string billingCity,
        string billingPostalCode,
        string billingCountryCode)
    {
        ArgumentNullException.ThrowIfNull(email);
        SetName(name);
        Email = email.Value;
        Phone = NormalizeOptional(phone, 32, "customer.phone_invalid");
        BillingAddressLine1 = NormalizeRequired(
            billingAddressLine1,
            200,
            "customer.billing_address_invalid");
        BillingAddressLine2 = NormalizeOptional(
            billingAddressLine2,
            200,
            "customer.billing_address_invalid");
        BillingCity = NormalizeRequired(
            billingCity,
            120,
            "customer.billing_city_invalid");
        BillingPostalCode = NormalizeRequired(
            billingPostalCode,
            32,
            "customer.billing_postal_code_invalid");
        BillingCountryCode = NormalizeCountryCode(billingCountryCode);
    }

    private static string NormalizeRequired(
        string value,
        int maximumLength,
        string code)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Length > maximumLength)
        {
            throw new DomainRuleViolationException(
                code,
                $"A value is required and cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value,
        int maximumLength,
        string code)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }
        if (normalized.Length > maximumLength)
        {
            throw new DomainRuleViolationException(
                code,
                $"The value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static string NormalizeCountryCode(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (normalized is null ||
            normalized.Length != 2 ||
            normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw new DomainRuleViolationException(
                "customer.billing_country_invalid",
                "Billing country must be a two-letter country code.");
        }

        return normalized;
    }

    private void Touch(DateTimeOffset now)
    {
        Version = Guid.NewGuid();
        UpdatedAtUtc = now;
    }
}
