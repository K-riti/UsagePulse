using Microsoft.Azure.Cosmos;
using UsagePulse.Contracts;
using UsagePulse.Functions.Configuration;
using UsagePulse.Processing.Abstractions;

namespace UsagePulse.Functions.Infrastructure;

public sealed class CosmosUsageEventRepository : IUsageEventRepository
{
    private readonly Container container;

    public CosmosUsageEventRepository(CosmosClient cosmosClient, UsagePulseSettings settings)
    {
        container = cosmosClient.GetContainer(settings.CosmosDatabase, settings.EventsContainer);
    }

    public async Task StoreAsync(UsageEvent usageEvent, CancellationToken cancellationToken)
    {
        var document = new UsageEventDocument(
            usageEvent.EventId,
            usageEvent.TenantId,
            usageEvent.UserId,
            usageEvent.Feature,
            usageEvent.Quantity,
            usageEvent.OccurredAt,
            usageEvent.Dimensions,
            DateTimeOffset.UtcNow);

        await container.UpsertItemAsync(document, new PartitionKey(document.tenantId), cancellationToken: cancellationToken);
    }

    private sealed record UsageEventDocument(
        string id,
        string tenantId,
        string userId,
        string feature,
        int quantity,
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<string, string>? dimensions,
        DateTimeOffset ingestedAt);
}
