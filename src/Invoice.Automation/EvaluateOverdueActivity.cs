using System.Text.Json;
using Dapr.Workflow;
using Invoice.Domain.Auditing;
using Invoice.Domain.Invoices;
using Invoice.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Invoice.Automation;

public sealed partial class EvaluateOverdueActivity(
    IServiceScopeFactory scopeFactory,
    ILogger<EvaluateOverdueActivity> logger) :
    WorkflowActivity<OverdueWorkflowInput, OverdueEvaluationResult>
{
    private static readonly Guid DeliveryNamespace =
        Guid.Parse("e2107b84-4ad8-5b2a-a94c-93102421ff71");
    private static readonly Guid AuditNamespace =
        Guid.Parse("f9710740-d8c8-54c3-918c-48bf599f5a73");
    private static readonly Guid OutboxNamespace =
        Guid.Parse("4c602924-9cd1-51e9-876f-b4c918908b8b");

    public override async Task<OverdueEvaluationResult> RunAsync(
        WorkflowActivityContext context,
        OverdueWorkflowInput input)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<InvoiceDbContext>();
        var strategy = database.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await database.Database.BeginTransactionAsync();
            var invoice = await database.Invoices.SingleOrDefaultAsync(item =>
                item.BusinessId == input.BusinessId && item.Id == input.InvoiceId);
            if (invoice is null)
            {
                return new OverdueEvaluationResult("Missing", null, null);
            }

            if (invoice.Status is InvoiceStatus.Paid or InvoiceStatus.Voided)
            {
                return new OverdueEvaluationResult(
                    invoice.Status.ToString(), null, invoice.DueAtUtc);
            }

            var now = DateTimeOffset.UtcNow;
            if (invoice.DueAtUtc is null || now < invoice.DueAtUtc.Value)
            {
                return new OverdueEvaluationResult(
                    "Reschedule", null, invoice.DueAtUtc);
            }

            var deliveryId = DeterministicGuid.Create(
                DeliveryNamespace,
                $"{invoice.Id:N}:overdue-v1");
            var existing = await database.NotificationDeliveries
                .SingleOrDefaultAsync(delivery => delivery.Id == deliveryId);
            if (existing is not null)
            {
                return new OverdueEvaluationResult(
                    "DeliveryReady", existing.Id, invoice.DueAtUtc);
            }

            if (invoice.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid)
            {
                invoice.MarkOverdue(now);
            }
            else if (invoice.Status != InvoiceStatus.Overdue)
            {
                return new OverdueEvaluationResult(
                    invoice.Status.ToString(), null, invoice.DueAtUtc);
            }

            var recipient = await database.Customers
                .Where(customer => customer.BusinessId == invoice.BusinessId &&
                    customer.Id == invoice.CustomerId)
                .Select(customer => customer.Email)
                .SingleAsync();
            var providerMessageId =
                $"<overdue-{deliveryId:N}@business-invoice-tracker.local>";
            database.NotificationDeliveries.Add(new NotificationDelivery
            {
                Id = deliveryId,
                BusinessId = invoice.BusinessId,
                InvoiceId = invoice.Id,
                WorkflowGeneration = "overdue-v1",
                Recipient = recipient,
                ProviderMessageId = providerMessageId,
                CreatedAtUtc = now
            });
            database.AuditEntries.Add(new AuditEntry(
                DeterministicGuid.Create(AuditNamespace, $"{invoice.Id:N}:overdue-v1"),
                invoice.BusinessId,
                "Invoice",
                invoice.Id,
                "InvoiceBecameOverdue",
                null,
                now,
                JsonSerializer.Serialize(new
                {
                    workflowId = context.InstanceId,
                    deliveryId,
                    invoice.DueAtUtc,
                    invoice.OutstandingAmount
                })));
            database.OutboxMessages.Add(new OutboxMessage
            {
                Id = DeterministicGuid.Create(
                    OutboxNamespace,
                    $"{invoice.Id:N}:overdue-v1"),
                Type = "InvoiceBecameOverdue",
                Payload = JsonSerializer.Serialize(new
                {
                    invoice.BusinessId,
                    InvoiceId = invoice.Id,
                    DeliveryId = deliveryId,
                    invoice.DueAtUtc,
                    invoice.OutstandingAmount,
                    OccurredAtUtc = now
                }),
                OccurredAtUtc = now,
                NextAttemptAtUtc = now
            });
            await database.SaveChangesAsync();
            await transaction.CommitAsync();
            return new OverdueEvaluationResult(
                "DeliveryReady", deliveryId, invoice.DueAtUtc);
        });

        LogEvaluated(
            logger,
            input.BusinessId,
            input.InvoiceId,
            context.InstanceId,
            result.Outcome,
            result.DeliveryId);
        return result;
    }

    [LoggerMessage(
        EventId = 8201,
        Level = LogLevel.Information,
        Message = "Evaluated overdue state for business {BusinessId}, invoice {InvoiceId}, workflow {WorkflowId}: {Outcome}, delivery {DeliveryId}.")]
    private static partial void LogEvaluated(
        ILogger logger,
        Guid businessId,
        Guid invoiceId,
        string workflowId,
        string outcome,
        Guid? deliveryId);
}

public sealed record OverdueEvaluationResult(
    string Outcome,
    Guid? DeliveryId,
    DateTimeOffset? DueAtUtc);
