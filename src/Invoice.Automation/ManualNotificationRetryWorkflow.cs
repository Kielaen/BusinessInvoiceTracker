using Dapr.Workflow;

namespace Invoice.Automation;

public sealed class ManualNotificationRetryWorkflow :
    Workflow<ManualNotificationRetryInput, NotificationDeliveryResult>
{
    public override Task<NotificationDeliveryResult> RunAsync(
        WorkflowContext context,
        ManualNotificationRetryInput input) =>
        context.CallActivityAsync<NotificationDeliveryResult>(
            nameof(DeliverOverdueNotificationActivity),
            input.DeliveryId,
            new WorkflowTaskOptions
            {
                RetryPolicy = new WorkflowRetryPolicy(
                    3,
                    TimeSpan.FromSeconds(2),
                    backoffCoefficient: 2,
                    maxRetryInterval: TimeSpan.FromSeconds(4),
                    retryTimeout: TimeSpan.FromSeconds(20))
            });

    public static string InstanceId(Guid deliveryId, Guid requestId) =>
        $"notification-{deliveryId:N}-manual-{requestId:N}";
}

public sealed record ManualNotificationRetryInput(
    Guid RequestId,
    Guid DeliveryId);

public sealed record ManualNotificationRetryRequest(Guid RequestId);
