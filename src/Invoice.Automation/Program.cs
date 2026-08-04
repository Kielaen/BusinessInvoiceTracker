using Invoice.Application;
using Invoice.Automation;
using Invoice.Infrastructure.Persistence;
using Dapr.Workflow;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
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
var connectionString = builder.Configuration.GetConnectionString("InvoiceDatabase")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:InvoiceDatabase must be configured.");
var daprHttpEndpoint = builder.Configuration["Dapr:HttpEndpoint"]
    ?? "http://127.0.0.1:3500";
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
            ResourceBuilder.CreateDefault().AddService("invoice-automation"));
        options.AddOtlpExporter();
    });
}

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("invoice-automation"))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("Invoice.Automation.Notifications")
            .AddSource("Invoice.Automation.WorkflowCommands")
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
            .AddMeter("Invoice.Automation.Operations")
            .AddMeter("Invoice.Automation.Notifications")
            .AddMeter("Invoice.Automation.WorkflowCommands")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter();
        }
    });

builder.Services.AddInvoicePersistence(connectionString);
builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<OverdueTrackingWorkflow>();
    options.RegisterWorkflow<ManualNotificationRetryWorkflow>();
    options.RegisterActivity<EvaluateOverdueActivity>();
    options.RegisterActivity<DeliverOverdueNotificationActivity>();
});
builder.Services.AddHostedService<WorkflowCommandDispatcher>();
builder.Services.AddSingleton<AutomationReconciler>();
builder.Services.AddHostedService(serviceProvider =>
    serviceProvider.GetRequiredService<AutomationReconciler>());
builder.Services.AddSingleton<OperationalStateMonitor>();
builder.Services.AddHostedService(serviceProvider =>
    serviceProvider.GetRequiredService<OperationalStateMonitor>());
builder.Services.AddHttpClient(
    "dapr",
    client =>
    {
        client.BaseAddress = new Uri(daprHttpEndpoint);
        client.Timeout = TimeSpan.FromSeconds(3);
    });
builder.Services.AddHttpClient(
    "mailpit",
    client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["Mailpit:BaseUri"] ?? "http://mailpit:8025/");
        client.Timeout = TimeSpan.FromSeconds(3);
    });
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();

app.MapGet("/health/live", () => TypedResults.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (
    InvoiceDbContext database,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var databaseCheck = await CheckDatabaseAsync(database, cancellationToken);
    var daprCheck = await CheckHttpAsync(
        "dapr",
        httpClientFactory.CreateClient("dapr"),
        "/v1.0/healthz",
        required: true,
        cancellationToken);
    var mailpitCheck = await CheckHttpAsync(
        "mailpit",
        httpClientFactory.CreateClient("mailpit"),
        "/readyz",
        required: false,
        cancellationToken);
    var checks = new List<DependencyCheckResponse>
    {
        databaseCheck,
        daprCheck,
        mailpitCheck
    };
    foreach (var dependency in new[]
    {
        (Name: "placement", Endpoint: configuration["Dapr:PlacementEndpoint"]),
        (Name: "scheduler", Endpoint: configuration["Dapr:SchedulerEndpoint"])
    })
    {
        if (!string.IsNullOrWhiteSpace(dependency.Endpoint))
        {
            checks.Add(await CheckTcpAsync(
                dependency.Name,
                dependency.Endpoint,
                cancellationToken));
        }
    }
    var ready = checks
        .Where(check => check.Required)
        .All(check => check.Status == "healthy");
    var degraded = ready && checks.Any(check => check.Status != "healthy");
    return Results.Json(
        new { status = ready ? degraded ? "degraded" : "ready" : "unready", checks },
        statusCode: ready
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable);
});

app.MapGet("/internal/reconciliation", (
    AutomationReconciler reconciler) => TypedResults.Ok(reconciler.Latest));
app.MapGet("/internal/operations/status", async (
    OperationalStateMonitor monitor,
    CancellationToken cancellationToken) => TypedResults.Ok(
        await monitor.RefreshOnceAsync(cancellationToken)));
app.MapPost("/internal/reconciliation/run", async (
    AutomationReconciler reconciler,
    CancellationToken cancellationToken) => TypedResults.Ok(
        await reconciler.ReconcileOnceAsync(cancellationToken)));
app.MapPost("/internal/notification-deliveries/{deliveryId:guid}/retry", async (
    Guid deliveryId,
    ManualNotificationRetryRequest request,
    InvoiceDbContext database,
    CancellationToken cancellationToken) =>
{
    if (request.RequestId == Guid.Empty)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["requestId"] = ["A request ID is required."]
        });
    }

    var strategy = database.Database.CreateExecutionStrategy();
    return await strategy.ExecuteAsync<IResult>(async () =>
    {
        await using var transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);
        var existing = await database.WorkflowCommands.AsNoTracking()
            .SingleOrDefaultAsync(
                command => command.Id == request.RequestId,
                cancellationToken);
        if (existing is not null)
        {
            var same = existing.Kind == WorkflowCommandKinds.ManualNotificationRetry &&
                JsonSerializer.Deserialize<ManualNotificationRetryInput>(
                    existing.Payload)?.DeliveryId == deliveryId;
            return same
                ? Results.Accepted(value: new
                {
                    request.RequestId,
                    DeliveryId = deliveryId,
                    status = "Accepted"
                })
                : Results.Conflict(new { detail = "Request ID was already used." });
        }

        var delivery = await database.NotificationDeliveries.SingleOrDefaultAsync(
            item => item.Id == deliveryId,
            cancellationToken);
        if (delivery is null)
        {
            return Results.NotFound();
        }
        if (delivery.Status != NotificationDeliveryStatuses.Failed)
        {
            return Results.Conflict(new
            {
                detail = "Only a terminal failed delivery can be retried manually."
            });
        }

        delivery.Status = NotificationDeliveryStatuses.Pending;
        delivery.AttemptCycleStart = delivery.Attempts;
        delivery.FailedAtUtc = null;
        delivery.LastError = null;
        var payload = new ManualNotificationRetryInput(request.RequestId, deliveryId);
        database.WorkflowCommands.Add(new WorkflowCommand
        {
            Id = request.RequestId,
            EventMessageId = request.RequestId,
            BusinessId = delivery.BusinessId,
            InvoiceId = delivery.InvoiceId,
            Kind = WorkflowCommandKinds.ManualNotificationRetry,
            Payload = JsonSerializer.Serialize(payload),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            NextAttemptAtUtc = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Accepted(value: new
        {
            request.RequestId,
            DeliveryId = deliveryId,
            status = "Accepted"
        });
    });
});

app.MapGet("/dapr/subscribe", () => Results.Ok(new[]
{
    new
    {
        pubsubname = "pubsub",
        topic = "walking-skeleton",
        route = "events/walking-skeleton",
        deadLetterTopic = (string?)null
    },
    new
    {
        pubsubname = "pubsub",
        topic = "invoice-lifecycle",
        route = "events/invoice-lifecycle",
        deadLetterTopic = (string?)"invoice-lifecycle-deadletter"
    },
    new
    {
        pubsubname = "pubsub",
        topic = "invoice-lifecycle-deadletter",
        route = "events/invoice-lifecycle-deadletter",
        deadLetterTopic = (string?)null
    }
}));

app.MapPost("/events/invoice-lifecycle-deadletter", async (
    DaprCloudEvent<JsonElement> cloudEvent,
    InvoiceDbContext database,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    await database.Database.ExecuteSqlInterpolatedAsync(
        $"""
        INSERT INTO automation_dead_letters
            ("MessageId", "EventType", "Payload", "ReceivedAtUtc")
        VALUES (
            {cloudEvent.Id}, {cloudEvent.Type},
            {cloudEvent.Data.GetRawText()}::jsonb, {DateTimeOffset.UtcNow})
        ON CONFLICT ("MessageId") DO NOTHING
        """,
        cancellationToken);
    AutomationLog.DeadLettered(logger, cloudEvent.Id, cloudEvent.Type);
    return Results.NoContent();
});

app.MapPost("/events/invoice-lifecycle", async (
    DaprCloudEvent<InvoiceLifecycleEvent> cloudEvent,
    InvoiceDbContext database,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var supported = cloudEvent.Type is
        "InvoiceIssued" or "InvoicePaymentRecorded" or "InvoiceVoided" or
        "InvoiceDueDateChanged";
    var @event = cloudEvent.Data;
    if (!supported || @event.MessageId == Guid.Empty ||
        @event.BusinessId == Guid.Empty || @event.InvoiceId == Guid.Empty ||
        cloudEvent.Id != @event.MessageId.ToString())
    {
        AutomationLog.InvalidLifecycle(logger, cloudEvent.Id, cloudEvent.Type);
        return Results.BadRequest();
    }

    var strategy = database.Database.CreateExecutionStrategy();
    var inserted = await strategy.ExecuteAsync(async () =>
    {
        await using var transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);
        var inboxRows = await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO inbox_messages ("MessageId", "ReceivedAtUtc")
            VALUES ({@event.MessageId}, {DateTimeOffset.UtcNow})
            ON CONFLICT ("MessageId") DO NOTHING
            """,
            cancellationToken);
        if (inboxRows != 0)
        {
            database.WorkflowCommands.Add(new WorkflowCommand
            {
                Id = @event.MessageId,
                EventMessageId = @event.MessageId,
                BusinessId = @event.BusinessId,
                InvoiceId = @event.InvoiceId,
                Kind = cloudEvent.Type == "InvoiceIssued"
                    ? WorkflowCommandKinds.Start
                    : WorkflowCommandKinds.Signal,
                Payload = System.Text.Json.JsonSerializer.Serialize(@event),
                CreatedAtUtc = @event.OccurredAtUtc,
                NextAttemptAtUtc = DateTimeOffset.UtcNow
            });
            await database.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return inboxRows;
    });

    if (inserted == 0)
    {
        AutomationLog.DuplicateLifecycle(logger, @event.MessageId);
    }
    else
    {
        AutomationLog.AcceptedLifecycle(
            logger,
            @event.MessageId,
            @event.BusinessId,
            @event.InvoiceId,
            cloudEvent.Type);
    }
    return Results.NoContent();
});

app.MapPost("/events/walking-skeleton", async (
    DaprCloudEvent<WalkingSkeletonEvent> cloudEvent,
    InvoiceDbContext database,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var @event = cloudEvent.Data;
    if (@event.MessageId == Guid.Empty || @event.ProbeId == Guid.Empty ||
        string.IsNullOrWhiteSpace(@event.RequestedBy))
    {
        AutomationLog.InvalidWalkingSkeleton(logger, cloudEvent.Id);
        return Results.NoContent();
    }
    var strategy = database.Database.CreateExecutionStrategy();
    var inserted = await strategy.ExecuteAsync(async () =>
    {
        await using var transaction =
            await database.Database.BeginTransactionAsync(cancellationToken);
        var inboxRows = await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO inbox_messages ("MessageId", "ReceivedAtUtc")
            VALUES ({@event.MessageId}, {DateTimeOffset.UtcNow})
            ON CONFLICT ("MessageId") DO NOTHING
            """,
            cancellationToken);
        if (inboxRows != 0)
        {
            database.AutomationReceipts.Add(new AutomationReceipt
            {
                Id = Guid.NewGuid(),
                MessageId = @event.MessageId,
                ProbeId = @event.ProbeId,
                RequestedBy = @event.RequestedBy,
                ProcessedAtUtc = DateTimeOffset.UtcNow
            });
            await database.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return inboxRows;
    });

    if (inserted == 0)
    {
        AutomationLog.Duplicate(logger, @event.MessageId);
        return Results.NoContent();
    }

    AutomationLog.Processed(logger, @event.MessageId, @event.ProbeId);
    return Results.NoContent();
});

app.Run();

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

static async Task<DependencyCheckResponse> CheckTcpAsync(
    string name,
    string endpoint,
    CancellationToken cancellationToken)
{
    var stopwatch = Stopwatch.StartNew();
    var separator = endpoint.LastIndexOf(':');
    if (separator <= 0 ||
        !int.TryParse(endpoint[(separator + 1)..], out var port))
    {
        return new DependencyCheckResponse(
            name,
            "unhealthy",
            true,
            stopwatch.ElapsedMilliseconds);
    }

    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
        cancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(1));
    try
    {
        using var client = new TcpClient();
        await client.ConnectAsync(endpoint[..separator], port, timeout.Token);
        return new DependencyCheckResponse(
            name,
            "healthy",
            true,
            stopwatch.ElapsedMilliseconds);
    }
    catch (Exception exception) when (
        exception is SocketException or OperationCanceledException &&
        !cancellationToken.IsCancellationRequested)
    {
        return new DependencyCheckResponse(
            name,
            "unhealthy",
            true,
            stopwatch.ElapsedMilliseconds);
    }
}

sealed record DependencyCheckResponse(
    string Name,
    string Status,
    bool Required,
    long DurationMilliseconds);
