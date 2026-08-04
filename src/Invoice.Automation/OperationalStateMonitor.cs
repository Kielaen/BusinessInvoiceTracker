using System.Diagnostics.Metrics;
using Invoice.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Invoice.Automation;

public sealed partial class OperationalStateMonitor : BackgroundService
{
    private static readonly Meter Meter = new("Invoice.Automation.Operations");
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OperationalStateMonitor> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private OperationalStateSnapshot _latest = OperationalStateSnapshot.Empty;

    public OperationalStateMonitor(
        IServiceScopeFactory scopeFactory,
        ILogger<OperationalStateMonitor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        Meter.CreateObservableGauge(
            "invoice.outbox.pending",
            () => Latest.PendingOutboxCount);
        Meter.CreateObservableGauge(
            "invoice.outbox.oldest.age",
            () => Latest.OldestOutboxAgeSeconds,
            unit: "s");
        Meter.CreateObservableGauge(
            "invoice.domain.events.retained",
            () => Latest.RetainedDomainEventCount);
        Meter.CreateObservableGauge(
            "invoice.workflow.commands.pending",
            () => Latest.PendingWorkflowCommandCount);
        Meter.CreateObservableGauge(
            "invoice.workflow.commands.failed",
            () => Latest.FailedWorkflowCommandCount);
        Meter.CreateObservableGauge(
            "invoice.automation.deadletters.unresolved",
            () => Latest.UnresolvedDeadLetterCount);
        Meter.CreateObservableGauge(
            "invoice.notifications.pending",
            () => Latest.PendingNotificationCount);
        Meter.CreateObservableGauge(
            "invoice.notifications.failed",
            () => Latest.FailedNotificationCount);
        Meter.CreateObservableGauge(
            "invoice.operations.snapshot.stale",
            () => Latest.IsStale ? 1 : 0);
    }

    public OperationalStateSnapshot Latest => Volatile.Read(ref _latest);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshOnceAsync(stoppingToken);
        }
    }

    public async Task<OperationalStateSnapshot> RefreshOnceAsync(
        CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<InvoiceDbContext>();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var now = DateTimeOffset.UtcNow;
            var pendingOutbox = await database.OutboxMessages.AsNoTracking()
                .CountAsync(
                    item => item.PublishedAtUtc == null &&
                        item.Type != "InvoiceBecameOverdue",
                    timeout.Token);
            var oldestOutbox = await database.OutboxMessages.AsNoTracking()
                .Where(item => item.PublishedAtUtc == null &&
                    item.Type != "InvoiceBecameOverdue")
                .MinAsync(item => (DateTimeOffset?)item.OccurredAtUtc, timeout.Token);
            var retainedDomainEvents = await database.OutboxMessages.AsNoTracking()
                .CountAsync(
                    item => item.PublishedAtUtc == null &&
                        item.Type == "InvoiceBecameOverdue",
                    timeout.Token);
            var pendingCommands = await database.WorkflowCommands.AsNoTracking()
                .CountAsync(
                    item => item.DispatchedAtUtc == null && item.FailedAtUtc == null,
                    timeout.Token);
            var failedCommands = await database.WorkflowCommands.AsNoTracking()
                .CountAsync(item => item.FailedAtUtc != null, timeout.Token);
            var unresolvedDeadLetters = await database.AutomationDeadLetters
                .AsNoTracking()
                .CountAsync(item => item.ResolvedAtUtc == null, timeout.Token);
            var pendingNotifications = await database.NotificationDeliveries
                .AsNoTracking()
                .CountAsync(
                    item => item.Status == NotificationDeliveryStatuses.Pending,
                    timeout.Token);
            var failedNotifications = await database.NotificationDeliveries
                .AsNoTracking()
                .CountAsync(
                    item => item.Status == NotificationDeliveryStatuses.Failed,
                    timeout.Token);
            var snapshot = new OperationalStateSnapshot(
                now,
                now,
                false,
                null,
                pendingOutbox,
                oldestOutbox is null
                    ? 0
                    : Math.Max(0, (long)(now - oldestOutbox.Value).TotalSeconds),
                retainedDomainEvents,
                pendingCommands,
                failedCommands,
                unresolvedDeadLetters,
                pendingNotifications,
                failedNotifications);
            Volatile.Write(ref _latest, snapshot);
            return snapshot;
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            var previous = Latest;
            var snapshot = previous with
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                IsStale = true,
                LastError = exception.GetType().Name
            };
            Volatile.Write(ref _latest, snapshot);
            LogRefreshFailed(_logger, exception);
            return snapshot;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    [LoggerMessage(
        EventId = 8301,
        Level = LogLevel.Warning,
        Message = "The operational state snapshot is stale because refresh failed.")]
    private static partial void LogRefreshFailed(
        ILogger logger,
        Exception exception);
}

public sealed record OperationalStateSnapshot(
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset? LastSuccessfulRefreshAtUtc,
    bool IsStale,
    string? LastError,
    int PendingOutboxCount,
    long OldestOutboxAgeSeconds,
    int RetainedDomainEventCount,
    int PendingWorkflowCommandCount,
    int FailedWorkflowCommandCount,
    int UnresolvedDeadLetterCount,
    int PendingNotificationCount,
    int FailedNotificationCount)
{
    public static OperationalStateSnapshot Empty { get; } = new(
        DateTimeOffset.MinValue,
        null,
        true,
        "NotYetRefreshed",
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0);
}
