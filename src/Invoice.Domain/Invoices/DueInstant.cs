namespace Invoice.Domain.Invoices;

public static class DueInstant
{
    public const string BusinessTimeZoneId = "Africa/Johannesburg";

    public static DateTimeOffset FromDueDate(DateOnly dueDate)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(BusinessTimeZoneId);
        var localBoundary = DateTime.SpecifyKind(
            dueDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        return new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(localBoundary, zone),
            TimeSpan.Zero);
    }
}
