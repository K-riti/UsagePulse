using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using UsagePulse.Contracts;
using UsagePulse.QueryApi.Configuration;
using UsagePulse.QueryApi.StorageModels;

namespace UsagePulse.QueryApi.Services;

public sealed class UsageSummaryService
{
    private static readonly IReadOnlyDictionary<TimeSpan, MaterializedWindowPolicy> MaterializedPolicies = new Dictionary<TimeSpan, MaterializedWindowPolicy>
    {
        [TimeSpan.FromMinutes(5)] = new("5m", TimeSpan.FromMinutes(1)),
        [TimeSpan.FromHours(1)] = new("1h", TimeSpan.FromMinutes(5)),
        [TimeSpan.FromHours(24)] = new("24h", TimeSpan.FromHours(1))
    };

    private readonly Container eventsContainer;
    private readonly Container summaryViewsContainer;

    public UsageSummaryService(CosmosClient cosmosClient, IOptions<UsagePulseReadOptions> options)
    {
        var readOptions = options.Value;
        eventsContainer = cosmosClient.GetContainer(readOptions.CosmosDatabase, readOptions.EventsContainer);
        summaryViewsContainer = cosmosClient.GetContainer(readOptions.CosmosDatabase, readOptions.SummaryViewsContainer);
    }

    public async Task<TenantUsageSummary> GetSummaryAsync(
        string tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (MaterializedPolicies.TryGetValue(to - from, out var policy))
        {
            return await GetMaterializedSummaryAsync(tenantId, from, to, policy, cancellationToken);
        }

        var query = new QueryDefinition(
            "SELECT c.feature, c.quantity FROM c WHERE c.tenantId = @tenantId AND c.occurredAt >= @from AND c.occurredAt <= @to")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@from", from)
            .WithParameter("@to", to);

        var featureBreakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var eventCount = 0;
        long totalQuantity = 0;

        using var iterator = eventsContainer.GetItemQueryIterator<UsageSummaryProjectionDocument>(query);
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync(cancellationToken))
            {
                eventCount++;
                totalQuantity += item.Quantity;
                featureBreakdown[item.Feature] = featureBreakdown.GetValueOrDefault(item.Feature) + item.Quantity;
            }
        }

        return new TenantUsageSummary(tenantId, from, to, eventCount, totalQuantity, featureBreakdown);
    }

    private async Task<TenantUsageSummary> GetMaterializedSummaryAsync(
        string tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        MaterializedWindowPolicy policy,
        CancellationToken cancellationToken)
    {
        var query = new QueryDefinition(
            "SELECT c.eventCount, c.totalQuantity, c.featureBreakdown FROM c WHERE c.tenantId = @tenantId AND c.window = @window AND c.bucketStart >= @from AND c.bucketStart < @to")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@window", policy.Name)
            .WithParameter("@from", FloorToWindow(from, policy.BucketSize))
            .WithParameter("@to", FloorToWindow(to.Add(policy.BucketSize), policy.BucketSize));

        var featureBreakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var eventCount = 0;
        long totalQuantity = 0;

        using var iterator = summaryViewsContainer.GetItemQueryIterator<UsageSummaryViewDocument>(query);
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync(cancellationToken))
            {
                eventCount += item.EventCount;
                totalQuantity += item.TotalQuantity;
                foreach (var feature in item.FeatureBreakdown)
                {
                    featureBreakdown[feature.Key] = featureBreakdown.GetValueOrDefault(feature.Key) + feature.Value;
                }
            }
        }

        return new TenantUsageSummary(tenantId, from, to, eventCount, totalQuantity, featureBreakdown);
    }

    private static DateTimeOffset FloorToWindow(DateTimeOffset value, TimeSpan bucketSize)
    {
        var utcTicks = value.UtcDateTime.Ticks;
        var ticks = utcTicks - (utcTicks % bucketSize.Ticks);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private sealed record MaterializedWindowPolicy(string Name, TimeSpan BucketSize);
}
