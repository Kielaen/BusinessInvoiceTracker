using Invoice.Domain.Common;
using Invoice.Domain.Financial;

namespace Invoice.Domain.Invoices;

public sealed class Invoice
{
    private readonly List<InvoiceLine> _lines = [];
    private readonly List<Payment> _payments = [];

    private Invoice()
    {
    }

    private Invoice(
        Guid id,
        Guid businessId,
        Guid customerId,
        CurrencyCode currency,
        DateOnly issueDate,
        DateOnly dueDate,
        DateTimeOffset now)
    {
        Id = id;
        BusinessId = businessId;
        CustomerId = customerId;
        Currency = currency.Value;
        IssueDate = issueDate;
        SetDueDate(dueDate);
        Status = InvoiceStatus.Draft;
        Version = Guid.NewGuid();
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid BusinessId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string? Number { get; private set; }
    public long? NumberSequence { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public InvoiceStatus Status { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public DateTimeOffset? DueAtUtc { get; private set; }
    public decimal SubtotalAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal OutstandingAmount => TotalAmount - PaidAmount;
    public Guid Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? IssuedAtUtc { get; private set; }
    public DateTimeOffset? VoidedAtUtc { get; private set; }
    public IReadOnlyCollection<InvoiceLine> Lines => _lines;
    public IReadOnlyCollection<Payment> Payments => _payments;

    public bool HasSameDraft(Invoice candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var currentLines = _lines.OrderBy(line => line.Position).ToArray();
        var candidateLines = candidate._lines
            .OrderBy(line => line.Position)
            .ToArray();
        return Status == InvoiceStatus.Draft &&
            candidate.Status == InvoiceStatus.Draft &&
            BusinessId == candidate.BusinessId &&
            CustomerId == candidate.CustomerId &&
            Currency == candidate.Currency &&
            IssueDate == candidate.IssueDate &&
            DueDate == candidate.DueDate &&
            currentLines.Length == candidateLines.Length &&
            currentLines.Zip(candidateLines).All(pair =>
                pair.First.Position == pair.Second.Position &&
                pair.First.Description == pair.Second.Description &&
                pair.First.Quantity == pair.Second.Quantity &&
                pair.First.Currency == pair.Second.Currency &&
                pair.First.UnitPriceAmount == pair.Second.UnitPriceAmount &&
                pair.First.TaxRatePercentage == pair.Second.TaxRatePercentage);
    }

    public static Invoice CreateDraft(
        Guid id,
        Guid businessId,
        Guid customerId,
        CurrencyCode currency,
        DateOnly issueDate,
        DateOnly dueDate,
        DateTimeOffset now)
    {
        if (id == Guid.Empty ||
            businessId == Guid.Empty ||
            customerId == Guid.Empty)
        {
            throw new DomainRuleViolationException(
                "invoice.identity_required",
                "Invoice, business, and customer identifiers are required.");
        }

        ArgumentNullException.ThrowIfNull(currency);
        return new Invoice(
            id,
            businessId,
            customerId,
            currency,
            issueDate,
            dueDate,
            now);
    }

    public void ChangeCustomer(Guid customerId, DateTimeOffset now)
    {
        RequireDraft();
        if (customerId == Guid.Empty)
        {
            throw new DomainRuleViolationException(
                "invoice.customer_required",
                "A customer is required.");
        }

        CustomerId = customerId;
        Touch(now);
    }

    public void ChangeDates(
        DateOnly issueDate,
        DateOnly dueDate,
        DateTimeOffset now)
    {
        RequireDraft();
        IssueDate = issueDate;
        SetDueDate(dueDate);
        Touch(now);
    }

    public void ReplaceLines(
        IEnumerable<InvoiceLineDraft> drafts,
        DateTimeOffset now)
    {
        RequireDraft();
        ArgumentNullException.ThrowIfNull(drafts);
        var materialized = drafts.Take(201).ToList();
        if (materialized.Count > 200)
        {
            throw new DomainRuleViolationException(
                "invoice.lines_limit",
                "An invoice cannot contain more than 200 lines.");
        }

        var currency = CurrencyCode.Parse(Currency);
        var replacements = materialized
            .Select((draft, index) => InvoiceLine.Create(
                BusinessId,
                Id,
                draft,
                currency,
                index + 1))
            .ToList();

        var subtotal = SumBounded(
            replacements.Select(line => line.NetAmount),
            "invoice.subtotal_out_of_range");
        var tax = SumBounded(
            replacements.Select(line => line.TaxAmount),
            "invoice.tax_out_of_range");
        var total = checked(subtotal + tax);
        if (total > FinancialMath.MaximumMoney)
        {
            throw new DomainRuleViolationException(
                "invoice.total_out_of_range",
                "Invoice total exceeds the supported monetary range.");
        }

        _lines.Clear();
        _lines.AddRange(replacements);
        SubtotalAmount = subtotal;
        TaxAmount = tax;
        TotalAmount = total;
        Touch(now);
    }

    public void Issue(
        InvoiceNumber number,
        bool customerIsActive,
        DateTimeOffset now)
    {
        RequireDraft();
        ArgumentNullException.ThrowIfNull(number);
        if (!customerIsActive)
        {
            throw new DomainRuleViolationException(
                "invoice.customer_inactive",
                "An invoice requires an active customer.");
        }

        if (_lines.Count == 0)
        {
            throw new DomainRuleViolationException(
                "invoice.lines_required",
                "An invoice requires at least one line before issue.");
        }

        Number = number.Value;
        NumberSequence = number.Sequence;
        DueAtUtc = DueInstant.FromDueDate(DueDate);
        IssuedAtUtc = now;
        Status = InvoiceStatus.Issued;
        Touch(now);
    }

    public Payment RecordPayment(
        Guid paymentId,
        Money amount,
        DateTimeOffset receivedAtUtc,
        string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(amount);
        if (paymentId == Guid.Empty)
        {
            throw new DomainRuleViolationException(
                "invoice.payment_id_required",
                "Payment identifier is required.");
        }
        if (Status is InvoiceStatus.Draft or
            InvoiceStatus.Voided or
            InvoiceStatus.Paid)
        {
            throw new DomainRuleViolationException(
                "invoice.payment_not_allowed",
                $"Payments are not allowed while invoice status is {Status}.");
        }

        amount.RequireSameCurrency(Money.Zero(CurrencyCode.Parse(Currency)));
        if (amount.Amount <= 0m)
        {
            throw new DomainRuleViolationException(
                "invoice.payment_positive",
                "Payment amount must be positive.");
        }

        if (amount.Amount > OutstandingAmount)
        {
            throw new DomainRuleViolationException(
                "invoice.payment_exceeds_outstanding",
                "Payment cannot exceed the outstanding amount.");
        }

        var normalizedKey = idempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedKey) ||
            normalizedKey.Length > 100)
        {
            throw new DomainRuleViolationException(
                "invoice.payment_key_invalid",
                "Payment idempotency key is required and cannot exceed 100 characters.");
        }

        if (_payments.Any(payment =>
            string.Equals(
                payment.IdempotencyKey,
                normalizedKey,
                StringComparison.Ordinal)))
        {
            throw new DomainRuleViolationException(
                "invoice.payment_duplicate",
                "Payment idempotency key has already been used.");
        }

        var payment = new Payment(
            paymentId,
            BusinessId,
            Id,
            amount,
            receivedAtUtc,
            normalizedKey);
        _payments.Add(payment);
        PaidAmount = checked(PaidAmount + amount.Amount);
        Status = PaidAmount == TotalAmount
            ? InvoiceStatus.Paid
            : Status == InvoiceStatus.Overdue
                ? InvoiceStatus.Overdue
                : InvoiceStatus.PartiallyPaid;
        Touch(receivedAtUtc);
        return payment;
    }

    public void MarkOverdue(DateTimeOffset now)
    {
        if (Status is not (InvoiceStatus.Issued or
            InvoiceStatus.PartiallyPaid))
        {
            throw new DomainRuleViolationException(
                "invoice.overdue_not_allowed",
                $"Invoice status {Status} cannot become overdue.");
        }

        if (DueAtUtc is null || now < DueAtUtc.Value)
        {
            throw new DomainRuleViolationException(
                "invoice.not_due",
                "Invoice cannot become overdue before its due instant.");
        }

        if (OutstandingAmount <= 0m)
        {
            throw new DomainRuleViolationException(
                "invoice.no_outstanding",
                "An invoice without an outstanding balance cannot become overdue.");
        }

        Status = InvoiceStatus.Overdue;
        Touch(now);
    }

    public void RescheduleDueDate(DateOnly dueDate, DateTimeOffset now)
    {
        if (Status is not (InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid))
        {
            throw new DomainRuleViolationException(
                "invoice.due_date_change_not_allowed",
                $"The due date cannot be changed while invoice status is {Status}.");
        }

        if (dueDate < IssueDate)
        {
            throw new DomainRuleViolationException(
                "invoice.due_before_issue",
                "Due date cannot be before issue date.");
        }

        DueDate = dueDate;
        DueAtUtc = DueInstant.FromDueDate(dueDate);
        Touch(now);
    }

    public void Void(DateTimeOffset now)
    {
        if (Status is InvoiceStatus.Voided or InvoiceStatus.Paid)
        {
            throw new DomainRuleViolationException(
                "invoice.void_not_allowed",
                $"Invoice status {Status} cannot be voided.");
        }

        if (PaidAmount > 0m)
        {
            throw new DomainRuleViolationException(
                "invoice.void_with_payment",
                "An invoice with payments cannot be voided.");
        }

        Status = InvoiceStatus.Voided;
        VoidedAtUtc = now;
        Touch(now);
    }

    private void SetDueDate(DateOnly dueDate)
    {
        if (dueDate < IssueDate)
        {
            throw new DomainRuleViolationException(
                "invoice.due_before_issue",
                "Due date cannot be before issue date.");
        }

        DueDate = dueDate;
        DueAtUtc = null;
    }

    private void RequireDraft()
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new DomainRuleViolationException(
                "invoice.issued_immutable",
                "Only draft invoices may be edited.");
        }
    }

    private void Touch(DateTimeOffset now)
    {
        Version = Guid.NewGuid();
        UpdatedAtUtc = now;
    }

    private static decimal SumBounded(
        IEnumerable<decimal> values,
        string errorCode)
    {
        try
        {
            var total = values.Aggregate(0m, (current, value) =>
                checked(current + value));
            if (total > FinancialMath.MaximumMoney)
            {
                throw new OverflowException();
            }

            return total;
        }
        catch (OverflowException)
        {
            throw new DomainRuleViolationException(
                errorCode,
                "Calculated amount exceeds the supported monetary range.");
        }
    }
}
