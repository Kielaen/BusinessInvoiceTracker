using System.Globalization;
using Invoice.Domain.Common;

namespace Invoice.Domain.Invoices;

public sealed record InvoiceNumber
{
    private const string Prefix = "INV-";

    private InvoiceNumber(long sequence)
    {
        Sequence = sequence;
        Value = string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}{sequence:D8}");
    }

    public long Sequence { get; }
    public string Value { get; }

    public static InvoiceNumber FromSequence(long sequence)
    {
        if (sequence <= 0 || sequence > 99_999_999)
        {
            throw new DomainRuleViolationException(
                "invoice_number.out_of_range",
                "Invoice sequence must be between 1 and 99,999,999.");
        }

        return new InvoiceNumber(sequence);
    }

    public override string ToString() => Value;
}
