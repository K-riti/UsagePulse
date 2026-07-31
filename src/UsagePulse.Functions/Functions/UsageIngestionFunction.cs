using Microsoft.Azure.Functions.Worker;
using UsagePulse.Functions.Orchestration;

namespace UsagePulse.Functions.Functions;

public sealed class UsageIngestionFunction
{
    private readonly UsageIngestionOrchestrator orchestrator;

    public UsageIngestionFunction(UsageIngestionOrchestrator orchestrator)
    {
        this.orchestrator = orchestrator;
    }

    [Function(nameof(UsageIngestionFunction))]
    public Task Run(
        [EventHubTrigger("%UsagePulse:EventHubName%", Connection = "UsagePulseEventHubConnection")] string[] events,
        CancellationToken cancellationToken)
    {
        return orchestrator.HandleBatchAsync(events, cancellationToken);
    }
}
