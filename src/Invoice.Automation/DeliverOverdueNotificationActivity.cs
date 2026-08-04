using System.Net;
using System.Net.Mail;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dapr.Workflow;
using Invoice.Domain.Invoices;
using Invoice.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Invoice.Automation;

public sealed partial class DeliverOverdueNotificationActivity(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<DeliverOverdueNotificationActivity> logger) :
    WorkflowActivity<Guid, NotificationDeliveryResult>
{
    private static readonly Guid AttemptNamespace =
        Guid.Parse("51b03f30-f4ae-5c7e-9dbd-377986ae4918");
    private static readonly ActivitySource ActivitySource =
        new("Invoice.Automation.Notifications");
    private static readonly Meter Meter =
        new("Invoice.Automation.Notifications");
    private static readonly Counter<long> DeliveryOutcomes =
        Meter.CreateCounter<long>("invoice.notification.delivery.outcomes");

    public override async Task<NotificationDeliveryResult> RunAsync(
        WorkflowActivityContext context,
        Guid deliveryId)
    {
        using var activity = ActivitySource.StartActivity(
            "notification.deliver",
            ActivityKind.Internal);
        activity?.SetTag("workflow.id", context.InstanceId);
        activity?.SetTag("notification.delivery.id", deliveryId);
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<InvoiceDbContext>();
        var delivery = await database.NotificationDeliveries.SingleAsync(
            item => item.Id == deliveryId);
        if (delivery.Status == NotificationDeliveryStatuses.Delivered)
        {
            return Result(delivery, "AlreadyDelivered");
        }
        if (delivery.Status == NotificationDeliveryStatuses.Failed)
        {
            return Result(delivery, "PermanentlyFailed");
        }
        if (delivery.Status == NotificationDeliveryStatuses.Cancelled)
        {
            return Result(delivery, "CancelledTerminalState");
        }

        if (await MailpitContainsAsync(delivery.ProviderMessageId))
        {
            var reconciled = NewAttempt(delivery, NotificationAttemptOutcomes.Reconciled);
            reconciled.CompletedAtUtc = DateTimeOffset.UtcNow;
            delivery.Status = NotificationDeliveryStatuses.Delivered;
            delivery.DeliveredAtUtc = reconciled.CompletedAtUtc;
            delivery.LastError = null;
            database.NotificationAttempts.Add(reconciled);
            await database.SaveChangesAsync();
            LogDelivery(logger, deliveryId, context.InstanceId, "Reconciled", delivery.Attempts);
            return Result(delivery, "Reconciled");
        }

        var invoiceStatus = await database.Invoices.AsNoTracking()
            .Where(invoice => invoice.BusinessId == delivery.BusinessId &&
                invoice.Id == delivery.InvoiceId)
            .Select(invoice => invoice.Status)
            .SingleAsync();
        if (invoiceStatus is InvoiceStatus.Paid or InvoiceStatus.Voided)
        {
            delivery.Status = NotificationDeliveryStatuses.Cancelled;
            delivery.LastError =
                $"Invoice entered terminal state {invoiceStatus} before delivery.";
            await database.SaveChangesAsync();
            LogDelivery(
                logger,
                deliveryId,
                context.InstanceId,
                "CancelledTerminalState",
                delivery.Attempts);
            return Result(delivery, "CancelledTerminalState");
        }

        NotificationAttempt attempt;
        try
        {
            _ = new MailAddress(delivery.Recipient);
            attempt = NewAttempt(delivery, NotificationAttemptOutcomes.Started);
            database.NotificationAttempts.Add(attempt);
            await database.SaveChangesAsync();
        }
        catch (FormatException exception)
        {
            attempt = NewAttempt(delivery, NotificationAttemptOutcomes.PermanentFailure);
            attempt.Error = Limit(exception.Message);
            attempt.CompletedAtUtc = DateTimeOffset.UtcNow;
            database.NotificationAttempts.Add(attempt);
            MarkFailed(delivery, attempt.Error, attempt.CompletedAtUtc.Value);
            await database.SaveChangesAsync();
            LogDelivery(logger, deliveryId, context.InstanceId, "PermanentFailure", delivery.Attempts);
            return Result(delivery, "PermanentlyFailed");
        }

        try
        {
            using var message = new MailMessage(
                new MailAddress("no-reply@business-invoice-tracker.local", "Business Invoice Tracker"),
                new MailAddress(delivery.Recipient))
            {
                Subject = "Invoice payment overdue",
                Body = $"Invoice {delivery.InvoiceId} is overdue. Delivery reference: {delivery.Id}.",
                IsBodyHtml = false
            };
            message.Headers.Add("Message-ID", delivery.ProviderMessageId);
            using var smtp = new SmtpClient(
                configuration["Mailpit:SmtpHost"] ?? "mailpit",
                configuration.GetValue("Mailpit:SmtpPort", 1025))
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = false,
                UseDefaultCredentials = false,
                Credentials = CredentialCache.DefaultNetworkCredentials
            };
            using var sendTimeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(5));
            await smtp.SendMailAsync(message, sendTimeout.Token);

            var completedAt = DateTimeOffset.UtcNow;
            attempt.Outcome = NotificationAttemptOutcomes.Delivered;
            attempt.CompletedAtUtc = completedAt;
            delivery.Status = NotificationDeliveryStatuses.Delivered;
            delivery.DeliveredAtUtc = completedAt;
            delivery.LastError = null;
            await database.SaveChangesAsync();
            LogDelivery(logger, deliveryId, context.InstanceId, "Delivered", delivery.Attempts);
            return Result(delivery, "Delivered");
        }
        catch (SmtpFailedRecipientException exception) when (
            (int)exception.StatusCode is >= 400 and < 500)
        {
            var shouldRetry = await RecordTransientFailureAsync(
                database,
                delivery,
                attempt,
                exception,
                context.InstanceId);
            if (!shouldRetry)
            {
                return Result(delivery, "RetryExhausted");
            }
            throw new InvalidOperationException(
                "The simulated notification provider is temporarily unavailable.",
                exception);
        }
        catch (SmtpFailedRecipientException exception)
        {
            await RecordPermanentFailureAsync(database, delivery, attempt, exception);
            LogDelivery(logger, deliveryId, context.InstanceId, "PermanentFailure", delivery.Attempts);
            return Result(delivery, "PermanentlyFailed");
        }
        catch (Exception exception) when (
            exception is SmtpException or IOException or TimeoutException or
            OperationCanceledException)
        {
            var shouldRetry = await RecordTransientFailureAsync(
                database,
                delivery,
                attempt,
                exception,
                context.InstanceId);
            if (!shouldRetry)
            {
                return Result(delivery, "RetryExhausted");
            }

            throw new InvalidOperationException(
                "The simulated notification provider is temporarily unavailable.",
                exception);
        }
    }

    private async Task<bool> MailpitContainsAsync(string messageId)
    {
        try
        {
            var client = httpClientFactory.CreateClient("mailpit");
            var normalizedMessageId = messageId.Trim('<', '>');
            var query = Uri.EscapeDataString($"message-id:{normalizedMessageId}");
            using var response = await client.GetAsync($"api/v1/search?query={query}");
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }
            var body = await response.Content.ReadAsStringAsync();
            return body.Contains(
                normalizedMessageId,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private static NotificationAttempt NewAttempt(
        NotificationDelivery delivery,
        string outcome)
    {
        delivery.Attempts++;
        delivery.LastAttemptAtUtc = DateTimeOffset.UtcNow;
        return new NotificationAttempt
        {
            Id = DeterministicGuid.Create(
                AttemptNamespace,
                $"{delivery.Id:N}:{delivery.Attempts}"),
            DeliveryId = delivery.Id,
            AttemptNumber = delivery.Attempts,
            StartedAtUtc = delivery.LastAttemptAtUtc.Value,
            Outcome = outcome
        };
    }

    private static async Task RecordPermanentFailureAsync(
        InvoiceDbContext database,
        NotificationDelivery delivery,
        NotificationAttempt attempt,
        Exception exception)
    {
        var completedAt = DateTimeOffset.UtcNow;
        attempt.Outcome = NotificationAttemptOutcomes.PermanentFailure;
        attempt.CompletedAtUtc = completedAt;
        attempt.Error = Limit(exception.Message);
        MarkFailed(delivery, attempt.Error, completedAt);
        await database.SaveChangesAsync();
    }

    private async Task<bool> RecordTransientFailureAsync(
        InvoiceDbContext database,
        NotificationDelivery delivery,
        NotificationAttempt attempt,
        Exception exception,
        string workflowId)
    {
        var completedAt = DateTimeOffset.UtcNow;
        attempt.Outcome = NotificationAttemptOutcomes.TransientFailure;
        attempt.CompletedAtUtc = completedAt;
        attempt.Error = Limit(exception.Message);
        delivery.LastError = attempt.Error;
        if (delivery.Attempts - delivery.AttemptCycleStart >= 3)
        {
            MarkFailed(delivery, attempt.Error, completedAt);
            await database.SaveChangesAsync();
            LogDelivery(
                logger,
                delivery.Id,
                workflowId,
                "RetryExhausted",
                delivery.Attempts);
            return false;
        }

        await database.SaveChangesAsync();
        LogTransient(
            logger,
            delivery.Id,
            workflowId,
            delivery.Attempts,
            exception);
        return true;
    }

    private static void MarkFailed(
        NotificationDelivery delivery,
        string? error,
        DateTimeOffset failedAtUtc)
    {
        delivery.Status = NotificationDeliveryStatuses.Failed;
        delivery.FailedAtUtc = failedAtUtc;
        delivery.LastError = error;
    }

    private static string Limit(string value) =>
        value.Length <= 1000 ? value : value[..1000];

    private static NotificationDeliveryResult Result(
        NotificationDelivery delivery,
        string outcome)
    {
        DeliveryOutcomes.Add(
            1,
            new KeyValuePair<string, object?>("outcome", outcome));
        Activity.Current?.SetTag("notification.outcome", outcome);
        Activity.Current?.SetTag("notification.attempts", delivery.Attempts);
        return new(
            delivery.Id,
            outcome,
            delivery.Attempts,
            delivery.DeliveredAtUtc,
            delivery.LastError);
    }

    [LoggerMessage(
        EventId = 8202,
        Level = LogLevel.Information,
        Message = "Notification delivery {DeliveryId} for workflow {WorkflowId} completed as {Outcome} after {Attempts} attempts.")]
    private static partial void LogDelivery(
        ILogger logger,
        Guid deliveryId,
        string workflowId,
        string outcome,
        int attempts);

    [LoggerMessage(
        EventId = 8203,
        Level = LogLevel.Warning,
        Message = "Notification delivery {DeliveryId} for workflow {WorkflowId} has transient failure on attempt {Attempt}.")]
    private static partial void LogTransient(
        ILogger logger,
        Guid deliveryId,
        string workflowId,
        int attempt,
        Exception exception);
}

public sealed record NotificationDeliveryResult(
    Guid DeliveryId,
    string Outcome,
    int Attempts,
    DateTimeOffset? DeliveredAtUtc,
    string? Error);
