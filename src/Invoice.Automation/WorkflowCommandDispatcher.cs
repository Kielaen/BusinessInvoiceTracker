using System.Text.Json;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dapr.Workflow;
using Grpc.Core;
using Invoice.Application;
using Invoice.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Invoice.Automation;

public sealed partial class WorkflowCommandDispatcher(
    IServiceScopeFactory scopeFactory,
    DaprWorkflowClient workflowClient,
    ILogger<WorkflowCommandDispatcher> logger) : BackgroundService
{
    private static readonly ActivitySource ActivitySource =
        new("Invoice.Automation.WorkflowCommands");
    private static readonly Meter Meter =
        new("Invoice.Automation.WorkflowCommands");
    private static readonly Counter<long> DispatchOutcomes =
        Meter.CreateCounter<long>("invoice.workflow.command.dispatch.outcomes");
    private readonly Guid _leaseOwner = Guid.NewGuid();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var commands = await ClaimAsync(stoppingToken);
                foreach (var command in commands)
                {
                    await DispatchAsync(command, stoppingToken);
                }
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException ||
                !stoppingToken.IsCancellationRequested)
            {
                LogCycleFailed(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<IReadOnlyList<WorkflowCommand>> ClaimAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<InvoiceDbContext>();
        var strategy = database.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await database.Database.BeginTransactionAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var commands = await database.WorkflowCommands
                .FromSqlInterpolated($"""
                    SELECT * FROM workflow_commands
                    WHERE "DispatchedAtUtc" IS NULL
                      AND "FailedAtUtc" IS NULL
                      AND "NextAttemptAtUtc" <= {now}
                      AND ("LeaseUntilUtc" IS NULL OR "LeaseUntilUtc" < {now})
                    ORDER BY "CreatedAtUtc", "Id"
                    FOR UPDATE SKIP LOCKED
                    LIMIT 10
                    """)
                .ToListAsync(cancellationToken);
            foreach (var command in commands)
            {
                command.LeaseOwner = _leaseOwner;
                command.LeaseUntilUtc = now.AddSeconds(30);
            }
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return commands;
        });
    }

    private async Task DispatchAsync(
        WorkflowCommand claimed,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity(
            "workflow.command.dispatch",
            ActivityKind.Producer);
        activity?.SetTag("workflow.command.id", claimed.Id);
        activity?.SetTag("workflow.command.kind", claimed.Kind);
        activity?.SetTag("invoice.id", claimed.InvoiceId);
        using var operationTimeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        operationTimeout.CancelAfter(TimeSpan.FromSeconds(3));
        var operationToken = operationTimeout.Token;
        try
        {
            if (claimed.Kind == WorkflowCommandKinds.ManualNotificationRetry)
            {
                var retry = JsonSerializer.Deserialize<ManualNotificationRetryInput>(
                    claimed.Payload) ?? throw new JsonException(
                        "Manual notification retry payload is empty.");
                var retryInstanceId = ManualNotificationRetryWorkflow.InstanceId(
                    retry.DeliveryId,
                    retry.RequestId);
                var retryState = await workflowClient.GetWorkflowStateAsync(
                    retryInstanceId,
                    getInputsAndOutputs: false,
                    operationToken);
                if (retryState is null || !retryState.Exists)
                {
                    await workflowClient.ScheduleNewWorkflowAsync(
                        nameof(ManualNotificationRetryWorkflow),
                        retryInstanceId,
                        retry,
                        startTime: null,
                        operationToken);
                }
                await CompleteAsync(claimed.Id, null, cancellationToken);
                RecordOutcome(activity, "dispatched");
                LogDispatched(
                    logger,
                    claimed.Id,
                    retryInstanceId,
                    claimed.Kind);
                return;
            }

            var lifecycleEvent = JsonSerializer.Deserialize<InvoiceLifecycleEvent>(
                claimed.Payload) ?? throw new JsonException(
                    "Workflow command payload is empty.");
            var instanceId = OverdueTrackingWorkflow.InstanceId(claimed.InvoiceId);
            if (claimed.Kind == WorkflowCommandKinds.Start)
            {
                if (lifecycleEvent.DueAtUtc is null)
                {
                    throw new JsonException("Issued event has no due instant.");
                }

                var state = await workflowClient.GetWorkflowStateAsync(
                    instanceId,
                    getInputsAndOutputs: false,
                    operationToken);
                if (state is null || !state.Exists)
                {
                    await workflowClient.ScheduleNewWorkflowAsync(
                        nameof(OverdueTrackingWorkflow),
                        instanceId,
                        new OverdueWorkflowInput(
                            claimed.BusinessId,
                            claimed.InvoiceId,
                            lifecycleEvent.DueAtUtc.Value),
                        startTime: null,
                        operationToken);
                }
            }
            else if (claimed.Kind == WorkflowCommandKinds.Signal)
            {
                var state = await workflowClient.GetWorkflowStateAsync(
                    instanceId,
                    getInputsAndOutputs: false,
                    operationToken);
                if ((state is null || !state.Exists) &&
                    lifecycleEvent.Status == "Voided" &&
                    lifecycleEvent.InvoiceNumber is null)
                {
                    await CompleteAsync(claimed.Id, null, cancellationToken);
                    RecordOutcome(activity, "skipped");
                    LogSkippedDraftVoid(logger, claimed.Id, claimed.InvoiceId);
                    return;
                }

                if (state is not null && state.Exists &&
                    state.IsWorkflowCompleted)
                {
                    await CompleteAsync(claimed.Id, null, cancellationToken);
                    RecordOutcome(activity, "skipped");
                    LogSkippedCompleted(logger, claimed.Id, claimed.InvoiceId);
                    return;
                }

                await workflowClient.RaiseEventAsync(
                    instanceId,
                    OverdueTrackingWorkflow.StateChangedEventName,
                    new WorkflowStateChange(
                        lifecycleEvent.MessageId,
                        lifecycleEvent.Status,
                        lifecycleEvent.DueAtUtc,
                        lifecycleEvent.OccurredAtUtc),
                    operationToken);
            }
            else
            {
                throw new JsonException(
                    $"Unknown workflow command kind '{claimed.Kind}'.");
            }

            await CompleteAsync(claimed.Id, null, cancellationToken);
            RecordOutcome(activity, "dispatched");
            LogDispatched(logger, claimed.Id, instanceId, claimed.Kind);
        }
        catch (Exception exception) when (
            exception is RpcException or JsonException or TimeoutException ||
            exception is OperationCanceledException &&
            !cancellationToken.IsCancellationRequested)
        {
            await CompleteAsync(claimed.Id, exception, cancellationToken);
            RecordOutcome(activity, "pending");
            LogRetry(logger, claimed.Id, claimed.Attempts + 1, exception);
        }
    }

    private static void RecordOutcome(Activity? activity, string outcome)
    {
        DispatchOutcomes.Add(
            1,
            new KeyValuePair<string, object?>("outcome", outcome));
        activity?.SetTag("workflow.command.outcome", outcome);
    }

    private async Task CompleteAsync(
        Guid commandId,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<InvoiceDbContext>();
        var command = await database.WorkflowCommands.SingleAsync(
            item => item.Id == commandId && item.LeaseOwner == _leaseOwner,
            cancellationToken);
        var now = DateTimeOffset.UtcNow;
        command.Attempts++;
        command.LeaseOwner = null;
        command.LeaseUntilUtc = null;
        if (exception is null)
        {
            command.DispatchedAtUtc = now;
            command.LastError = null;
        }
        else
        {
            command.LastError = exception.Message.Length <= 1000
                ? exception.Message
                : exception.Message[..1000];
            if (command.Attempts >= 10)
            {
                command.FailedAtUtc = now;
            }
            else
            {
                command.NextAttemptAtUtc = now.Add(
                    TimeSpan.FromSeconds(Math.Min(30, 1 << command.Attempts)));
            }
        }
        await database.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(
        EventId = 8101,
        Level = LogLevel.Information,
        Message = "Dispatched workflow command {CommandId} to {WorkflowId} as {Kind}.")]
    private static partial void LogDispatched(
        ILogger logger,
        Guid commandId,
        string workflowId,
        string kind);

    [LoggerMessage(
        EventId = 8102,
        Level = LogLevel.Warning,
        Message = "Workflow command {CommandId} remains pending after attempt {Attempt}.")]
    private static partial void LogRetry(
        ILogger logger,
        Guid commandId,
        int attempt,
        Exception exception);

    [LoggerMessage(
        EventId = 8103,
        Level = LogLevel.Information,
        Message = "Completed workflow command {CommandId} without a workflow because draft invoice {InvoiceId} was voided before issue.")]
    private static partial void LogSkippedDraftVoid(
        ILogger logger,
        Guid commandId,
        Guid invoiceId);

    [LoggerMessage(
        EventId = 8104,
        Level = LogLevel.Information,
        Message = "Completed workflow command {CommandId} without signaling because invoice workflow {InvoiceId} is already complete.")]
    private static partial void LogSkippedCompleted(
        ILogger logger,
        Guid commandId,
        Guid invoiceId);

    [LoggerMessage(
        EventId = 8105,
        Level = LogLevel.Error,
        Message = "Workflow command dispatch cycle failed before a command could be completed; durable rows remain available for recovery.")]
    private static partial void LogCycleFailed(
        ILogger logger,
        Exception exception);
}

public static class WorkflowCommandKinds
{
    public const string Start = "Start";
    public const string Signal = "Signal";
    public const string ManualNotificationRetry = "ManualNotificationRetry";
}
