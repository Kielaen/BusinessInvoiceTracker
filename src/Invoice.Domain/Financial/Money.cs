using Invoice.Domain.Common;

namespace Invoice.Domain.Financial;

public sealed record Money
{
    private Money(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }
    public CurrencyCode Currency { get; }

    public static Money Create(decimal amount, CurrencyCode currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        if (amount < 0 || amount > FinancialMath.MaximumMoney)
        {
            throw new DomainRuleViolationException(
                "money.out_of_range",
                "Money must be non-negative and within the supported range.");
        }

        if (FinancialMath.GetScale(amount) > FinancialMath.MoneyScale)
        {
            throw new DomainRuleViolationException(
                "money.scale",
                "Money supports at most two decimal places.");
        }

        return new Money(amount, currency);
    }

    public static Money Zero(CurrencyCode currency) => Create(0m, currency);

    public Money Add(Money other)
    {
        RequireSameCurrency(other);
        return Create(checked(Amount + other.Amount), Currency);
    }

    public Money Subtract(Money other)
    {
        RequireSameCurrency(other);
        return Create(checked(Amount - other.Amount), Currency);
    }

    public void RequireSameCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Currency != other.Currency)
        {
            throw new DomainRuleViolationException(
                "money.currency_mismatch",
                "Money values must use the same currency.");
        }
    }
}
