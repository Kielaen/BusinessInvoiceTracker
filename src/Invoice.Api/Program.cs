using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Diagnostics;
using Invoice.Api.Authorization;
using Invoice.Api.Identity;
using Invoice.Application;
using Invoice.Api;
using Invoice.Domain.Businesses;
using Invoice.Infrastructure.Identity;
using Invoice.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

if (args.Contains("--healthcheck", StringComparer.Ordinal))
{
    using var healthClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(3)
    };
    try
    {
        using var response = await healthClient.GetAsync(
            "http://127.0.0.1:8080/health/ready");
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (Exception exception) when (
        exception is HttpRequestException or TaskCanceledException)
    {
        Environment.ExitCode = 1;
    }
    return;
}

var builder = WebApplication.CreateBuilder(args);
var isOpenApiBuild = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
var isMigration = args.Contains("--migrate", StringComparer.Ordinal);
var connectionString = builder.Configuration.GetConnectionString("InvoiceDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
{
    if (!isOpenApiBuild)
    {
        throw new InvalidOperationException(
            "ConnectionStrings:InvoiceDatabase must be configured.");
    }
    connectionString =
        "Host=127.0.0.1;Database=openapi_build;Username=openapi_build;Password=openapi_build";
}
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

builder.Logging.ClearProviders();
builder.Logging.Configure(options =>
    options.ActivityTrackingOptions =
        ActivityTrackingOptions.TraceId |
        ActivityTrackingOptions.SpanId |
        ActivityTrackingOptions.ParentId);
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    builder.Logging.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(
            ResourceBuilder.CreateDefault().AddService("invoice-api"));
        options.AddOtlpExporter();
    });
}

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("invoice-api"))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("Invoice.Api.Outbox")
            .AddSource("Invoice.Api.Customers")
            .AddSource("Invoice.Api.Invoices")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter();
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("Invoice.Api.Outbox")
            .AddMeter("Invoice.Api.Customers")
            .AddMeter("Invoice.Api.Invoices")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter();
        }
    });

builder.Services.AddInvoicePersistence(connectionString);
builder.Services.AddHttpClient(
    "dapr",
    client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["Dapr:HttpEndpoint"] ?? "http://127.0.0.1:3500");
        client.Timeout = TimeSpan.FromSeconds(3);
    });
builder.Services.AddHostedService<OutboxPublisher>();

var keysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "artifacts", "keys");
var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName("BusinessInvoiceTracker");
if (isOpenApiBuild || isMigration)
{
    dataProtection.UseEphemeralDataProtectionProvider();
}
else
{
    Directory.CreateDirectory(keysPath);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.SignIn.RequireConfirmedEmail = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<InvoiceDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = builder.Environment.IsDevelopment()
            ? "invoice-auth"
            : "__Host-invoice-auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.None
        : CookieSecurePolicy.Always;
    options.SlidingExpiration = false;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.Events.OnRedirectToLogin = context =>
        ApiProblems.Unauthorized().ExecuteAsync(context.HttpContext);
    options.Events.OnRedirectToAccessDenied = context =>
        ApiProblems.Forbidden().ExecuteAsync(context.HttpContext);
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<
    IAuthorizationHandler,
    BusinessMembershipAuthorizationHandler>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
        new ValueTask(ApiProblems.TooManyRequests().ExecuteAsync(context.HttpContext));
    options.AddFixedWindowLimiter(
        "login",
        limiter =>
        {
            limiter.PermitLimit = 10;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueLimit = 0;
        });
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = builder.Environment.IsDevelopment()
        ? "invoice-antiforgery"
        : "__Host-invoice-antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.None
        : CookieSecurePolicy.Always;
    options.HeaderName = "X-CSRF-TOKEN";
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance = context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] =
            context.HttpContext.TraceIdentifier;
    };
});

var app = builder.Build();

if (isMigration)
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<InvoiceDbContext>();
    await database.Database.MigrateAsync();
    await DemonstrationDataSeeder.SeedAsync(
        scope.ServiceProvider,
        builder.Configuration,
        builder.Environment);
    return;
}

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; base-uri 'self'; frame-ancestors 'none'; " +
        "form-action 'self'; object-src 'none'";
    await next(context);
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();
app.MapCustomerEndpoints();
app.MapInvoiceEndpoints();
app.MapDashboardEndpoints();

app.MapGet("/health/live", () => TypedResults.Ok(new { status = "live" }))
    .ExcludeFromDescription();
app.MapGet("/health/ready", async (
    InvoiceDbContext database,
    IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken) =>
{
    var databaseCheck = await CheckDatabaseAsync(database, cancellationToken);
    var daprCheck = await CheckHttpAsync(
        "dapr",
        httpClientFactory.CreateClient("dapr"),
        "/v1.0/healthz",
        required: true,
        cancellationToken);
    var checks = new[] { databaseCheck, daprCheck };
    var ready = checks.All(check => check.Status == "healthy");
    return Results.Json(
        new { status = ready ? "ready" : "unready", checks },
        statusCode: ready
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable);
})
    .ExcludeFromDescription();

app.MapGet("/api/v1/auth/antiforgery", (
    HttpContext context,
    IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    context.Response.Cookies.Append(
        "XSRF-TOKEN",
        tokens.RequestToken!,
        new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Strict,
            Secure = !app.Environment.IsDevelopment(),
            Path = "/"
        });
    return TypedResults.Ok(new AntiforgeryResponse(
        tokens.HeaderName!,
        tokens.RequestToken!));
})
.WithName("GetAntiforgeryToken")
.WithTags("Authentication");

app.MapPost("/api/v1/auth/login", async Task<IResult> (
    LoginRequest request,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    InvoiceDbContext database,
    CancellationToken cancellationToken) =>
{
    var validationErrors = new Dictionary<string, string[]>();
    if (string.IsNullOrWhiteSpace(request.Email) ||
        !request.Email.Contains('@', StringComparison.Ordinal))
    {
        validationErrors["email"] = ["Enter a valid email address."];
    }
    if (string.IsNullOrWhiteSpace(request.Password))
    {
        validationErrors["password"] = ["Enter your password."];
    }
    if (validationErrors.Count > 0)
    {
        return ApiProblems.InvalidLoginRequest(validationErrors);
    }

    var user = await userManager.FindByEmailAsync(request.Email);
    if (user is null)
    {
        return ApiProblems.Unauthorized();
    }

    var result = await signInManager.PasswordSignInAsync(
        user,
        request.Password,
        isPersistent: false,
        lockoutOnFailure: true);
    if (!result.Succeeded)
    {
        return ApiProblems.Unauthorized();
    }

    return TypedResults.Ok(await LoadCurrentUserAsync(
        user,
        database,
        cancellationToken));
})
    .AddEndpointFilter<ValidateAntiforgeryFilter>()
    .WithName("Login")
    .WithTags("Authentication")
    .Produces<CurrentUserResponse>()
    .ProducesValidationProblem()
    .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")
    .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")
    .RequireRateLimiting("login");

app.MapPost("/api/v1/auth/logout", async (
    SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return TypedResults.NoContent();
})
.RequireAuthorization()
.AddEndpointFilter<ValidateAntiforgeryFilter>()
.WithName("Logout")
.WithTags("Authentication")
.Produces(StatusCodes.Status204NoContent)
.Produces<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
.Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json");

app.MapGet("/api/v1/auth/me", async Task<IResult> (
    ClaimsPrincipal principal,
    UserManager<ApplicationUser> userManager,
    InvoiceDbContext database,
    CancellationToken cancellationToken) =>
{
    var user = await userManager.GetUserAsync(principal);
    return user is null
        ? ApiProblems.Unauthorized()
        : TypedResults.Ok(await LoadCurrentUserAsync(
            user,
            database,
            cancellationToken));
})
    .RequireAuthorization()
    .WithName("GetCurrentUser")
    .WithTags("Authentication")
    .Produces<CurrentUserResponse>()
    .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json");

app.MapGet("/api/v1/businesses", async (
    int? page,
    int? pageSize,
    ClaimsPrincipal principal,
    InvoiceDbContext database,
    CancellationToken cancellationToken) =>
{
    var currentPage = page ?? 1;
    var currentPageSize = pageSize ?? 20;
    var paginationErrors = ValidatePagination(currentPage, currentPageSize);
    if (paginationErrors.Count > 0)
    {
        return Results.ValidationProblem(paginationErrors);
    }

    var userId = GetRequiredUserId(principal);
    var query = database.BusinessMemberships
        .AsNoTracking()
        .Where(membership =>
            membership.UserId == userId &&
            membership.IsActive);
    var totalCount = await query.CountAsync(cancellationToken);
    var businesses = await query
        .OrderBy(membership => membership.Business.Name)
        .ThenBy(membership => membership.BusinessId)
        .Skip((currentPage - 1) * currentPageSize)
        .Take(currentPageSize)
        .Select(membership => new BusinessResponse(
            membership.BusinessId,
            membership.Business.Name,
            membership.Role.ToString()))
        .ToListAsync(cancellationToken);
    return Results.Ok(new BusinessPageResponse(
        businesses,
        currentPage,
        currentPageSize,
        totalCount,
        totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)currentPageSize)));
})
    .RequireAuthorization()
    .WithName("ListBusinesses")
    .WithTags("Businesses")
    .Produces<BusinessPageResponse>()
    .ProducesValidationProblem()
    .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json");

app.MapGet("/api/v1/businesses/{businessId:guid}", async Task<IResult> (
    Guid businessId,
    ClaimsPrincipal principal,
    InvoiceDbContext database,
    IAuthorizationService authorization,
    CancellationToken cancellationToken) =>
{
    var membership = await LoadMembershipAsync(
        principal,
        businessId,
        database,
        cancellationToken);
    if (membership is null)
    {
        return ApiProblems.NotFound();
    }

    var result = await authorization.AuthorizeAsync(
        principal,
        membership,
        new BusinessRoleRequirement(BusinessRole.Viewer));
    return result.Succeeded
        ? TypedResults.Ok(new BusinessResponse(
            membership.BusinessId,
            membership.Business.Name,
            membership.Role.ToString()))
        : ApiProblems.Forbidden();
})
    .RequireAuthorization()
    .WithName("GetBusiness")
    .WithTags("Businesses")
    .Produces<BusinessResponse>()
    .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")
    .Produces<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")
    .Produces<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");

app.MapGet(
    "/api/v1/businesses/{businessId:guid}/memberships",
    async Task<IResult> (
        Guid businessId,
        int? page,
        int? pageSize,
        ClaimsPrincipal principal,
        InvoiceDbContext database,
        IAuthorizationService authorization,
        CancellationToken cancellationToken) =>
{
    var currentPage = page ?? 1;
    var currentPageSize = pageSize ?? 20;
    var paginationErrors = ValidatePagination(currentPage, currentPageSize);
    if (paginationErrors.Count > 0)
    {
        return Results.ValidationProblem(paginationErrors);
    }

    var membership = await LoadMembershipAsync(
        principal,
        businessId,
        database,
        cancellationToken);
    if (membership is null)
    {
        return ApiProblems.NotFound();
    }

    var result = await authorization.AuthorizeAsync(
        principal,
        membership,
        new BusinessRoleRequirement(BusinessRole.Owner));
    if (!result.Succeeded)
    {
        return ApiProblems.Forbidden();
    }

    var query = database.BusinessMemberships
        .AsNoTracking()
        .Where(candidate => candidate.BusinessId == businessId)
        .Join(
            database.Users,
            candidate => candidate.UserId,
            user => user.Id,
            (membership, user) => new { membership, user });
    var totalCount = await query.CountAsync(cancellationToken);
    var members = await query
        .OrderBy(candidate => candidate.user.Email)
        .ThenBy(candidate => candidate.user.Id)
        .Skip((currentPage - 1) * currentPageSize)
        .Take(currentPageSize)
        .Select(candidate => new MembershipResponse(
            candidate.user.Id,
            candidate.user.Email!,
            candidate.membership.Role.ToString(),
            candidate.membership.IsActive))
        .ToListAsync(cancellationToken);
    return Results.Ok(new MembershipPageResponse(
        members,
        currentPage,
        currentPageSize,
        totalCount,
        totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)currentPageSize)));
})
    .RequireAuthorization()
    .WithName("ListMemberships")
    .WithTags("Memberships")
    .Produces<MembershipPageResponse>()
    .ProducesValidationProblem()
    .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")
    .Produces<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")
    .Produces<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");

app.MapPost("/api/v1/walking-skeleton", async (
    ClaimsPrincipal user,
    InvoiceDbContext database,
    CancellationToken cancellationToken) =>
{
    var now = DateTimeOffset.UtcNow;
    var probe = new DeliveryProbe
    {
        Id = Guid.NewGuid(),
        RequestedBy = user.Identity!.Name!,
        CreatedAtUtc = now
    };
    var messageId = Guid.NewGuid();
    var @event = new WalkingSkeletonEvent(
        messageId,
        probe.Id,
        probe.RequestedBy,
        now);
    var outbox = new OutboxMessage
    {
        Id = messageId,
        Type = nameof(WalkingSkeletonEvent),
        Payload = JsonSerializer.Serialize(@event),
        OccurredAtUtc = now,
        NextAttemptAtUtc = now
    };

    database.DeliveryProbes.Add(probe);
    database.OutboxMessages.Add(outbox);
    await database.SaveChangesAsync(cancellationToken);

    return TypedResults.Accepted(
        $"/api/v1/walking-skeleton/{probe.Id}",
        new WalkingSkeletonAccepted(probe.Id, messageId, "pending"));
})
.RequireAuthorization()
.AddEndpointFilter<ValidateAntiforgeryFilter>()
.WithName("CreateWalkingSkeletonProbe")
.WithTags("WalkingSkeleton")
.Produces<WalkingSkeletonAccepted>(StatusCodes.Status202Accepted)
.Produces<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
.Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json");

app.MapGet("/api/v1/walking-skeleton/{probeId:guid}", async Task<IResult> (
    Guid probeId,
    ClaimsPrincipal user,
    InvoiceDbContext database,
    CancellationToken cancellationToken) =>
{
    var requestedBy = user.Identity!.Name!;
    var probe = await database.DeliveryProbes
        .AsNoTracking()
        .SingleOrDefaultAsync(
            x => x.Id == probeId && x.RequestedBy == requestedBy,
            cancellationToken);
    if (probe is null)
    {
        return ApiProblems.NotFound();
    }

    var receipt = await database.AutomationReceipts
        .AsNoTracking()
        .SingleOrDefaultAsync(x => x.ProbeId == probeId, cancellationToken);
    return TypedResults.Ok(new WalkingSkeletonStatus(
        probe.Id,
        receipt is null ? "pending" : "processed",
        receipt?.ProcessedAtUtc.UtcDateTime));
})
.RequireAuthorization()
.WithName("GetWalkingSkeletonProbe")
.WithTags("WalkingSkeleton")
.Produces<WalkingSkeletonStatus>()
.Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")
.Produces<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");

app.Run();

static Guid GetRequiredUserId(ClaimsPrincipal principal)
{
    var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);

    if (!Guid.TryParse(subject, out var userId))
    {
        throw new InvalidOperationException("The authenticated principal does not contain a valid user identifier.");
    }

    return userId;
}

static Dictionary<string, string[]> ValidatePagination(int page, int pageSize)
{
    var errors = new Dictionary<string, string[]>();
    if (page < 1)
    {
        errors["page"] = ["Page must be at least 1."];
    }
    if (pageSize is < 1 or > 100)
    {
        errors["pageSize"] = ["Page size must be between 1 and 100."];
    }
    return errors;
}

static async Task<BusinessMembership?> LoadMembershipAsync(
    ClaimsPrincipal principal,
    Guid businessId,
    InvoiceDbContext database,
    CancellationToken cancellationToken)
{
    var userId = GetRequiredUserId(principal);

    return await database.BusinessMemberships
        .AsNoTracking()
        .Include(membership => membership.Business)
        .SingleOrDefaultAsync(
            membership =>
                membership.BusinessId == businessId &&
                membership.UserId == userId &&
                membership.IsActive,
            cancellationToken);
}

static async Task<CurrentUserResponse> LoadCurrentUserAsync(
    ApplicationUser user,
    InvoiceDbContext database,
    CancellationToken cancellationToken)
{
    var memberships = await database.BusinessMemberships
        .AsNoTracking()
        .Where(membership => membership.UserId == user.Id && membership.IsActive)
        .OrderBy(membership => membership.Business.Name)
        .ThenBy(membership => membership.BusinessId)
        .Take(100)
        .Select(membership => new MembershipSummary(
            membership.BusinessId,
            membership.Business.Name,
            membership.Role.ToString()))
        .ToListAsync(cancellationToken);

    return new CurrentUserResponse(user.Id, user.Email!, memberships);
}

static async Task<DependencyCheckResponse> CheckDatabaseAsync(
    InvoiceDbContext database,
    CancellationToken cancellationToken)
{
    var stopwatch = Stopwatch.StartNew();
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
        cancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(2));
    try
    {
        var healthy = await database.Database.CanConnectAsync(timeout.Token);
        return new DependencyCheckResponse(
            "postgresql",
            healthy ? "healthy" : "unhealthy",
            true,
            stopwatch.ElapsedMilliseconds);
    }
    catch (Exception exception) when (
        exception is not OperationCanceledException ||
        !cancellationToken.IsCancellationRequested)
    {
        return new DependencyCheckResponse(
            "postgresql",
            "unhealthy",
            true,
            stopwatch.ElapsedMilliseconds);
    }
}

static async Task<DependencyCheckResponse> CheckHttpAsync(
    string name,
    HttpClient client,
    string path,
    bool required,
    CancellationToken cancellationToken)
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        using var response = await client.GetAsync(path, cancellationToken);
        return new DependencyCheckResponse(
            name,
            response.IsSuccessStatusCode ? "healthy" : "unhealthy",
            required,
            stopwatch.ElapsedMilliseconds);
    }
    catch (Exception exception) when (
        exception is HttpRequestException or TaskCanceledException)
    {
        return new DependencyCheckResponse(
            name,
            "unhealthy",
            required,
            stopwatch.ElapsedMilliseconds);
    }
}

sealed record DependencyCheckResponse(
    string Name,
    string Status,
    bool Required,
    long DurationMilliseconds);
