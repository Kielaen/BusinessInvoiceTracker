namespace Invoice.Domain.Financial;

public static class FinancialMath
{
    public const int MoneyScale = 2;
    public const int QuantityScale = 4;
    public const int TaxRateScale = 4;
    public const decimal MaximumMoney = 999_999_999_999_999.99m;

    public static decimal RoundMoney(decimal amount) =>
        decimal.Round(amount, MoneyScale, MidpointRounding.AwayFromZero);

    public static int GetScale(decimal value)
    {
        var bits = decimal.GetBits(value);
        return (bits[3] >> 16) & 0x7F;
    }
}
