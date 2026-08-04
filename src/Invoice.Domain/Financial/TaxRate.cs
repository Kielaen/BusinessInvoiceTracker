using Invoice.Domain.Common;

namespace Invoice.Domain.Financial;

public sealed record TaxRate
{
    private TaxRate(decimal percentage)
    {
        Percentage = percentage;
    }

    public decimal Percentage { get; }

    public static TaxRate Create(decimal percentage)
    {
        if (percentage is < 0m or > 100m)
        {
            throw new DomainRuleViolationException(
                "tax_rate.out_of_range",
                "Tax rate must be between zero and one hundred percent.");
        }

        if (FinancialMath.GetScale(percentage) > FinancialMath.TaxRateScale)
        {
            throw new DomainRuleViolationException(
                "tax_rate.scale",
                "Tax rate supports at most four decimal places.");
        }

        return new TaxRate(percentage);
    }
}
