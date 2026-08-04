using Invoice.Domain.Common;

namespace Invoice.Domain.Financial;

public sealed record CurrencyCode
{
    private CurrencyCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static CurrencyCode Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length != 3 ||
            value.Any(character => character is < 'A' or > 'Z'))
        {
            throw new DomainRuleViolationException(
                "currency.invalid",
                "Currency must contain exactly three uppercase ASCII letters.");
        }

        return new CurrencyCode(value);
    }

    public override string ToString() => Value;
}
