using Invoice.Domain.Common;
using Invoice.Domain.Financial;
using Invoice.Domain.Invoices;

namespace Invoice.Tests;

public sealed class InvoiceLifecycleTests
{
    private static readonly CurrencyCode Zar = CurrencyCode.Parse("ZAR");
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidLifecyclePreservesOverdueUntilFinalPayment()
    {
        var invoice = CreateIssuableDraft();
        invoice.Issue(InvoiceNumber.FromSequence(1), true, Now);
        invoice.RecordPayment(
            Guid.NewGuid(),
            Money.Create(10m, Zar),
            Now.AddHours(1),
            "partial-1");

        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        invoice.MarkOverdue(invoice.DueAtUtc!.Value);
        invoice.RecordPayment(
            Guid.NewGuid(),
            Money.Create(5m, Zar),
            Now.AddDays(2),
            "partial-2");

        Assert.Equal(InvoiceStatus.Overdue, invoice.Status);
        invoice.RecordPayment(
            Guid.NewGuid(),
            Money.Create(invoice.OutstandingAmount, Zar),
            Now.AddDays(3),
            "final");

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(0m, invoice.OutstandingAmount);
    }

    [Fact]
    public void IssuedInvoiceIsImmutableAndCannotBeIssuedTwice()
    {
        var invoice = CreateIssuableDraft();
        invoice.Issue(InvoiceNumber.FromSequence(1), true, Now);

        Assert.Throws<DomainRuleViolationException>(() =>
            invoice.ChangeDates(
                new DateOnly(2026, 7, 2),
                new DateOnly(2026, 8, 1),
                Now.AddMinutes(1)));
        Assert.Throws<DomainRuleViolationException>(() =>
            invoice.ReplaceLines([], Now.AddMinutes(1)));
        Assert.Throws<DomainRuleViolationException>(() =>
            invoice.Issue(InvoiceNumber.FromSequence(2), true, Now));
    }

    [Fact]
    public void IssueRejectsMissingLinesInactiveCustomerAndInvalidDates()
    {
        var empty = CreateDraft();
        Assert.Throws<DomainRuleViolationException>(() =>
            empty.Issue(InvoiceNumber.FromSequence(1), true, Now));

        var invoice = CreateIssuableDraft();
        Assert.Throws<DomainRuleViolationException>(() =>
            invoice.Issue(InvoiceNumber.FromSequence(1), false, Now));
        Assert.Throws<DomainRuleViolationException>(() =>
            Domain.Invoices.Invoice.CreateDraft(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Zar,
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 7, 31),
                Now));
    }

    [Fact]
    public void PaymentRulesRejectInvalidStateDuplicateAndOverpayment()
    {
        var draft = CreateIssuableDraft();
        Assert.Throws<DomainRuleViolationException>(() =>
            draft.RecordPayment(
                Guid.NewGuid(),
                Money.Create(1m, Zar),
                Now,
                "draft"));

        var invoice = CreateIssuableDraft();
        invoice.Issue(InvoiceNumber.FromSequence(1), true, Now);
        Assert.Throws<DomainRuleViolationException>(() =>
            invoice.RecordPayment(
                Guid.NewGuid(),
                Money.Zero(Zar),
                Now,
                "zero"));
        Assert.Throws<DomainRuleViolationException>(() =>
            invoice.RecordPayment(
                Guid.NewGuid(),
                Money.Create(invoice.TotalAmount + 0.01m, Zar),
                Now,
                "too-much"));

        invoice.RecordPayment(
            Guid.NewGuid(),
            Money.Create(1m, Zar),
            Now,
            "same-key");
        Assert.Throws<DomainRuleViolationException>(() =>
            invoice.RecordPayment(
                Guid.NewGuid(),
                Money.Create(1m, Zar),
                Now,
                "same-key"));
        Assert.Throws<DomainRuleViolationException>(() => invoice.Void(Now));
    }

    [Fact]
    public void OverdueAndTerminalTransitionsRejectInvalidCalls()
    {
        var invoice = CreateIssuableDraft();
        invoice.Issue(InvoiceNumber.FromSequence(1), true, Now);
        Assert.Throws<DomainRuleViolationException>(() =>
            invoice.MarkOverdue(invoice.DueAtUtc!.Value.AddTicks(-1)));

        invoice.Void(Now);
        Assert.Equal(InvoiceStatus.Voided, invoice.Status);
        Assert.Throws<DomainRuleViolationException>(() => invoice.Void(Now));
        Assert.Throws<DomainRuleViolationException>(() =>
            invoice.MarkOverdue(Now.AddDays(100)));
    }

    [Fact]
    public void IssuedInvoiceDueDateCanBeRescheduledWhileWorkflowWaits()
    {
        var invoice = CreateIssuableDraft();
        invoice.Issue(InvoiceNumber.FromSequence(1), true, Now);
        var previousVersion = invoice.Version;
        var newDueDate = new DateOnly(2026, 8, 15);

        invoice.RescheduleDueDate(newDueDate, Now.AddMinutes(1));

        Assert.Equal(newDueDate, invoice.DueDate);
        Assert.Equal(DueInstant.FromDueDate(newDueDate), invoice.DueAtUtc);
        Assert.NotEqual(previousVersion, invoice.Version);
        Assert.Throws<DomainRuleViolationException>(() =>
            invoice.RescheduleDueDate(
                new DateOnly(2026, 6, 30),
                Now.AddMinutes(2)));

        invoice.Void(Now.AddMinutes(3));
        Assert.Throws<DomainRuleViolationException>(() =>
            invoice.RescheduleDueDate(new DateOnly(2026, 8, 20), Now.AddMinutes(4)));
    }

    [Fact]
    public void PaidAndVoidedStatesAreTerminal()
    {
        var paid = CreateIssuableDraft();
        paid.Issue(InvoiceNumber.FromSequence(1), true, Now);
        paid.RecordPayment(
            Guid.NewGuid(),
            Money.Create(paid.TotalAmount, Zar),
            Now,
            "paid");
        Assert.Throws<DomainRuleViolationException>(() =>
            paid.RecordPayment(Guid.NewGuid(), Money.Create(1m, Zar), Now, "again"));
        Assert.Throws<DomainRuleViolationException>(() => paid.Void(Now));
        Assert.Throws<DomainRuleViolationException>(() =>
            paid.MarkOverdue(Now.AddDays(100)));

        var voided = CreateIssuableDraft();
        voided.Void(Now);
        Assert.Throws<DomainRuleViolationException>(() =>
            voided.RecordPayment(
                Guid.NewGuid(),
                Money.Create(1m, Zar),
                Now,
                "voided"));
        Assert.Throws<DomainRuleViolationException>(() =>
            voided.Issue(InvoiceNumber.FromSequence(2), true, Now));
        Assert.Throws<DomainRuleViolationException>(() =>
            voided.ChangeCustomer(Guid.NewGuid(), Now));
    }

    private static Domain.Invoices.Invoice CreateIssuableDraft()
    {
        var invoice = CreateDraft();
        invoice.ReplaceLines(
        [
            new("Service", 1m, Money.Create(100m, Zar), TaxRate.Create(15m))
        ],
        Now);
        return invoice;
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
