using Microsoft.Azure.Cosmos;
using UsagePulse.Contracts;
using UsagePulse.Functions.Configuration;
using UsagePulse.Functions.StorageModels;
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
        var document = new UsageEventDocument
        {
            Id = usageEvent.EventId,
            TenantId = usageEvent.TenantId,
            UserId = usageEvent.UserId,
            Feature = usageEvent.Feature,
            Quantity = usageEvent.Quantity,
            OccurredAt = usageEvent.OccurredAt,
            Dimensions = usageEvent.Dimensions,
            IngestedAt = DateTimeOffset.UtcNow
        };

        await container.UpsertItemAsync(document, new PartitionKey(document.TenantId), cancellationToken: cancellationToken);
    }
}
