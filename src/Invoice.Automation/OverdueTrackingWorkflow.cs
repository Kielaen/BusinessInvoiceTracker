using Dapr.Workflow;

namespace Invoice.Automation;

public sealed class OverdueTrackingWorkflow :
    Workflow<OverdueWorkflowInput, OverdueWorkflowResult>
{
    public override async Task<OverdueWorkflowResult> RunAsync(
        WorkflowContext context,
        OverdueWorkflowInput input)
    {
        var dueAtUtc = input.DueAtUtc;
        while (true)
        {
            using var timerCancellation = new CancellationTokenSource();
            var timer = context.CreateTimer(
                dueAtUtc.UtcDateTime,
                timerCancellation.Token);
            var stateChange = context.WaitForExternalEventAsync<WorkflowStateChange>(
                StateChangedEventName,
                CancellationToken.None);

            var winner = await Task.WhenAny(timer, stateChange);
            if (winner == timer)
            {
                var evaluation = await context.CallActivityAsync<OverdueEvaluationResult>(
                    nameof(EvaluateOverdueActivity),
                    input);
                if (evaluation.Outcome == "Reschedule" &&
                    evaluation.DueAtUtc is not null)
                {
                    dueAtUtc = evaluation.DueAtUtc.Value;
                    continue;
                }
                if (evaluation.DeliveryId is null)
                {
                    return new OverdueWorkflowResult(
                        evaluation.Outcome.ToLowerInvariant(),
                        context.CurrentUtcDateTime,
                        evaluation.DueAtUtc ?? dueAtUtc,
                        null,
                        null);
                }

                var delivery = await context.CallActivityAsync<NotificationDeliveryResult>(
                    nameof(DeliverOverdueNotificationActivity),
                    evaluation.DeliveryId.Value,
                    new WorkflowTaskOptions
                    {
                        RetryPolicy = new WorkflowRetryPolicy(
                            3,
                            TimeSpan.FromSeconds(2),
                            backoffCoefficient: 2,
                            maxRetryInterval: TimeSpan.FromSeconds(4),
                            retryTimeout: TimeSpan.FromSeconds(20))
                    });
                return new OverdueWorkflowResult(
                    delivery.Outcome.ToLowerInvariant(),
                    context.CurrentUtcDateTime,
                    evaluation.DueAtUtc ?? dueAtUtc,
                    delivery.DeliveryId,
                    delivery.Attempts);
            }

            timerCancellation.Cancel();
            var change = await stateChange;
            if (change.Status is "Paid" or "Voided")
            {
                return new OverdueWorkflowResult(
                    change.Status.ToLowerInvariant(),
                    context.CurrentUtcDateTime,
                    change.DueAtUtc ?? dueAtUtc,
                    null,
                    null);
            }

            if (change.DueAtUtc is not null)
            {
                dueAtUtc = change.DueAtUtc.Value;
            }
        }
    }

    public const string StateChangedEventName = "state-changed";

    public static string InstanceId(Guid invoiceId) =>
        $"invoice-{invoiceId:N}-overdue-v1";
}

public sealed record OverdueWorkflowInput(
    Guid BusinessId,
    Guid InvoiceId,
    DateTimeOffset DueAtUtc);

public sealed record WorkflowStateChange(
    Guid EventId,
    string Status,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset OccurredAtUtc);

public sealed record OverdueWorkflowResult(
    string CompletionKind,
    DateTime CompletedAtUtc,
    DateTimeOffset DueAtUtc,
    Guid? DeliveryId,
    int? DeliveryAttempts);
