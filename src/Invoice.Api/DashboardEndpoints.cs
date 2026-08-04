using System.Diagnostics;
using System.Security.Claims;
using Invoice.Api.Authorization;
using Invoice.Domain.Businesses;
using Invoice.Domain.Invoices;
using Invoice.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Invoice.Api;

public static class DashboardEndpoints
{
    private const int OverdueLimit = 5;
    private const int ActivityLimit = 10;
    private static readonly ActivitySource Activity = new("Invoice.Api.Dashboard");

    public static void MapDashboardEndpoints(this WebApplication app)
    {
        app.MapGet(
                "/api/v1/businesses/{businessId:guid}/dashboard",
                GetAsync)
            .RequireAuthorization()
            .WithTags("Dashboard")
            .WithName("GetDashboard")
            .Produces<DashboardResponse>()
            .Produces<ProblemDetails>(401, "application/problem+json")
            .Produces<ProblemDetails>(403, "application/problem+json")
            .Produces<ProblemDetails>(404, "application/problem+json");
    }

    private static async Task<IResult> GetAsync(
        Guid businessId,
        ClaimsPrincipal principal,
        InvoiceDbContext database,
        IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(
            principal,
            businessId,
            database,
            authorization,
            cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        using var activity = Activity.StartActivity("dashboard.get");
        activity?.SetTag("business.id", businessId);
        var invoices = database.Invoices.AsNoTracking()
            .Where(invoice => invoice.BusinessId == businessId);

        var counts = await invoices
            .GroupBy(_ => 1)
            .Select(group => new DashboardCountsResponse(
                group.Count(),
                group.Count(invoice => invoice.Status == InvoiceStatus.Draft),
                group.Count(invoice =>
                    invoice.Status == InvoiceStatus.Issued ||
                    invoice.Status == InvoiceStatus.PartiallyPaid ||
                    invoice.Status == InvoiceStatus.Overdue),
                group.Count(invoice => invoice.Status == InvoiceStatus.Overdue),
                group.Count(invoice => invoice.Status == InvoiceStatus.Paid),
                group.Count(invoice => invoice.Status == InvoiceStatus.Voided)))
            .SingleOrDefaultAsync(cancellationToken) ??
            new DashboardCountsResponse(0, 0, 0, 0, 0, 0);

        var currencyTotals = await invoices
            .Where(invoice =>
                invoice.Status == InvoiceStatus.Issued ||
                invoice.Status == InvoiceStatus.PartiallyPaid ||
                invoice.Status == InvoiceStatus.Overdue)
            .GroupBy(invoice => invoice.Currency)
            .OrderBy(group => group.Key)
            .Select(group => new DashboardCurrencyResponse(
                group.Key,
                group.Sum(invoice => invoice.TotalAmount - invoice.PaidAmount),
                group.Sum(invoice => invoice.Status == InvoiceStatus.Overdue
                    ? invoice.TotalAmount - invoice.PaidAmount
                    : 0m)))
            .ToListAsync(cancellationToken);

        var overdueRows = await (
                from invoice in invoices
                join customer in database.Customers.AsNoTracking()
                    on new { invoice.BusinessId, Id = invoice.CustomerId }
                    equals new { customer.BusinessId, customer.Id }
                where invoice.Status == InvoiceStatus.Overdue
                orderby invoice.DueAtUtc, invoice.Id
                select new DashboardInvoiceDatabaseRow(
                    invoice.Id,
                    customer.Name,
                    invoice.Number,
                    invoice.Status,
                    invoice.Currency,
                    invoice.IssueDate,
                    invoice.DueDate,
                    invoice.TotalAmount,
                    invoice.PaidAmount,
                    invoice.Version,
                    invoice.UpdatedAtUtc))
            .Take(OverdueLimit)
            .ToListAsync(cancellationToken);
        var overdue = overdueRows.Select(row => new InvoiceSummaryResponse(
            row.Id,
            row.CustomerName,
            row.Number,
            row.Status.ToString(),
            row.Currency,
            row.IssueDate,
            row.DueDate,
            row.TotalAmount,
            row.TotalAmount - row.PaidAmount,
            row.Version,
            row.UpdatedAtUtc.UtcDateTime)).ToArray();

        var activityRows = await (
                from audit in database.AuditEntries.AsNoTracking()
                join invoice in database.Invoices.AsNoTracking()
                    on new { audit.BusinessId, Id = audit.AggregateId }
                    equals new { invoice.BusinessId, invoice.Id }
                join customer in database.Customers.AsNoTracking()
                    on new { invoice.BusinessId, Id = invoice.CustomerId }
                    equals new { customer.BusinessId, customer.Id }
                where audit.BusinessId == businessId &&
                    audit.AggregateType == "Invoice"
                orderby audit.OccurredAtUtc descending, audit.Id descending
                select new DashboardActivityDatabaseRow(
                    audit.Id,
                    invoice.Id,
                    invoice.Number,
                    customer.Name,
                    audit.Action,
                    audit.ActorUserId,
                    audit.OccurredAtUtc))
            .Take(ActivityLimit)
            .ToListAsync(cancellationToken);
        var recentActivity = activityRows.Select(row => new DashboardActivityResponse(
            row.Id.ToString(),
            row.InvoiceId,
            row.InvoiceNumber,
            row.CustomerName,
            row.Action,
            row.ActorUserId,
            row.OccurredAtUtc.UtcDateTime)).ToArray();

        return TypedResults.Ok(new DashboardResponse(
            counts,
            currencyTotals,
            overdue,
            recentActivity,
            OverdueLimit,
            ActivityLimit));
    }

    private static async Task<IResult?> AuthorizeAsync(
        ClaimsPrincipal principal,
        Guid businessId,
        InvoiceDbContext database,
        IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(subject, out var userId))
        {
            return ApiProblems.NotFound();
        }

        var membership = await database.BusinessMemberships.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.BusinessId == businessId &&
                item.UserId == userId &&
                item.IsActive,
                cancellationToken);
        if (membership is null)
        {
            return ApiProblems.NotFound();
        }

        return (await authorization.AuthorizeAsync(
            principal,
            membership,
            new BusinessRoleRequirement(BusinessRole.Viewer))).Succeeded
                ? null
                : ApiProblems.Forbidden();
    }

    private sealed record DashboardInvoiceDatabaseRow(
        Guid Id,
        string CustomerName,
        string? Number,
        InvoiceStatus Status,
        string Currency,
        DateOnly IssueDate,
        DateOnly DueDate,
        decimal TotalAmount,
        decimal PaidAmount,
        Guid Version,
        DateTimeOffset UpdatedAtUtc);

    private sealed record DashboardActivityDatabaseRow(
        Guid Id,
        Guid InvoiceId,
        string? InvoiceNumber,
        string CustomerName,
        string Action,
        Guid? ActorUserId,
        DateTimeOffset OccurredAtUtc);
}

public sealed record DashboardResponse(
    DashboardCountsResponse Counts,
    IReadOnlyList<DashboardCurrencyResponse> CurrencyTotals,
    IReadOnlyList<InvoiceSummaryResponse> OverdueInvoices,
    IReadOnlyList<DashboardActivityResponse> RecentActivity,
    int OverdueLimit,
    int ActivityLimit);

public sealed record DashboardCountsResponse(
    int Total,
    int Draft,
    int Open,
    int Overdue,
    int Paid,
    int Voided);

public sealed record DashboardCurrencyResponse(
    string Currency,
    decimal OutstandingAmount,
    decimal OverdueAmount);

public sealed record DashboardActivityResponse(
    string Id,
    Guid InvoiceId,
    string? InvoiceNumber,
    string CustomerName,
    string Action,
    Guid? ActorUserId,
    DateTime OccurredAtUtc);
