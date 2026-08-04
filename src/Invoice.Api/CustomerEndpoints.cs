using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using Invoice.Api.Authorization;
using Invoice.Domain.Businesses;
using Invoice.Domain.Common;
using Invoice.Domain.Customers;
using Invoice.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Invoice.Api;

public static class CustomerEndpoints
{
    private static readonly ActivitySource Activity = new("Invoice.Api.Customers");
    private static readonly Meter Meter = new("Invoice.Api.Customers");
    private static readonly Counter<long> Mutations =
        Meter.CreateCounter<long>("invoice.customers.mutations");
    private static readonly Action<ILogger, Guid, Guid, Exception?> CustomerCreated =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Information,
            new EventId(5001, nameof(CustomerCreated)),
            "Customer created for BusinessId {BusinessId} CustomerId {CustomerId}");
    private static readonly Action<ILogger, Guid, Guid, Exception?> CustomerUpdated =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Information,
            new EventId(5002, nameof(CustomerUpdated)),
            "Customer updated for BusinessId {BusinessId} CustomerId {CustomerId}");

    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/businesses/{businessId:guid}/customers")
            .RequireAuthorization()
            .WithTags("Customers");

        group.MapGet("", ListCustomersAsync)
            .WithName("ListCustomers")
            .Produces<CustomerPageResponse>()
            .ProducesValidationProblem()
            .Produces<ProblemDetails>(401, "application/problem+json")
            .Produces<ProblemDetails>(403, "application/problem+json")
            .Produces<ProblemDetails>(404, "application/problem+json");

        group.MapGet("/{customerId:guid}", GetCustomerAsync)
            .WithName("GetCustomer")
            .Produces<CustomerResponse>()
            .Produces<ProblemDetails>(401, "application/problem+json")
            .Produces<ProblemDetails>(403, "application/problem+json")
            .Produces<ProblemDetails>(404, "application/problem+json");

        group.MapPost("", CreateCustomerAsync)
            .AddEndpointFilter<ValidateAntiforgeryFilter>()
            .WithName("CreateCustomer")
            .Produces<CustomerResponse>(201)
            .Produces<CustomerResponse>(200)
            .ProducesValidationProblem()
            .Produces<ProblemDetails>(401, "application/problem+json")
            .Produces<ProblemDetails>(403, "application/problem+json")
            .Produces<ProblemDetails>(404, "application/problem+json")
            .Produces<ProblemDetails>(409, "application/problem+json");

        group.MapPut("/{customerId:guid}", UpdateCustomerAsync)
            .AddEndpointFilter<ValidateAntiforgeryFilter>()
            .WithName("UpdateCustomer")
            .Produces<CustomerResponse>()
            .ProducesValidationProblem()
            .Produces<ProblemDetails>(401, "application/problem+json")
            .Produces<ProblemDetails>(403, "application/problem+json")
            .Produces<ProblemDetails>(404, "application/problem+json")
            .Produces<ProblemDetails>(409, "application/problem+json");
    }

    private static async Task<IResult> ListCustomersAsync(
        Guid businessId,
        string? search,
        int? page,
        int? pageSize,
        ClaimsPrincipal principal,
        InvoiceDbContext database,
        IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? 20;
        var errors = new Dictionary<string, string[]>();
        if (currentPage < 1) errors["page"] = ["Page must be at least 1."];
        if (currentPageSize is < 1 or > 100) errors["pageSize"] = ["Page size must be between 1 and 100."];
        if (search?.Length > 100) errors["search"] = ["Search cannot exceed 100 characters."];
        if (errors.Count > 0) return ApiProblems.Validation(errors);

        var membership = await LoadMembershipAsync(principal, businessId, database, cancellationToken);
        if (membership is null) return ApiProblems.NotFound();
        if (!(await authorization.AuthorizeAsync(
                principal,
                membership,
                new BusinessRoleRequirement(BusinessRole.Viewer))).Succeeded)
        {
            return ApiProblems.Forbidden();
        }

        var query = database.Customers
            .AsNoTracking()
            .Where(customer => customer.BusinessId == businessId && customer.IsActive);
        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrEmpty(normalizedSearch))
        {
            var pattern = $"%{EscapeLike(normalizedSearch)}%";
            query = query.Where(customer =>
                EF.Functions.ILike(customer.Name, pattern, "\\") ||
                EF.Functions.ILike(customer.Email, pattern, "\\"));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(customer => customer.Name)
            .ThenBy(customer => customer.Id)
            .Skip((currentPage - 1) * currentPageSize)
            .Take(currentPageSize)
            .Select(customer => ToResponse(customer))
            .ToListAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)currentPageSize);

        return TypedResults.Ok(new CustomerPageResponse(
            items,
            currentPage,
            currentPageSize,
            totalCount,
            totalPages));
    }

    private static async Task<IResult> GetCustomerAsync(
        Guid businessId,
        Guid customerId,
        ClaimsPrincipal principal,
        InvoiceDbContext database,
        IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var access = await AuthorizeAsync(
            principal,
            businessId,
            BusinessRole.Viewer,
            database,
            authorization,
            cancellationToken);
        if (access is not null) return access;

        var customer = await database.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.BusinessId == businessId && candidate.Id == customerId,
                cancellationToken);
        return customer is null
            ? ApiProblems.NotFound()
            : TypedResults.Ok(ToResponse(customer));
    }

    private static async Task<IResult> CreateCustomerAsync(
        Guid businessId,
        CustomerCreateRequest request,
        ClaimsPrincipal principal,
        InvoiceDbContext database,
        IAuthorizationService authorization,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var access = await AuthorizeAsync(
            principal,
            businessId,
            BusinessRole.Accountant,
            database,
            authorization,
            cancellationToken);
        if (access is not null) return access;

        var validation = Validate(request, requireRequestId: true);
        if (validation.Errors.Count > 0) return ApiProblems.Validation(validation.Errors);
        var candidate = Customer.Create(
            request.RequestId,
            businessId,
            request.Name,
            validation.Email!,
            request.Phone,
            request.BillingAddressLine1,
            request.BillingAddressLine2,
            request.BillingCity,
            request.BillingPostalCode,
            request.BillingCountryCode,
            DateTimeOffset.UtcNow);

        var existing = await database.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                customer => customer.BusinessId == businessId && customer.Id == request.RequestId,
                cancellationToken);
        if (existing is not null)
        {
            return existing.HasSameDetails(candidate)
                ? TypedResults.Ok(ToResponse(existing))
                : ApiProblems.Conflict("The create request ID was already used for different customer data.");
        }

        using var activity = Activity.StartActivity("customer.create");
        activity?.SetTag("business.id", businessId);
        activity?.SetTag("customer.id", candidate.Id);
        database.Customers.Add(candidate);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            existing = await database.Customers
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    customer => customer.BusinessId == businessId && customer.Id == request.RequestId,
                    cancellationToken);
            if (existing is not null && existing.HasSameDetails(candidate))
            {
                return TypedResults.Ok(ToResponse(existing));
            }
            return ApiProblems.Conflict("A customer with this email or request ID already exists in the business.");
        }

        Mutations.Add(1, new KeyValuePair<string, object?>("operation", "create"));
        CustomerCreated(
            loggerFactory.CreateLogger("Invoice.Api.Customers"),
            businessId,
            candidate.Id,
            null);
        return TypedResults.Created(
            $"/api/v1/businesses/{businessId}/customers/{candidate.Id}",
            ToResponse(candidate));
    }

    private static async Task<IResult> UpdateCustomerAsync(
        Guid businessId,
        Guid customerId,
        CustomerUpdateRequest request,
        ClaimsPrincipal principal,
        InvoiceDbContext database,
        IAuthorizationService authorization,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var access = await AuthorizeAsync(
            principal,
            businessId,
            BusinessRole.Accountant,
            database,
            authorization,
            cancellationToken);
        if (access is not null) return access;

        var validation = Validate(request, requireRequestId: false);
        if (request.Version == Guid.Empty)
        {
            validation.Errors["version"] = ["The last observed customer version is required."];
        }
        if (validation.Errors.Count > 0) return ApiProblems.Validation(validation.Errors);

        var customer = await database.Customers.SingleOrDefaultAsync(
            candidate => candidate.BusinessId == businessId && candidate.Id == customerId,
            cancellationToken);
        if (customer is null) return ApiProblems.NotFound();
        if (customer.Version != request.Version)
        {
            return ApiProblems.Conflict("The customer changed after it was loaded. Reload the latest version.");
        }

        using var activity = Activity.StartActivity("customer.update");
        activity?.SetTag("business.id", businessId);
        activity?.SetTag("customer.id", customerId);
        customer.Update(
            request.Name,
            validation.Email!,
            request.Phone,
            request.BillingAddressLine1,
            request.BillingAddressLine2,
            request.BillingCity,
            request.BillingPostalCode,
            request.BillingCountryCode,
            DateTimeOffset.UtcNow);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiProblems.Conflict("The customer changed concurrently. Reload the latest version.");
        }
        catch (DbUpdateException)
        {
            return ApiProblems.Conflict("A customer with this email already exists in the business.");
        }

        Mutations.Add(1, new KeyValuePair<string, object?>("operation", "update"));
        CustomerUpdated(
            loggerFactory.CreateLogger("Invoice.Api.Customers"),
            businessId,
            customer.Id,
            null);
        return TypedResults.Ok(ToResponse(customer));
    }

    private static CustomerValidation Validate(
        CustomerWriteRequest request,
        bool requireRequestId)
    {
        var errors = new Dictionary<string, string[]>();
        if (requireRequestId && request is CustomerCreateRequest create && create.RequestId == Guid.Empty)
            errors["requestId"] = ["A stable create request ID is required."];
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160)
            errors["name"] = ["Name is required and cannot exceed 160 characters."];
        EmailAddress? email = null;
        try { email = EmailAddress.Parse(request.Email); }
        catch (DomainRuleViolationException) { errors["email"] = ["Enter a valid email address."]; }
        if (request.Phone?.Trim().Length > 32)
            errors["phone"] = ["Phone cannot exceed 32 characters."];
        ValidateRequired(request.BillingAddressLine1, 200, "billingAddressLine1", errors);
        if (request.BillingAddressLine2?.Trim().Length > 200)
            errors["billingAddressLine2"] = ["Address line 2 cannot exceed 200 characters."];
        ValidateRequired(request.BillingCity, 120, "billingCity", errors);
        ValidateRequired(request.BillingPostalCode, 32, "billingPostalCode", errors);
        var country = request.BillingCountryCode?.Trim();
        if (country is null || country.Length != 2 || country.Any(character => !char.IsAsciiLetter(character)))
            errors["billingCountryCode"] = ["Enter a two-letter country code."];
        return new CustomerValidation(errors, email);
    }

    private static void ValidateRequired(
        string value,
        int maximumLength,
        string field,
        Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
            errors[field] = [$"A value is required and cannot exceed {maximumLength} characters."];
    }

    private static async Task<IResult?> AuthorizeAsync(
        ClaimsPrincipal principal,
        Guid businessId,
        BusinessRole minimumRole,
        InvoiceDbContext database,
        IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var membership = await LoadMembershipAsync(principal, businessId, database, cancellationToken);
        if (membership is null) return ApiProblems.NotFound();
        return (await authorization.AuthorizeAsync(
            principal,
            membership,
            new BusinessRoleRequirement(minimumRole))).Succeeded
                ? null
                : ApiProblems.Forbidden();
    }

    private static async Task<BusinessMembership?> LoadMembershipAsync(
        ClaimsPrincipal principal,
        Guid businessId,
        InvoiceDbContext database,
        CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(subject, out var userId)) return null;
        return await database.BusinessMemberships
            .AsNoTracking()
            .SingleOrDefaultAsync(
                membership => membership.BusinessId == businessId &&
                    membership.UserId == userId && membership.IsActive,
                cancellationToken);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static CustomerResponse ToResponse(Customer customer) => new(
        customer.Id,
        customer.Name,
        customer.Email,
        customer.Phone,
        customer.BillingAddressLine1,
        customer.BillingAddressLine2,
        customer.BillingCity,
        customer.BillingPostalCode,
        customer.BillingCountryCode,
        customer.IsActive,
        customer.Version,
        customer.CreatedAtUtc.UtcDateTime,
        customer.UpdatedAtUtc.UtcDateTime);

    private sealed record CustomerValidation(
        Dictionary<string, string[]> Errors,
        EmailAddress? Email);
}

public abstract record CustomerWriteRequest(
    string Name,
    string Email,
    string? Phone,
    string BillingAddressLine1,
    string? BillingAddressLine2,
    string BillingCity,
    string BillingPostalCode,
    string BillingCountryCode);

public sealed record CustomerCreateRequest(
    Guid RequestId,
    string Name,
    string Email,
    string? Phone,
    string BillingAddressLine1,
    string? BillingAddressLine2,
    string BillingCity,
    string BillingPostalCode,
    string BillingCountryCode) : CustomerWriteRequest(
        Name, Email, Phone, BillingAddressLine1, BillingAddressLine2,
        BillingCity, BillingPostalCode, BillingCountryCode);

public sealed record CustomerUpdateRequest(
    Guid Version,
    string Name,
    string Email,
    string? Phone,
    string BillingAddressLine1,
    string? BillingAddressLine2,
    string BillingCity,
    string BillingPostalCode,
    string BillingCountryCode) : CustomerWriteRequest(
        Name, Email, Phone, BillingAddressLine1, BillingAddressLine2,
        BillingCity, BillingPostalCode, BillingCountryCode);

public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    string BillingAddressLine1,
    string? BillingAddressLine2,
    string BillingCity,
    string BillingPostalCode,
    string BillingCountryCode,
    bool IsActive,
    Guid Version,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CustomerPageResponse(
    IReadOnlyList<CustomerResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
