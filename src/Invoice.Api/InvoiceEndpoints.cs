using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Text.Json;
using Invoice.Api.Authorization;
using Invoice.Application;
using Invoice.Domain.Auditing;
using Invoice.Domain.Businesses;
using Invoice.Domain.Common;
using Invoice.Domain.Financial;
using Invoice.Domain.Invoices;
using Invoice.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Invoice.Api;

public static class InvoiceEndpoints
{
    private static readonly ActivitySource Activity = new("Invoice.Api.Invoices");
    private static readonly Meter Meter = new("Invoice.Api.Invoices");
    private static readonly Counter<long> Mutations =
        Meter.CreateCounter<long>("invoice.drafts.mutations");
    private static readonly Action<ILogger, Guid, Guid, Exception?> DraftCreated =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Information, new EventId(6001, nameof(DraftCreated)),
            "Draft invoice created for BusinessId {BusinessId} InvoiceId {InvoiceId}");
    private static readonly Action<ILogger, Guid, Guid, Exception?> DraftUpdated =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Information, new EventId(6002, nameof(DraftUpdated)),
            "Draft invoice updated for BusinessId {BusinessId} InvoiceId {InvoiceId}");
    private static readonly Action<ILogger, Guid, Guid, Exception?> InvoiceIssued =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Information, new EventId(7001, nameof(InvoiceIssued)),
            "Invoice issued for BusinessId {BusinessId} InvoiceId {InvoiceId}");
    private static readonly Action<ILogger, Guid, Guid, Exception?> PaymentRecorded =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Information, new EventId(7002, nameof(PaymentRecorded)),
            "Payment recorded for BusinessId {BusinessId} InvoiceId {InvoiceId}");
    private static readonly Action<ILogger, Guid, Guid, Exception?> InvoiceVoided =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Information, new EventId(7003, nameof(InvoiceVoided)),
            "Invoice voided for BusinessId {BusinessId} InvoiceId {InvoiceId}");
    private static readonly Action<ILogger, Guid, Guid, Exception?> DueDateChanged =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Information, new EventId(7004, nameof(DueDateChanged)),
            "Invoice due date changed for BusinessId {BusinessId} InvoiceId {InvoiceId}");

    public static void MapInvoiceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/businesses/{businessId:guid}/invoices")
            .RequireAuthorization()
            .WithTags("Invoices");

        group.MapGet("", ListAsync).WithName("ListInvoices")
            .Produces<InvoicePageResponse>().ProducesValidationProblem()
            .Produces<ProblemDetails>(401, "application/problem+json")
            .Produces<ProblemDetails>(403, "application/problem+json")
            .Produces<ProblemDetails>(404, "application/problem+json");
        group.MapPost("", CreateAsync).AddEndpointFilter<ValidateAntiforgeryFilter>()
            .WithName("CreateDraftInvoice")
            .Produces<InvoiceResponse>(201).Produces<InvoiceResponse>(200)
            .ProducesValidationProblem()
            .Produces<ProblemDetails>(401, "application/problem+json")
            .Produces<ProblemDetails>(403, "application/problem+json")
            .Produces<ProblemDetails>(404, "application/problem+json")
            .Produces<ProblemDetails>(409, "application/problem+json");
        group.MapGet("/{invoiceId:guid}", GetAsync).WithName("GetInvoice")
            .Produces<InvoiceResponse>()
            .Produces<ProblemDetails>(401, "application/problem+json")
            .Produces<ProblemDetails>(403, "application/problem+json")
            .Produces<ProblemDetails>(404, "application/problem+json");
        group.MapPut("/{invoiceId:guid}", UpdateAsync).AddEndpointFilter<ValidateAntiforgeryFilter>()
            .WithName("UpdateDraftInvoice")
            .Produces<InvoiceResponse>().ProducesValidationProblem()
            .Produces<ProblemDetails>(401, "application/problem+json")
            .Produces<ProblemDetails>(403, "application/problem+json")
            .Produces<ProblemDetails>(404, "application/problem+json")
            .Produces<ProblemDetails>(409, "application/problem+json");
        group.MapPost("/{invoiceId:guid}/issue", IssueAsync)
            .AddEndpointFilter<ValidateAntiforgeryFilter>()
            .WithName("IssueInvoice").Produces<InvoiceResponse>()
            .ProducesValidationProblem()
            .Produces<ProblemDetails>(401, "application/problem+json")
            .Produces<ProblemDetails>(403, "application/problem+json")
            .Produces<ProblemDetails>(404, "application/problem+json")
            .Produces<ProblemDetails>(409, "application/problem+json");
        group.MapPost("/{invoiceId:guid}/payments", RecordPaymentAsync)
            .AddEndpointFilter<ValidateAntiforgeryFilter>()
            .WithName("RecordInvoicePayment").Produces<InvoiceResponse>()
            .ProducesValidationProblem()
            .Produces<ProblemDetails>(401, "application/problem+json")
            .Produces<ProblemDetails>(403, "application/problem+json")
            .Produces<ProblemDetails>(404, "application/problem+json")
            .Produces<ProblemDetails>(409, "application/problem+json");
        group.MapPost("/{invoiceId:guid}/void", VoidAsync)
            .AddEndpointFilter<ValidateAntiforgeryFilter>()
            .WithName("VoidInvoice").Produces<InvoiceResponse>()
            .ProducesValidationProblem()
            .Produces<ProblemDetails>(401, "application/problem+json")
            .Produces<ProblemDetails>(403, "application/problem+json")
            .Produces<ProblemDetails>(404, "application/problem+json")
            .Produces<ProblemDetails>(409, "application/problem+json");
        group.MapPost("/{invoiceId:guid}/due-date", ChangeDueDateAsync)
            .AddEndpointFilter<ValidateAntiforgeryFilter>()
            .WithName("ChangeInvoiceDueDate").Produces<InvoiceResponse>()
            .ProducesValidationProblem()
            .Produces<ProblemDetails>(401, "application/problem+json")
            .Produces<ProblemDetails>(403, "application/problem+json")
            .Produces<ProblemDetails>(404, "application/problem+json")
            .Produces<ProblemDetails>(409, "application/problem+json");
        group.MapGet("/{invoiceId:guid}/audit", AuditAsync).WithName("GetInvoiceAudit")
            .Produces<IReadOnlyList<InvoiceAuditResponse>>()
            .Produces<ProblemDetails>(401, "application/problem+json")
            .Produces<ProblemDetails>(403, "application/problem+json")
            .Produces<ProblemDetails>(404, "application/problem+json");
    }

    private static async Task<IResult> ListAsync(
        Guid businessId, string? search, string? status, string? sort,
        string? direction, int? page, int? pageSize, ClaimsPrincipal principal,
        InvoiceDbContext database, IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var currentSearch = search?.Trim() ?? string.Empty;
        var currentSort = string.IsNullOrWhiteSpace(sort)
            ? "updated"
            : sort.Trim().ToLowerInvariant();
        var currentDirection = string.IsNullOrWhiteSpace(direction)
            ? "desc"
            : direction.Trim().ToLowerInvariant();
        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? 20;
        var errors = new Dictionary<string, string[]>();
        if (currentSearch.Length > 100)
            errors["search"] = ["Search must be 100 characters or fewer."];
        if (!string.IsNullOrWhiteSpace(status) &&
            !Enum.TryParse<InvoiceStatus>(status, true, out _))
            errors["status"] = ["Status is not recognized."];
        if (currentSort is not ("updated" or "due" or "amount" or
            "customer" or "number" or "status"))
            errors["sort"] = ["Sort must be updated, due, amount, customer, number, or status."];
        if (currentDirection is not ("asc" or "desc"))
            errors["direction"] = ["Direction must be asc or desc."];
        if (currentPage is < 1 or > 10000)
            errors["page"] = ["Page must be between 1 and 10000."];
        if (currentPageSize is < 1 or > 100)
            errors["pageSize"] = ["Page size must be between 1 and 100."];
        if (errors.Count > 0) return ApiProblems.Validation(errors);
        var denied = await AuthorizeAsync(
            principal, businessId, BusinessRole.Viewer, database, authorization, cancellationToken);
        if (denied is not null) return denied;

        using var activity = Activity.StartActivity("invoices.list");
        var query =
            from invoice in database.Invoices.AsNoTracking()
            join customer in database.Customers.AsNoTracking()
                on new { invoice.BusinessId, Id = invoice.CustomerId }
                equals new { customer.BusinessId, customer.Id }
            where invoice.BusinessId == businessId
            select new { Invoice = invoice, CustomerName = customer.Name };
        if (currentSearch.Length > 0)
        {
            var pattern = $"%{EscapeLikePattern(currentSearch)}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.CustomerName, pattern, "\\") ||
                EF.Functions.ILike(item.Invoice.Number ?? string.Empty, pattern, "\\"));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsedStatus = Enum.Parse<InvoiceStatus>(status, true);
            query = query.Where(item => item.Invoice.Status == parsedStatus);
        }
        var totalCount = await query.CountAsync(cancellationToken);
        var descending = currentDirection == "desc";
        var ordered = (currentSort, descending) switch
        {
            ("due", false) => query.OrderBy(item => item.Invoice.DueDate)
                .ThenBy(item => item.Invoice.Id),
            ("due", true) => query.OrderByDescending(item => item.Invoice.DueDate)
                .ThenBy(item => item.Invoice.Id),
            ("amount", false) => query.OrderBy(item => item.Invoice.TotalAmount)
                .ThenBy(item => item.Invoice.Id),
            ("amount", true) => query.OrderByDescending(item => item.Invoice.TotalAmount)
                .ThenBy(item => item.Invoice.Id),
            ("customer", false) => query.OrderBy(item => item.CustomerName)
                .ThenBy(item => item.Invoice.Id),
            ("customer", true) => query.OrderByDescending(item => item.CustomerName)
                .ThenBy(item => item.Invoice.Id),
            ("number", false) => query.OrderBy(item => item.Invoice.Number)
                .ThenBy(item => item.Invoice.Id),
            ("number", true) => query.OrderByDescending(item => item.Invoice.Number)
                .ThenBy(item => item.Invoice.Id),
            ("status", false) => query.OrderBy(item => item.Invoice.Status)
                .ThenBy(item => item.Invoice.Id),
            ("status", true) => query.OrderByDescending(item => item.Invoice.Status)
                .ThenBy(item => item.Invoice.Id),
            (_, false) => query.OrderBy(item => item.Invoice.UpdatedAtUtc)
                .ThenBy(item => item.Invoice.Id),
            _ => query.OrderByDescending(item => item.Invoice.UpdatedAtUtc)
                .ThenBy(item => item.Invoice.Id)
        };
        var pageRows = await ordered
            .Skip((currentPage - 1) * currentPageSize)
            .Take(currentPageSize)
            .Select(item => new InvoiceListDatabaseRow(
                item.Invoice.Id,
                item.CustomerName,
                item.Invoice.Number,
                item.Invoice.Status,
                item.Invoice.Currency,
                item.Invoice.IssueDate,
                item.Invoice.DueDate,
                item.Invoice.TotalAmount,
                item.Invoice.PaidAmount,
                item.Invoice.Version,
                item.Invoice.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        var items = pageRows.Select(item => new InvoiceSummaryResponse(
            item.Id, item.CustomerName, item.Number, item.Status.ToString(), item.Currency,
            item.IssueDate, item.DueDate, item.TotalAmount,
            item.TotalAmount - item.PaidAmount,
            item.Version, item.UpdatedAtUtc.UtcDateTime)).ToArray();
        return TypedResults.Ok(new InvoicePageResponse(
            items, currentPage, currentPageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)currentPageSize)));
    }

    private static async Task<IResult> GetAsync(
        Guid businessId, Guid invoiceId, ClaimsPrincipal principal,
        InvoiceDbContext database, IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(
            principal, businessId, BusinessRole.Viewer, database, authorization, cancellationToken);
        if (denied is not null) return denied;
        var invoice = await database.Invoices.AsNoTracking().Include(item => item.Lines)
            .SingleOrDefaultAsync(
                item => item.BusinessId == businessId && item.Id == invoiceId,
                cancellationToken);
        if (invoice is null) return ApiProblems.NotFound();
        var customerName = await database.Customers.AsNoTracking()
            .Where(customer => customer.BusinessId == businessId && customer.Id == invoice.CustomerId)
            .Select(customer => customer.Name).SingleAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(invoice, customerName));
    }

    private static async Task<IResult> CreateAsync(
        Guid businessId, InvoiceCreateRequest request, ClaimsPrincipal principal,
        InvoiceDbContext database, IAuthorizationService authorization,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(
            principal, businessId, BusinessRole.Accountant, database, authorization, cancellationToken);
        if (denied is not null) return denied;
        var built = await BuildCandidateAsync(
            request.RequestId, businessId, request, database, cancellationToken);
        if (built.Error is not null) return built.Error;
        var candidate = built.Invoice!;
        var existing = await database.Invoices.AsNoTracking().Include(invoice => invoice.Lines)
            .SingleOrDefaultAsync(invoice =>
                invoice.BusinessId == businessId && invoice.Id == request.RequestId,
                cancellationToken);
        if (existing is not null)
        {
            if (!existing.HasSameDraft(candidate))
                return ApiProblems.Conflict("The create request ID was already used for different invoice data.");
            return TypedResults.Ok(ToResponse(existing, built.CustomerName!));
        }

        using var activity = Activity.StartActivity("invoice.draft.create");
        database.Invoices.Add(candidate);
        database.AuditEntries.Add(NewAudit(candidate, principal, "DraftCreated"));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            var raced = await database.Invoices.AsNoTracking().Include(invoice => invoice.Lines)
                .SingleOrDefaultAsync(invoice =>
                    invoice.BusinessId == businessId && invoice.Id == request.RequestId,
                    cancellationToken);
            if (raced is not null && raced.HasSameDraft(candidate))
                return TypedResults.Ok(ToResponse(raced, built.CustomerName!));
            return ApiProblems.Conflict("The draft conflicts with current database state.");
        }
        Mutations.Add(1, new KeyValuePair<string, object?>("operation", "create"));
        DraftCreated(loggerFactory.CreateLogger("Invoice.Api.Invoices"), businessId, candidate.Id, null);
        return TypedResults.Created(
            $"/api/v1/businesses/{businessId}/invoices/{candidate.Id}",
            ToResponse(candidate, built.CustomerName!));
    }

    private static async Task<IResult> UpdateAsync(
        Guid businessId, Guid invoiceId, InvoiceUpdateRequest request,
        ClaimsPrincipal principal, InvoiceDbContext database,
        IAuthorizationService authorization, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(
            principal, businessId, BusinessRole.Accountant, database, authorization, cancellationToken);
        if (denied is not null) return denied;
        var current = await database.Invoices.Include(invoice => invoice.Lines)
            .SingleOrDefaultAsync(invoice =>
                invoice.BusinessId == businessId && invoice.Id == invoiceId,
                cancellationToken);
        if (current is null) return ApiProblems.NotFound();
        if (current.Status != InvoiceStatus.Draft)
            return ApiProblems.Conflict("Only a draft invoice may be edited.");
        if (current.Version != request.Version)
            return ApiProblems.Conflict("The draft changed after it was loaded. Reload before saving.");
        var built = await BuildCandidateAsync(
            invoiceId, businessId, request, database, cancellationToken);
        if (built.Error is not null) return built.Error;
        var candidate = built.Invoice!;
        if (current.HasSameDraft(candidate))
            return TypedResults.Ok(ToResponse(current, built.CustomerName!));
        var now = DateTimeOffset.UtcNow;
        current.ChangeCustomer(candidate.CustomerId, now);
        current.ChangeDates(candidate.IssueDate, candidate.DueDate, now);
        current.ReplaceLines(
            candidate.Lines.OrderBy(line => line.Position).Select(line =>
                new InvoiceLineDraft(
                    line.Description, line.Quantity,
                    Money.Create(line.UnitPriceAmount, CurrencyCode.Parse(line.Currency)),
                    TaxRate.Create(line.TaxRatePercentage))),
            now);
        // Replacement lines have application-generated IDs. Because they are
        // attached through an already-tracked aggregate, mark them explicitly
        // as inserts while EF's orphan handling deletes the prior collection.
        foreach (var line in current.Lines)
            database.Entry(line).State = EntityState.Added;
        using var activity = Activity.StartActivity("invoice.draft.update");
        database.AuditEntries.Add(NewAudit(current, principal, "DraftUpdated"));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiProblems.Conflict("The draft changed while this update was being saved.");
        }
        Mutations.Add(1, new KeyValuePair<string, object?>("operation", "update"));
        DraftUpdated(loggerFactory.CreateLogger("Invoice.Api.Invoices"), businessId, current.Id, null);
        return TypedResults.Ok(ToResponse(current, built.CustomerName!));
    }

    private static async Task<IResult> IssueAsync(
        Guid businessId, Guid invoiceId, InvoiceCommandRequest request,
        ClaimsPrincipal principal, InvoiceDbContext database,
        PostgreSqlInvoiceNumberAllocator allocator,
        IAuthorizationService authorization, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(
            principal, businessId, BusinessRole.Accountant,
            database, authorization, cancellationToken);
        if (denied is not null) return denied;
        var invalid = ValidateCommand(request.RequestId, request.Version);
        if (invalid is not null) return invalid;

        try
        {
            var strategy = database.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IResult>(async () =>
            {
                await using var transaction = await database.Database
                    .BeginTransactionAsync(cancellationToken);
                var invoice = await database.Invoices
                    .Include(item => item.Lines).Include(item => item.Payments)
                    .SingleOrDefaultAsync(item => item.BusinessId == businessId &&
                        item.Id == invoiceId, cancellationToken);
                if (invoice is null) return ApiProblems.NotFound();
                var customer = await database.Customers.SingleAsync(item =>
                    item.BusinessId == businessId && item.Id == invoice.CustomerId,
                    cancellationToken);
                var replay = await FindCommandAsync(
                    request.RequestId, database, cancellationToken);
                if (replay is not null)
                {
                    return MatchesCommand(replay, "InvoiceIssued", businessId, invoiceId)
                        ? TypedResults.Ok(ToResponse(invoice, customer.Name))
                        : ApiProblems.Conflict("The command request ID was already used for a different operation.");
                }
                if (invoice.Status != InvoiceStatus.Draft)
                    return ApiProblems.Conflict("Only a draft invoice may be issued.");
                if (invoice.Version != request.Version)
                    return ApiProblems.Conflict("The invoice changed after it was loaded. Reload before issuing.");

                var now = DateTimeOffset.UtcNow;
                var number = await allocator.AllocateAsync(businessId, cancellationToken);
                invoice.Issue(number, customer.IsActive, now);
                AddLifecycleEvidence(
                    database, invoice, principal, request.RequestId,
                    "InvoiceIssued", now, null, null);
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                Mutations.Add(1, new KeyValuePair<string, object?>("operation", "issue"));
                InvoiceIssued(
                    loggerFactory.CreateLogger("Invoice.Api.Invoices"),
                    businessId, invoice.Id, null);
                return TypedResults.Ok(ToResponse(invoice, customer.Name));
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiProblems.Conflict("The invoice changed while it was being issued.");
        }
        catch (DbUpdateException)
        {
            return ApiProblems.Conflict("The issue command conflicts with current database state.");
        }
        catch (DomainRuleViolationException error)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>
            {
                ["invoice"] = [error.Message]
            });
        }
    }

    private static async Task<IResult> RecordPaymentAsync(
        Guid businessId, Guid invoiceId, InvoicePaymentRequest request,
        ClaimsPrincipal principal, InvoiceDbContext database,
        IAuthorizationService authorization, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(
            principal, businessId, BusinessRole.Accountant,
            database, authorization, cancellationToken);
        if (denied is not null) return denied;
        var errors = new Dictionary<string, string[]>();
        if (request.RequestId == Guid.Empty) errors["requestId"] = ["A request ID is required."];
        if (request.Version == Guid.Empty) errors["version"] = ["An invoice version is required."];
        var key = request.IdempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
            errors["idempotencyKey"] = ["An idempotency key of at most 100 characters is required."];
        Money? amount = null;
        try { amount = Money.Create(request.Amount, CurrencyCode.Parse("ZAR")); }
        catch (DomainRuleViolationException error) { errors["amount"] = [error.Message]; }
        if (errors.Count > 0) return ApiProblems.Validation(errors);
        var paymentAmount = amount!;

        try
        {
            var strategy = database.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IResult>(async () =>
            {
                await using var transaction = await database.Database
                    .BeginTransactionAsync(cancellationToken);
                var invoice = await database.Invoices
                    .Include(item => item.Lines).Include(item => item.Payments)
                    .SingleOrDefaultAsync(item => item.BusinessId == businessId &&
                        item.Id == invoiceId, cancellationToken);
                if (invoice is null) return ApiProblems.NotFound();
                var customerName = await database.Customers
                    .Where(item => item.BusinessId == businessId &&
                        item.Id == invoice.CustomerId)
                    .Select(item => item.Name).SingleAsync(cancellationToken);
                var existing = invoice.Payments.SingleOrDefault(payment =>
                    payment.Id == request.RequestId || payment.IdempotencyKey == key);
                var receivedAt = NormalizeDatabaseTimestamp(request.ReceivedAtUtc);
                if (existing is not null)
                {
                    var identical = existing.Id == request.RequestId &&
                        existing.IdempotencyKey == key &&
                        existing.Amount == paymentAmount.Amount &&
                        existing.ReceivedAtUtc == receivedAt;
                    return identical
                        ? TypedResults.Ok(ToResponse(invoice, customerName))
                        : ApiProblems.Conflict("The payment identity was already used for different data.");
                }
                var replay = await FindCommandAsync(
                    request.RequestId, database, cancellationToken);
                if (replay is not null)
                    return ApiProblems.Conflict("The command request ID was already used for a different operation.");
                if (invoice.Version != request.Version)
                    return ApiProblems.Conflict("The invoice changed after it was loaded. Reload before recording payment.");

                var payment = invoice.RecordPayment(
                    request.RequestId, paymentAmount, receivedAt, key!);
                database.Entry(payment).State = EntityState.Added;
                AddLifecycleEvidence(
                    database, invoice, principal, request.RequestId,
                    "InvoicePaymentRecorded", receivedAt, paymentAmount.Amount, key);
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                Mutations.Add(1, new KeyValuePair<string, object?>("operation", "payment"));
                PaymentRecorded(
                    loggerFactory.CreateLogger("Invoice.Api.Invoices"),
                    businessId, invoice.Id, null);
                return TypedResults.Ok(ToResponse(invoice, customerName));
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiProblems.Conflict("The invoice balance changed while payment was being recorded.");
        }
        catch (DbUpdateException)
        {
            return ApiProblems.Conflict("The payment command conflicts with current database state.");
        }
        catch (DomainRuleViolationException error)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>
            {
                ["payment"] = [error.Message]
            });
        }
    }

    private static async Task<IResult> VoidAsync(
        Guid businessId, Guid invoiceId, InvoiceCommandRequest request,
        ClaimsPrincipal principal, InvoiceDbContext database,
        IAuthorizationService authorization, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(
            principal, businessId, BusinessRole.Accountant,
            database, authorization, cancellationToken);
        if (denied is not null) return denied;
        var invalid = ValidateCommand(request.RequestId, request.Version);
        if (invalid is not null) return invalid;

        try
        {
            var strategy = database.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IResult>(async () =>
            {
                await using var transaction = await database.Database
                    .BeginTransactionAsync(cancellationToken);
                var invoice = await database.Invoices
                    .Include(item => item.Lines).Include(item => item.Payments)
                    .SingleOrDefaultAsync(item => item.BusinessId == businessId &&
                        item.Id == invoiceId, cancellationToken);
                if (invoice is null) return ApiProblems.NotFound();
                var customerName = await database.Customers
                    .Where(item => item.BusinessId == businessId &&
                        item.Id == invoice.CustomerId)
                    .Select(item => item.Name).SingleAsync(cancellationToken);
                var replay = await FindCommandAsync(
                    request.RequestId, database, cancellationToken);
                if (replay is not null)
                {
                    return MatchesCommand(replay, "InvoiceVoided", businessId, invoiceId)
                        ? TypedResults.Ok(ToResponse(invoice, customerName))
                        : ApiProblems.Conflict("The command request ID was already used for a different operation.");
                }
                if (invoice.Version != request.Version)
                    return ApiProblems.Conflict("The invoice changed after it was loaded. Reload before voiding.");

                var now = DateTimeOffset.UtcNow;
                invoice.Void(now);
                AddLifecycleEvidence(
                    database, invoice, principal, request.RequestId,
                    "InvoiceVoided", now, null, null);
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                Mutations.Add(1, new KeyValuePair<string, object?>("operation", "void"));
                InvoiceVoided(
                    loggerFactory.CreateLogger("Invoice.Api.Invoices"),
                    businessId, invoice.Id, null);
                return TypedResults.Ok(ToResponse(invoice, customerName));
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiProblems.Conflict("The invoice changed while it was being voided.");
        }
        catch (DbUpdateException)
        {
            return ApiProblems.Conflict("The void command conflicts with current database state.");
        }
        catch (DomainRuleViolationException error)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>
            {
                ["invoice"] = [error.Message]
            });
        }
    }

    private static async Task<IResult> ChangeDueDateAsync(
        Guid businessId, Guid invoiceId, InvoiceDueDateRequest request,
        ClaimsPrincipal principal, InvoiceDbContext database,
        IAuthorizationService authorization, ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(
            principal, businessId, BusinessRole.Accountant,
            database, authorization, cancellationToken);
        if (denied is not null) return denied;
        var invalid = ValidateCommand(request.RequestId, request.Version);
        if (invalid is not null) return invalid;

        try
        {
            var strategy = database.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync<IResult>(async () =>
            {
                await using var transaction = await database.Database
                    .BeginTransactionAsync(cancellationToken);
                var invoice = await database.Invoices
                    .Include(item => item.Lines).Include(item => item.Payments)
                    .SingleOrDefaultAsync(item => item.BusinessId == businessId &&
                        item.Id == invoiceId, cancellationToken);
                if (invoice is null) return ApiProblems.NotFound();
                var customerName = await database.Customers
                    .Where(item => item.BusinessId == businessId &&
                        item.Id == invoice.CustomerId)
                    .Select(item => item.Name).SingleAsync(cancellationToken);
                var replay = await FindCommandAsync(
                    request.RequestId, database, cancellationToken);
                if (replay is not null)
                {
                    return MatchesDueDateCommand(
                        replay, businessId, invoiceId, request.DueDate)
                            ? TypedResults.Ok(ToResponse(invoice, customerName))
                            : ApiProblems.Conflict(
                                "The command request ID was already used for different data.");
                }
                if (invoice.Version != request.Version)
                {
                    return ApiProblems.Conflict(
                        "The invoice changed after it was loaded. Reload before changing the due date.");
                }

                var now = DateTimeOffset.UtcNow;
                invoice.RescheduleDueDate(request.DueDate, now);
                AddLifecycleEvidence(
                    database, invoice, principal, request.RequestId,
                    "InvoiceDueDateChanged", now, null, null);
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                Mutations.Add(
                    1,
                    new KeyValuePair<string, object?>("operation", "due-date"));
                DueDateChanged(
                    loggerFactory.CreateLogger("Invoice.Api.Invoices"),
                    businessId,
                    invoice.Id,
                    null);
                return TypedResults.Ok(ToResponse(invoice, customerName));
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiProblems.Conflict(
                "The invoice changed while its due date was being updated.");
        }
        catch (DbUpdateException)
        {
            return ApiProblems.Conflict(
                "The due-date command conflicts with current database state.");
        }
        catch (DomainRuleViolationException error)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]>
            {
                ["dueDate"] = [error.Message]
            });
        }
    }

    private static async Task<IResult> AuditAsync(
        Guid businessId, Guid invoiceId, ClaimsPrincipal principal,
        InvoiceDbContext database, IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(
            principal, businessId, BusinessRole.Viewer, database, authorization, cancellationToken);
        if (denied is not null) return denied;
        if (!await database.Invoices.AsNoTracking().AnyAsync(
                invoice => invoice.BusinessId == businessId && invoice.Id == invoiceId,
                cancellationToken))
            return ApiProblems.NotFound();
        var records = await database.AuditEntries.AsNoTracking()
            .Where(entry => entry.BusinessId == businessId &&
                entry.AggregateId == invoiceId && entry.AggregateType == "Invoice")
            .OrderByDescending(entry => entry.OccurredAtUtc).ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken);
        var entries = records.Select(entry => new InvoiceAuditResponse(
            entry.Id, entry.Action, entry.ActorUserId,
            entry.OccurredAtUtc.UtcDateTime, entry.Details)).ToArray();
        return TypedResults.Ok(entries);
    }

    private static async Task<CandidateResult> BuildCandidateAsync(
        Guid invoiceId, Guid businessId, InvoiceWriteRequest request,
        InvoiceDbContext database, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (invoiceId == Guid.Empty) errors["requestId"] = ["A request ID is required."];
        if (request.CustomerId == Guid.Empty) errors["customerId"] = ["Select a customer."];
        if (request.Lines is null || request.Lines.Count is < 1 or > 200)
            errors["lines"] = ["Add between 1 and 200 invoice lines."];
        CurrencyCode? currency = null;
        try { currency = CurrencyCode.Parse(request.Currency); }
        catch (DomainRuleViolationException error) { errors["currency"] = [error.Message]; }
        if (currency is not null && currency.Value != "ZAR")
            errors["currency"] = ["Phase 6 supports ZAR invoices only."];
        var customer = request.CustomerId == Guid.Empty
            ? null
            : await database.Customers.AsNoTracking().SingleOrDefaultAsync(
                item => item.BusinessId == businessId &&
                    item.Id == request.CustomerId && item.IsActive,
                cancellationToken);
        if (request.CustomerId != Guid.Empty && customer is null)
            errors["customerId"] = ["Select an active customer for this business."];
        if (errors.Count > 0) return new(null, null, ApiProblems.Validation(errors));

        var drafts = new List<InvoiceLineDraft>(request.Lines!.Count);
        for (var index = 0; index < request.Lines.Count; index++)
        {
            var line = request.Lines[index];
            try
            {
                drafts.Add(new InvoiceLineDraft(
                    line.Description, line.Quantity,
                    Money.Create(line.UnitPrice, currency!),
                    TaxRate.Create(line.TaxRate)));
            }
            catch (DomainRuleViolationException error)
            {
                errors[$"lines[{index}]"] = [error.Message];
            }
        }
        if (errors.Count > 0) return new(null, null, ApiProblems.Validation(errors));
        try
        {
            var candidate = Domain.Invoices.Invoice.CreateDraft(
                invoiceId, businessId, request.CustomerId, currency!,
                request.IssueDate, request.DueDate, DateTimeOffset.UtcNow);
            candidate.ReplaceLines(drafts, DateTimeOffset.UtcNow);
            return new(candidate, customer!.Name, null);
        }
        catch (DomainRuleViolationException error)
        {
            errors["invoice"] = [error.Message];
            return new(null, null, ApiProblems.Validation(errors));
        }
    }

    private static ValidationProblem? ValidateCommand(Guid requestId, Guid version)
    {
        var errors = new Dictionary<string, string[]>();
        if (requestId == Guid.Empty) errors["requestId"] = ["A request ID is required."];
        if (version == Guid.Empty) errors["version"] = ["An invoice version is required."];
        return errors.Count == 0 ? null : ApiProblems.Validation(errors);
    }

    private static DateTimeOffset NormalizeDatabaseTimestamp(DateTime value)
    {
        var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return new DateTimeOffset(utc.Ticks - (utc.Ticks % 10), TimeSpan.Zero);
    }

    private static Task<OutboxMessage?> FindCommandAsync(
        Guid requestId, InvoiceDbContext database,
        CancellationToken cancellationToken) => database.OutboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(message => message.Id == requestId, cancellationToken);

    private static bool MatchesCommand(
        OutboxMessage message, string type, Guid businessId, Guid invoiceId)
    {
        if (message.Type != type) return false;
        try
        {
            var payload = JsonSerializer.Deserialize<InvoiceLifecycleEvent>(message.Payload);
            return payload is not null && payload.MessageId == message.Id &&
                payload.BusinessId == businessId && payload.InvoiceId == invoiceId;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool MatchesDueDateCommand(
        OutboxMessage message,
        Guid businessId,
        Guid invoiceId,
        DateOnly dueDate)
    {
        if (!MatchesCommand(
                message,
                "InvoiceDueDateChanged",
                businessId,
                invoiceId))
        {
            return false;
        }

        var payload = JsonSerializer.Deserialize<InvoiceLifecycleEvent>(
            message.Payload);
        return payload?.DueAtUtc == DueInstant.FromDueDate(dueDate);
    }

    private static void AddLifecycleEvidence(
        InvoiceDbContext database, Domain.Invoices.Invoice invoice,
        ClaimsPrincipal principal, Guid requestId, string type,
        DateTimeOffset occurredAtUtc, decimal? amount, string? idempotencyKey)
    {
        var @event = new InvoiceLifecycleEvent(
            requestId, invoice.BusinessId, invoice.Id,
            invoice.Number, invoice.Status.ToString(), invoice.Currency,
            invoice.TotalAmount, invoice.PaidAmount, invoice.OutstandingAmount,
            invoice.DueAtUtc, amount, idempotencyKey, occurredAtUtc);
        database.OutboxMessages.Add(new OutboxMessage
        {
            Id = requestId,
            Type = type,
            Payload = JsonSerializer.Serialize(@event),
            OccurredAtUtc = occurredAtUtc,
            NextAttemptAtUtc = occurredAtUtc
        });
        database.AuditEntries.Add(NewAudit(invoice, principal, type));
    }

    private static AuditEntry NewAudit(
        Domain.Invoices.Invoice invoice, ClaimsPrincipal principal, string action)
    {
        Guid? actor = Guid.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? userId : null;
        return new AuditEntry(
            Guid.NewGuid(), invoice.BusinessId, "Invoice", invoice.Id, action,
            actor, DateTimeOffset.UtcNow,
            JsonSerializer.Serialize(new
            {
                invoice.Version,
                invoice.Currency,
                invoice.Number,
                Status = invoice.Status.ToString(),
                invoice.SubtotalAmount,
                invoice.TaxAmount,
                invoice.TotalAmount,
                invoice.PaidAmount,
                invoice.OutstandingAmount,
                LineCount = invoice.Lines.Count
            }));
    }

    private static async Task<IResult?> AuthorizeAsync(
        ClaimsPrincipal principal, Guid businessId, BusinessRole minimumRole,
        InvoiceDbContext database, IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(subject, out var userId)) return ApiProblems.NotFound();
        var membership = await database.BusinessMemberships.AsNoTracking()
            .SingleOrDefaultAsync(item => item.BusinessId == businessId &&
                item.UserId == userId && item.IsActive, cancellationToken);
        if (membership is null) return ApiProblems.NotFound();
        return (await authorization.AuthorizeAsync(
            principal, membership, new BusinessRoleRequirement(minimumRole))).Succeeded
                ? null : ApiProblems.Forbidden();
    }

    private static InvoiceResponse ToResponse(
        Domain.Invoices.Invoice invoice, string customerName) => new(
            invoice.Id, invoice.CustomerId, customerName, invoice.Number,
            invoice.Status.ToString(), invoice.Currency, invoice.IssueDate,
            invoice.DueDate,
            invoice.Lines.OrderBy(line => line.Position).Select(line =>
                new InvoiceLineResponse(
                    line.Id, line.Position, line.Description, line.Quantity,
                    line.UnitPriceAmount, line.TaxRatePercentage, line.NetAmount,
                    line.TaxAmount, line.TotalAmount)).ToArray(),
            invoice.SubtotalAmount, invoice.TaxAmount, invoice.TotalAmount,
            invoice.PaidAmount, invoice.OutstandingAmount, invoice.Version,
            invoice.CreatedAtUtc.UtcDateTime, invoice.UpdatedAtUtc.UtcDateTime);

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private sealed record CandidateResult(
        Domain.Invoices.Invoice? Invoice, string? CustomerName, IResult? Error);

    private sealed record InvoiceListDatabaseRow(
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
}

public abstract record InvoiceWriteRequest(
    Guid CustomerId, string Currency, DateOnly IssueDate, DateOnly DueDate,
    IReadOnlyList<InvoiceLineWriteRequest>? Lines);

public sealed record InvoiceCreateRequest(
    Guid RequestId, Guid CustomerId, string Currency, DateOnly IssueDate,
    DateOnly DueDate, IReadOnlyList<InvoiceLineWriteRequest>? Lines) :
    InvoiceWriteRequest(CustomerId, Currency, IssueDate, DueDate, Lines);

public sealed record InvoiceUpdateRequest(
    Guid Version, Guid CustomerId, string Currency, DateOnly IssueDate,
    DateOnly DueDate, IReadOnlyList<InvoiceLineWriteRequest>? Lines) :
    InvoiceWriteRequest(CustomerId, Currency, IssueDate, DueDate, Lines);

public sealed record InvoiceCommandRequest(Guid RequestId, Guid Version);

public sealed record InvoiceDueDateRequest(
    Guid RequestId,
    Guid Version,
    DateOnly DueDate);

public sealed record InvoicePaymentRequest(
    Guid RequestId, Guid Version, string IdempotencyKey,
    decimal Amount, DateTime ReceivedAtUtc);

public sealed record InvoiceLineWriteRequest(
    string Description, decimal Quantity, decimal UnitPrice, decimal TaxRate);

public sealed record InvoiceLineResponse(
    Guid Id, int Position, string Description, decimal Quantity,
    decimal UnitPrice, decimal TaxRate, decimal NetAmount,
    decimal TaxAmount, decimal TotalAmount);

public sealed record InvoiceResponse(
    Guid Id, Guid CustomerId, string CustomerName, string? Number, string Status,
    string Currency, DateOnly IssueDate, DateOnly DueDate,
    IReadOnlyList<InvoiceLineResponse> Lines, decimal SubtotalAmount,
    decimal TaxAmount, decimal TotalAmount, decimal PaidAmount,
    decimal OutstandingAmount, Guid Version, DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record InvoiceSummaryResponse(
    Guid Id, string CustomerName, string? Number, string Status, string Currency,
    DateOnly IssueDate, DateOnly DueDate, decimal TotalAmount,
    decimal OutstandingAmount, Guid Version, DateTime UpdatedAtUtc);

public sealed record InvoicePageResponse(
    IReadOnlyList<InvoiceSummaryResponse> Items, int Page, int PageSize,
    int TotalCount, int TotalPages);

public sealed record InvoiceAuditResponse(
    Guid Id, string Action, Guid? ActorUserId, DateTime OccurredAtUtc,
    string Details);
