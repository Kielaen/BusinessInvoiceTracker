using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var isOpenApiBuild = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    builder.Logging.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(
            ResourceBuilder.CreateDefault().AddService("phase0-auth-api"));
        options.AddOtlpExporter();
    });
}

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("phase0-auth-api"))
    .WithTracing(tracing =>
    {
        tracing
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
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter();
        }
    });

var keysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "artifacts", "keys");
var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName("BusinessInvoiceTracker.Phase0");
if (isOpenApiBuild)
{
    dataProtection.UseEphemeralDataProtectionProvider();
}
else
{
    Directory.CreateDirectory(keysPath);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
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
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();
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
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();

app.MapGet("/health/live", () => TypedResults.Ok(new { status = "live" }))
    .ExcludeFromDescription();

app.MapGet("/api/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
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
    return TypedResults.Ok(new AntiforgeryResponse(tokens.HeaderName!, tokens.RequestToken!));
})
.WithName("GetAntiforgeryToken")
.WithTags("Authentication");

app.MapPost("/api/login", async Task<Results<Ok<UserResponse>, UnauthorizedHttpResult>> (
    LoginRequest request,
    HttpContext context) =>
{
    var role = request.Username switch
    {
        "owner" => "Owner",
        "viewer" => "Viewer",
        _ => null
    };

    if (role is null || request.Password != "phase0-only")
    {
        return TypedResults.Unauthorized();
    }

    var principal = new ClaimsPrincipal(
        new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, request.Username),
            new Claim(ClaimTypes.Name, request.Username),
            new Claim(ClaimTypes.Role, role)
        ],
        CookieAuthenticationDefaults.AuthenticationScheme));

    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal);
    return TypedResults.Ok(new UserResponse(request.Username, role));
})
.AddEndpointFilter<ValidateAntiforgeryFilter>()
.WithName("Login")
.WithTags("Authentication")
.Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

app.MapPost("/api/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return TypedResults.NoContent();
})
.RequireAuthorization()
.AddEndpointFilter<ValidateAntiforgeryFilter>()
.WithName("Logout")
.WithTags("Authentication")
.Produces(StatusCodes.Status401Unauthorized)
.Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

app.MapGet("/api/v1/auth/me", (ClaimsPrincipal user) =>
    TypedResults.Ok(new UserResponse(
        user.Identity!.Name!,
        user.FindFirstValue(ClaimTypes.Role)!)))
    .RequireAuthorization()
    .WithName("GetCurrentUser")
    .WithTags("Authentication")
    .Produces(StatusCodes.Status401Unauthorized);

app.MapGet("/api/v1/owner-proof", () => TypedResults.Ok(new { allowed = true }))
    .RequireAuthorization(policy => policy.RequireRole("Owner"))
    .WithName("GetOwnerProof")
    .WithTags("Authentication")
    .Produces(StatusCodes.Status401Unauthorized)
    .Produces(StatusCodes.Status403Forbidden);

app.MapPost("/api/v1/mutation-proof", (MutationRequest request) =>
    TypedResults.Ok(new MutationResponse(request.Value, DateTimeOffset.UtcNow)))
    .RequireAuthorization()
    .AddEndpointFilter<ValidateAntiforgeryFilter>()
    .WithName("PostMutationProof")
    .WithTags("Authentication")
    .Produces(StatusCodes.Status401Unauthorized)
    .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

app.MapGet("/api/v1/contracts/invoice", () =>
    TypedResults.Ok(new InvoiceContract(
        Guid.Parse("11111111-1111-4111-8111-111111111111"),
        InvoiceContractStatus.Issued,
        "1234.50",
        DateTimeOffset.Parse("2026-07-30T12:00:00Z"),
        null,
        new PageMetadata(1, 25, 1))))
    .WithName("GetInvoiceContract")
    .WithTags("Contract")
    .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

app.Run();

public sealed class ValidateAntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid antiforgery token",
                type: "https://business-invoice-tracker.invalid/problems/invalid-antiforgery-token");
        }
        return await next(context);
    }
}

public sealed record AntiforgeryResponse(string HeaderName, string Token);
public sealed record LoginRequest(
    [property: Required, MinLength(1), StringLength(64)] string Username,
    [property: Required, MinLength(1), StringLength(128)] string Password);
public sealed record UserResponse(string Username, string Role);
public sealed record MutationRequest(
    [property: Required, MinLength(1), StringLength(100)] string Value);
public sealed record MutationResponse(string Value, DateTimeOffset AcceptedAt);
public sealed record PageMetadata(
    [property: Range(1, int.MaxValue)] int Page,
    [property: Range(1, 100)] int PageSize,
    [property: Range(0, int.MaxValue)] int TotalCount);
public sealed record InvoiceContract(
    Guid Id,
    InvoiceContractStatus Status,
    [property: RegularExpression(@"^(0|[1-9]\d*)\.\d{2}$")] string Total,
    DateTimeOffset IssuedAt,
    string? PurchaseOrder,
    PageMetadata Page);
public enum InvoiceContractStatus
{
    Draft,
    Issued,
    Paid
}

public partial class Program;
