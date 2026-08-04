using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using System.Text.Json;
using Invoice.Application;
using Invoice.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Invoice.Api;

public sealed partial class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    ILogger<OutboxPublisher> logger)
    : BackgroundService
{
    private static readonly ActivitySource ActivitySource =
        new("Invoice.Api.Outbox");
    private static readonly Meter Meter = new("Invoice.Api.Outbox");
    private static readonly Counter<long> PublishOutcomes =
        Meter.CreateCounter<long>("invoice.outbox.publish.outcomes");
    private static readonly Histogram<double> PublishDuration =
        Meter.CreateHistogram<double>("invoice.outbox.publish.duration", "ms");
    private readonly Guid _leaseOwner = Guid.NewGuid();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PublishBatchAsync(stoppingToken);
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

    private async Task PublishBatchAsync(CancellationToken cancellationToken)
    {
        var messages = await ClaimBatchAsync(cancellationToken);

        var client = httpClientFactory.CreateClient("dapr");
        foreach (var message in messages)
        {
            var stopwatch = Stopwatch.StartNew();
            var lifecycle = message.Type is
                "InvoiceIssued" or "InvoicePaymentRecorded" or
                "InvoiceVoided" or "InvoiceDueDateChanged";
            var topic = lifecycle ? "invoice-lifecycle" : "walking-skeleton";
            using var activity = ActivitySource.StartActivity(
                "outbox.publish",
                ActivityKind.Producer);
            activity?.SetTag("messaging.system", "dapr");
            activity?.SetTag("messaging.destination.name", topic);
            activity?.SetTag("messaging.message.id", message.Id);
            try
            {
                using var content = JsonContent.Create(
                    JsonDocument.Parse(message.Payload).RootElement);
                var path = $"/v1.0/publish/pubsub/{topic}" +
                    $"?metadata.cloudevent.id={message.Id:D}" +
                    "&metadata.cloudevent.source=invoice-api" +
                    $"&metadata.cloudevent.type={Uri.EscapeDataString(message.Type)}";
                using var response = await client.PostAsync(
                    path,
                    content,
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                await CompleteAsync(message.Id, null, cancellationToken);
                PublishOutcomes.Add(
                    1,
                    new KeyValuePair<string, object?>("outcome", "published"));
                PublishDuration.Record(
                    stopwatch.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("outcome", "published"));
                LogPublished(
                    logger,
                    message.Id,
                    message.Type,
                    topic,
                    message.PublishAttempts);
            }
            catch (Exception exception) when (
                exception is HttpRequestException or TaskCanceledException)
            {
                await CompleteAsync(message.Id, exception, cancellationToken);
                PublishOutcomes.Add(
                    1,
                    new KeyValuePair<string, object?>("outcome", "pending"));
                PublishDuration.Record(
                    stopwatch.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("outcome", "pending"));
                LogPending(
                    logger,
                    message.Id,
                    message.PublishAttempts,
                    exception);
            }
        }
    }

    private async Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(
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
            var messages = await database.OutboxMessages
                .FromSqlInterpolated($"""
                    SELECT * FROM outbox_messages
                    WHERE "PublishedAtUtc" IS NULL
                      AND "NextAttemptAtUtc" <= {now}
                      AND ("LeaseUntilUtc" IS NULL OR "LeaseUntilUtc" < {now})
                      AND "Type" IN (
                        'WalkingSkeletonEvent', 'InvoiceIssued',
                        'InvoicePaymentRecorded', 'InvoiceVoided',
                        'InvoiceDueDateChanged')
                    ORDER BY "OccurredAtUtc", "Id"
                    FOR UPDATE SKIP LOCKED
                    LIMIT 20
                    """)
                .ToListAsync(cancellationToken);
            foreach (var message in messages)
            {
                message.LeaseOwner = _leaseOwner;
                message.LeaseUntilUtc = now.AddSeconds(30);
                message.PublishAttempts++;
                message.NextAttemptAtUtc = now.Add(
                    GetRetryDelay(message.PublishAttempts));
            }
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return messages;
        });
    }

    private async Task CompleteAsync(
        Guid messageId,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<InvoiceDbContext>();
        var message = await database.OutboxMessages.SingleAsync(
            item => item.Id == messageId && item.LeaseOwner == _leaseOwner,
            cancellationToken);
        message.LeaseOwner = null;
        message.LeaseUntilUtc = null;
        if (exception is null)
        {
            message.PublishedAtUtc = DateTimeOffset.UtcNow;
            message.LastError = null;
        }
        else
        {
            message.LastError = exception.Message.Length <= 1000
                ? exception.Message
                : exception.Message[..1000];
        }
        await database.SaveChangesAsync(cancellationToken);
    }

    private static TimeSpan GetRetryDelay(int attempt)
    {
        var exponent = Math.Min(attempt, 5);
        return TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, exponent)));
    }

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Published outbox message {MessageId} of type {MessageType} to {Topic} on attempt {Attempt}.")]
    private static partial void LogPublished(
        ILogger logger,
        Guid messageId,
        string messageType,
        string topic,
        int attempt);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Outbox message {MessageId} remains pending after attempt {Attempt}.")]
    private static partial void LogPending(
        ILogger logger,
        Guid messageId,
        int attempt,
        Exception exception);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "Outbox publishing cycle failed before a message could be completed; durable rows remain available for recovery.")]
    private static partial void LogCycleFailed(
        ILogger logger,
        Exception exception);
}
