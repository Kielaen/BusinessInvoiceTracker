using Invoice.Domain.Common;
using Invoice.Domain.Financial;
using Invoice.Domain.Invoices;

namespace Invoice.Tests;

public sealed class FinancialModelTests
{
    private static readonly CurrencyCode Zar = CurrencyCode.Parse("ZAR");
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CalculatesPerLineRoundingAndDeterministicTotals()
    {
        var invoice = CreateDraft();
        invoice.ReplaceLines(
        [
            new("Consulting", 2m, Money.Create(10m, Zar), TaxRate.Create(15m)),
            new("Fractional", 0.5m, Money.Create(10.01m, Zar), TaxRate.Create(0m))
        ],
        Now);

        Assert.Equal(25.01m, invoice.SubtotalAmount);
        Assert.Equal(3m, invoice.TaxAmount);
        Assert.Equal(28.01m, invoice.TotalAmount);
        Assert.Equal(invoice.TotalAmount, invoice.OutstandingAmount);
    }

    [Fact]
    public void RoundsMidpointsAwayFromZeroAtTheLineBoundary()
    {
        var invoice = CreateDraft();
        invoice.ReplaceLines(
        [
            new("Midpoint", 1.005m, Money.Create(1m, Zar), TaxRate.Create(0m))
        ],
        Now);

        Assert.Equal(1.01m, invoice.SubtotalAmount);
    }

    [Fact]
    public void RejectsInvalidFinancialInputsAndCurrencyMixing()
    {
        Assert.Throws<DomainRuleViolationException>(
            () => Money.Create(-0.01m, Zar));
        Assert.Throws<DomainRuleViolationException>(
            () => Money.Create(1.001m, Zar));
        Assert.Throws<DomainRuleViolationException>(
            () => TaxRate.Create(100.0001m));
        Assert.Throws<DomainRuleViolationException>(
            () => CurrencyCode.Parse("zar"));

        var invoice = CreateDraft();
        Assert.Throws<DomainRuleViolationException>(() => invoice.ReplaceLines(
        [
            new(
                "Wrong currency",
                1m,
                Money.Create(1m, CurrencyCode.Parse("USD")),
                TaxRate.Create(0m))
        ],
        Now));
    }

    [Fact]
    public void DueDateBecomesOverdueAtNextJohannesburgMidnight()
    {
        var dueAt = DueInstant.FromDueDate(new DateOnly(2026, 7, 31));

        Assert.Equal(
            new DateTimeOffset(2026, 7, 31, 22, 0, 0, TimeSpan.Zero),
            dueAt);
    }

    [Fact]
    public void DraftReplayComparisonIgnoresGeneratedLineIdsButDetectsChangedData()
    {
        var invoiceId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var original = Domain.Invoices.Invoice.CreateDraft(
            invoiceId, businessId, customerId, Zar,
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), Now);
        var replay = Domain.Invoices.Invoice.CreateDraft(
            invoiceId, businessId, customerId, Zar,
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), Now.AddMinutes(1));
        var changed = Domain.Invoices.Invoice.CreateDraft(
            invoiceId, businessId, customerId, Zar,
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), Now.AddMinutes(2));
        original.ReplaceLines(
            [new("Service", 2m, Money.Create(100m, Zar), TaxRate.Create(15m))],
            Now);
        replay.ReplaceLines(
            [new("Service", 2m, Money.Create(100m, Zar), TaxRate.Create(15m))],
            Now.AddMinutes(1));
        changed.ReplaceLines(
            [new("Service", 3m, Money.Create(100m, Zar), TaxRate.Create(15m))],
            Now.AddMinutes(2));

        Assert.True(original.HasSameDraft(replay));
        Assert.False(original.HasSameDraft(changed));
    }

    private static Domain.Invoices.Invoice CreateDraft() =>
        Domain.Invoices.Invoice.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Zar,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            Now);
}
