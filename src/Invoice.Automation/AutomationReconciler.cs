using System.Text.Json;
using Dapr.Workflow;
using Invoice.Application;
using Invoice.Domain.Invoices;
using Invoice.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Invoice.Automation;

public sealed partial class AutomationReconciler(
    IServiceScopeFactory scopeFactory,
    DaprWorkflowClient workflowClient,
    IHttpClientFactory httpClientFactory,
    ILogger<AutomationReconciler> logger) : BackgroundService
{
    private static readonly Guid ReconciliationNamespace =
        Guid.Parse("3f4c0a14-2835-5bd4-9216-8e199a906fcb");
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private int _invoiceScanOffset;
    private AutomationReconciliationReport _latest =
        AutomationReconciliationReport.Empty;

    public AutomationReconciliationReport Latest => _latest;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await TryReconcileOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TryReconcileOnceAsync(stoppingToken);
        }
    }

    private async Task TryReconcileOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ReconcileOnceAsync(cancellationToken);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            // ReconcileOnceAsync already emitted the structured failure. The next
            // periodic run retries from durable state; a dependency outage must
            // not terminate the automation host.
        }
    }

    public async Task<AutomationReconciliationReport> ReconcileOnceAsync(
        CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var startedAt = DateTimeOffset.UtcNow;
            var repairedStarts = 0;
            var repairedActiveSignals = 0;
            var repairedTerminalSignals = 0;
            var workflowDiscrepancies = 0;

            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<InvoiceDbContext>();
            var invoices = await database.Invoices.AsNoTracking()
                .Where(invoice => invoice.Status != InvoiceStatus.Draft)
                .OrderBy(invoice => invoice.UpdatedAtUtc)
                .ThenBy(invoice => invoice.Id)
                .Skip(_invoiceScanOffset)
                .Take(100)
                .Select(invoice => new ReconciliationInvoice(
                    invoice.Id,
                    invoice.BusinessId,
                    invoice.Number,
                    invoice.Status,
                    invoice.Currency,
                    invoice.TotalAmount,
                    invoice.PaidAmount,
                    invoice.DueAtUtc,
                    invoice.Version,
                    invoice.UpdatedAtUtc))
                .ToListAsync(cancellationToken);
            _invoiceScanOffset = invoices.Count == 100
                ? _invoiceScanOffset + invoices.Count
                : 0;

            foreach (var invoice in invoices)
            {
                var instanceId = OverdueTrackingWorkflow.InstanceId(invoice.Id);
                var state = await workflowClient.GetWorkflowStateAsync(
                    instanceId,
                    getInputsAndOutputs: false,
                    cancellationToken);
                var exists = state is not null && state.Exists;
                if (!exists && invoice.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid)
                {
                    workflowDiscrepancies++;
                    if (await EnsureCurrentCommandAsync(database, invoice, WorkflowCommandKinds.Start,
                            "InvoiceIssued", cancellationToken))
                    {
                        repairedStarts++;
                    }
                }
                else if (exists && state!.IsWorkflowRunning &&
                    invoice.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid)
                {
                    // Reassert the current authoritative due date once per invoice version.
                    // This repairs a broker-acknowledged event that was routed incorrectly or
                    // otherwise never reached the workflow subscriber.
                    if (await EnsureCurrentCommandAsync(database, invoice, WorkflowCommandKinds.Signal,
                            "InvoiceDueDateChanged", cancellationToken))
                    {
                        workflowDiscrepancies++;
                        repairedActiveSignals++;
                    }
                }
                else if (exists && state!.IsWorkflowRunning &&
                    invoice.Status is InvoiceStatus.Paid or InvoiceStatus.Voided)
                {
                    if (await EnsureCurrentCommandAsync(database, invoice, WorkflowCommandKinds.Signal,
                            invoice.Status == InvoiceStatus.Paid
                                ? "InvoicePaymentRecorded"
                                : "InvoiceVoided",
                            cancellationToken))
                    {
                        workflowDiscrepancies++;
                        repairedTerminalSignals++;
                    }
                }
            }

            var reconciledDeliveries = await ReconcileDeliveriesAsync(
                database,
                cancellationToken);
            var pendingCommands = await database.WorkflowCommands.CountAsync(
                command => command.DispatchedAtUtc == null && command.FailedAtUtc == null,
                cancellationToken);
            var failedCommands = await database.WorkflowCommands.CountAsync(
                command => command.FailedAtUtc != null,
                cancellationToken);
            var pendingDeliveries = await database.NotificationDeliveries.CountAsync(
                delivery => delivery.Status == NotificationDeliveryStatuses.Pending,
                cancellationToken);
            var failedDeliveries = await database.NotificationDeliveries.CountAsync(
                delivery => delivery.Status == NotificationDeliveryStatuses.Failed,
                cancellationToken);
            var unresolvedDeadLetters = await database.AutomationDeadLetters.CountAsync(
                message => message.ResolvedAtUtc == null,
                cancellationToken);

            _latest = new AutomationReconciliationReport(
                startedAt,
                DateTimeOffset.UtcNow,
                invoices.Count,
                workflowDiscrepancies,
                repairedStarts,
                repairedActiveSignals,
                repairedTerminalSignals,
                reconciledDeliveries,
                pendingCommands,
                failedCommands,
                pendingDeliveries,
                failedDeliveries,
                unresolvedDeadLetters);
            LogCompleted(
                logger,
                workflowDiscrepancies,
                repairedStarts + repairedActiveSignals + repairedTerminalSignals + reconciledDeliveries);
            return _latest;
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogFailed(logger, exception);
            throw;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private static async Task<bool> EnsureCurrentCommandAsync(
        InvoiceDbContext database,
        ReconciliationInvoice invoice,
        string kind,
        string eventType,
        CancellationToken cancellationToken)
    {
        var messageId = DeterministicGuid.Create(
            ReconciliationNamespace,
            $"{invoice.Id:N}:{invoice.Version:N}:{kind}:{eventType}");
        var existing = await database.WorkflowCommands
            .SingleOrDefaultAsync(command => command.Id == messageId, cancellationToken);
        if (existing is not null)
        {
            if (existing.DispatchedAtUtc is null && existing.FailedAtUtc is null)
            {
                return false;
            }

            existing.DispatchedAtUtc = null;
            existing.FailedAtUtc = null;
            existing.Attempts = 0;
            existing.LeaseOwner = null;
            existing.LeaseUntilUtc = null;
            existing.LastError = null;
            existing.NextAttemptAtUtc = DateTimeOffset.UtcNow;
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }

        var lifecycleEvent = new InvoiceLifecycleEvent(
            messageId,
            invoice.BusinessId,
            invoice.Id,
            invoice.Number,
            invoice.Status.ToString(),
            invoice.Currency,
            invoice.TotalAmount,
            invoice.PaidAmount,
            invoice.TotalAmount - invoice.PaidAmount,
            invoice.DueAtUtc,
            null,
            null,
            invoice.UpdatedAtUtc);
        database.WorkflowCommands.Add(new WorkflowCommand
        {
            Id = messageId,
            EventMessageId = messageId,
            BusinessId = invoice.BusinessId,
            InvoiceId = invoice.Id,
            Kind = kind,
            Payload = JsonSerializer.Serialize(lifecycleEvent),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            NextAttemptAtUtc = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<int> ReconcileDeliveriesAsync(
        InvoiceDbContext database,
        CancellationToken cancellationToken)
    {
        var deliveries = await database.NotificationDeliveries
            .Where(delivery => delivery.Status == NotificationDeliveryStatuses.Pending)
            .OrderBy(delivery => delivery.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
        var reconciled = 0;
        var client = httpClientFactory.CreateClient("mailpit");
        foreach (var delivery in deliveries)
        {
            var normalized = delivery.ProviderMessageId.Trim('<', '>');
            var query = Uri.EscapeDataString($"message-id:{normalized}");
            try
            {
                using var response = await client.GetAsync(
                    $"api/v1/search?query={query}",
                    cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!body.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var completedAt = DateTimeOffset.UtcNow;
                delivery.Attempts++;
                delivery.LastAttemptAtUtc = completedAt;
                delivery.Status = NotificationDeliveryStatuses.Delivered;
                delivery.DeliveredAtUtc = completedAt;
                delivery.LastError = null;
                database.NotificationAttempts.Add(new NotificationAttempt
                {
                    Id = DeterministicGuid.Create(
                        ReconciliationNamespace,
                        $"delivery:{delivery.Id:N}:{delivery.Attempts}"),
                    DeliveryId = delivery.Id,
                    AttemptNumber = delivery.Attempts,
                    StartedAtUtc = completedAt,
                    CompletedAtUtc = completedAt,
                    Outcome = NotificationAttemptOutcomes.Reconciled
                });
                reconciled++;
            }
            catch (HttpRequestException)
            {
                LogMailpitUnavailable(
                    logger,
                    delivery.Id);
            }
        }

        if (reconciled > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        return reconciled;
    }

    private sealed record ReconciliationInvoice(
        Guid Id,
        Guid BusinessId,
        string? Number,
        InvoiceStatus Status,
        string Currency,
        decimal TotalAmount,
        decimal PaidAmount,
        DateTimeOffset? DueAtUtc,
        Guid Version,
        DateTimeOffset UpdatedAtUtc);

    [LoggerMessage(
        EventId = 8301,
        Level = LogLevel.Information,
        Message = "Automation reconciliation completed with {Discrepancies} workflow discrepancies and {Repairs} repairs.")]
    private static partial void LogCompleted(
        ILogger logger,
        int discrepancies,
        int repairs);

    [LoggerMessage(
        EventId = 8302,
        Level = LogLevel.Error,
        Message = "Automation reconciliation failed.")]
    private static partial void LogFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 8303,
        Level = LogLevel.Warning,
        Message = "Mailpit reconciliation was unavailable for delivery {DeliveryId}.")]
    private static partial void LogMailpitUnavailable(
        ILogger logger,
        Guid deliveryId);
}

public sealed record AutomationReconciliationReport(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    int InvoicesScanned,
    int WorkflowDiscrepancies,
    int RepairedStarts,
    int RepairedActiveSignals,
    int RepairedTerminalSignals,
    int ReconciledDeliveries,
    int PendingCommands,
    int FailedCommands,
    int PendingDeliveries,
    int FailedDeliveries,
    int UnresolvedDeadLetters)
{
    public static AutomationReconciliationReport Empty { get; } = new(
        DateTimeOffset.MinValue,
        DateTimeOffset.MinValue,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
