using Microsoft.Azure.Cosmos;
using UsagePulse.Functions.Configuration;
using UsagePulse.Functions.StorageModels;
using UsagePulse.Processing.Abstractions;

namespace UsagePulse.Functions.Infrastructure;

public sealed class CosmosIdempotencyStore : IIdempotencyStore
{
    private readonly Container container;

    public CosmosIdempotencyStore(CosmosClient cosmosClient, UsagePulseSettings settings)
    {
        container = cosmosClient.GetContainer(settings.CosmosDatabase, settings.IdempotencyContainer);
    }

    public async Task<bool> TryStartProcessingAsync(string eventId, CancellationToken cancellationToken)
    {
        try
        {
            await container.ReadItemAsync<IdempotencyRecord>(eventId, new PartitionKey(eventId), cancellationToken: cancellationToken);
            return false;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var item = new IdempotencyRecord
            {
                Id = eventId,
                Status = "started",
                ProcessedAt = DateTimeOffset.UtcNow
            };

            await container.CreateItemAsync(item, new PartitionKey(item.Id), cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return false;
        }
    }

    public async Task MarkProcessedAsync(string eventId, CancellationToken cancellationToken)
    {
        var item = new IdempotencyRecord
        {
            Id = eventId,
            Status = "processed",
            ProcessedAt = DateTimeOffset.UtcNow
        };

        await container.UpsertItemAsync(item, new PartitionKey(item.Id), cancellationToken: cancellationToken);
    }
}
