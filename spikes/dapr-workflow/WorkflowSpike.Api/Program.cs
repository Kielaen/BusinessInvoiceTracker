using Dapr.Workflow;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    builder.Logging.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(
            ResourceBuilder.CreateDefault().AddService("phase0-workflow-worker"));
        options.AddOtlpExporter();
    });
}

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("phase0-workflow-worker"))
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

builder.Services.AddDaprWorkflow(options =>
{
    options.RegisterWorkflow<DurabilityWorkflow>();
});

var app = builder.Build();

app.MapGet("/health/live", () => TypedResults.Ok(new { status = "live" }));

app.MapPost("/workflows/{instanceId}", async (
    string instanceId,
    ScheduleRequest request,
    DaprWorkflowClient client,
    CancellationToken cancellationToken) =>
{
    if (request.DueInSeconds is < 1 or > 120)
    {
        return Results.BadRequest(new { error = "dueInSeconds must be between 1 and 120." });
    }
    if (request.WaitBeforeEventRegistrationSeconds is < 0 or > 30)
    {
        return Results.BadRequest(new { error = "waitBeforeEventRegistrationSeconds must be between 0 and 30." });
    }

    var input = new WorkflowInput(
        DateTimeOffset.UtcNow.AddSeconds(request.DueInSeconds),
        request.WaitBeforeEventRegistrationSeconds);
    await client.ScheduleNewWorkflowAsync(
        nameof(DurabilityWorkflow),
        instanceId,
        input,
        startTime: null,
        cancellationToken);
    return Results.Accepted($"/workflows/{instanceId}", new { instanceId, input.DueAtUtc });
});

app.MapPost("/workflows/{instanceId}/events/control", async (
    string instanceId,
    ControlEvent control,
    DaprWorkflowClient client,
    CancellationToken cancellationToken) =>
{
    await client.RaiseEventAsync(instanceId, "control", control, cancellationToken);
    return Results.Accepted();
});

app.MapGet("/workflows/{instanceId}", async (
    string instanceId,
    DaprWorkflowClient client,
    CancellationToken cancellationToken) =>
{
    var state = await client.GetWorkflowStateAsync(
        instanceId,
        getInputsAndOutputs: true,
        cancellationToken);
    if (state is null || !state.Exists)
    {
        return Results.NotFound();
    }

    WorkflowOutcome? output = null;
    if (state.IsWorkflowCompleted)
    {
        output = state.ReadOutputAs<WorkflowOutcome>();
    }

    return Results.Ok(new
    {
        instanceId,
        runtimeStatus = state.RuntimeStatus.ToString(),
        state.IsWorkflowRunning,
        state.IsWorkflowCompleted,
        output,
        failure = state.FailureDetails?.ErrorMessage
    });
});

app.Run();

public sealed class DurabilityWorkflow : Workflow<WorkflowInput, WorkflowOutcome>
{
    public override async Task<WorkflowOutcome> RunAsync(
        WorkflowContext context,
        WorkflowInput input)
    {
        if (input.WaitBeforeEventRegistrationSeconds > 0)
        {
            await context.CreateTimer(
                TimeSpan.FromSeconds(input.WaitBeforeEventRegistrationSeconds),
                CancellationToken.None);
        }

        using var timerCancellation = new CancellationTokenSource();
        var timer = context.CreateTimer(input.DueAtUtc.UtcDateTime, timerCancellation.Token);
        var externalEvent = context.WaitForExternalEventAsync<ControlEvent>(
            "control",
            CancellationToken.None);

        var winner = await Task.WhenAny(timer, externalEvent);
        if (winner == externalEvent)
        {
            timerCancellation.Cancel();
            var control = await externalEvent;
            return new WorkflowOutcome(
                "external-event",
                control.Action,
                context.CurrentUtcDateTime);
        }

        return new WorkflowOutcome("timer", null, context.CurrentUtcDateTime);
    }
}

public sealed record ScheduleRequest(
    int DueInSeconds,
    int WaitBeforeEventRegistrationSeconds = 0);
public sealed record WorkflowInput(
    DateTimeOffset DueAtUtc,
    int WaitBeforeEventRegistrationSeconds);
public sealed record ControlEvent(string Action);
public sealed record WorkflowOutcome(
    string CompletionKind,
    string? Signal,
    DateTime CompletedAtUtc);
