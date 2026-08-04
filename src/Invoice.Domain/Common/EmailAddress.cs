using System.Net.Mail;

namespace Invoice.Domain.Common;

public sealed record EmailAddress
{
    private EmailAddress(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static EmailAddress Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 254 ||
            !MailAddress.TryCreate(value, out var parsed) ||
            !string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainRuleViolationException(
                "email.invalid",
                "Email address is invalid.");
        }

        return new EmailAddress(value.Trim().ToLowerInvariant());
    }

    public override string ToString() => Value;
}
