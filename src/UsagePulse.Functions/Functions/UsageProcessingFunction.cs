using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using UsagePulse.Functions.Orchestration;

namespace UsagePulse.Functions.Functions;

public sealed class UsageProcessingFunction
{
    private readonly UsageProcessingOrchestrator orchestrator;

    public UsageProcessingFunction(UsageProcessingOrchestrator orchestrator)
    {
        this.orchestrator = orchestrator;
    }

    [Function(nameof(UsageProcessingFunction))]
    public Task Run(
        [ServiceBusTrigger("%UsagePulse:ServiceBusQueue%", Connection = "UsagePulseServiceBusConnection")] ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken)
    {
        return orchestrator.HandleAsync(message, messageActions, cancellationToken);
    }
}
