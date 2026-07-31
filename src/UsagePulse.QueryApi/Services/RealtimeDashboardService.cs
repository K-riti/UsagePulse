using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using UsagePulse.Contracts;
using UsagePulse.QueryApi.Configuration;
using UsagePulse.QueryApi.StorageModels;

namespace UsagePulse.QueryApi.Services;

public sealed class RealtimeDashboardService
{
    private static readonly IReadOnlyDictionary<string, MaterializedWindowPolicy> Policies = new Dictionary<string, MaterializedWindowPolicy>(StringComparer.OrdinalIgnoreCase)
    {
        ["5m"] = new("5m", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1)),
        ["1h"] = new("1h", TimeSpan.FromHours(1), TimeSpan.FromMinutes(5)),
        ["24h"] = new("24h", TimeSpan.FromHours(24), TimeSpan.FromHours(1))
    };

    private readonly Container summaryViewsContainer;

    public RealtimeDashboardService(CosmosClient cosmosClient, IOptions<UsagePulseReadOptions> options)
    {
        var readOptions = options.Value;
        summaryViewsContainer = cosmosClient.GetContainer(readOptions.CosmosDatabase, readOptions.SummaryViewsContainer);
    }

    public async Task<TenantRealtimeDashboard> GetRealtimeAsync(
        string tenantId,
        string window,
        CancellationToken cancellationToken)
    {
        if (!Policies.TryGetValue(window, out var policy))
        {
            throw new ArgumentException("Supported windows are 5m, 1h, and 24h.", nameof(window));
        }

        var to = DateTimeOffset.UtcNow;
        var from = to - policy.WindowSize;
        var query = new QueryDefinition(
            "SELECT c.bucketStart, c.bucketEnd, c.eventCount, c.totalQuantity, c.featureBreakdown FROM c WHERE c.tenantId = @tenantId AND c.window = @window AND c.bucketStart >= @from AND c.bucketStart < @to ORDER BY c.bucketStart ASC")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@window", policy.Name)
            .WithParameter("@from", FloorToWindow(from, policy.BucketSize))
            .WithParameter("@to", FloorToWindow(to.Add(policy.BucketSize), policy.BucketSize));

        var points = new List<TenantUsagePoint>();
        var featureBreakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var eventCount = 0;
        long totalQuantity = 0;

        using var iterator = summaryViewsContainer.GetItemQueryIterator<UsageSummaryViewDocument>(query);
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync(cancellationToken))
            {
                points.Add(new TenantUsagePoint(item.BucketStart, item.BucketEnd, item.EventCount, item.TotalQuantity));
                eventCount += item.EventCount;
                totalQuantity += item.TotalQuantity;

                foreach (var feature in item.FeatureBreakdown)
                {
                    featureBreakdown[feature.Key] = featureBreakdown.GetValueOrDefault(feature.Key) + feature.Value;
                }
            }
        }

        return new TenantRealtimeDashboard(tenantId, from, to, eventCount, totalQuantity, featureBreakdown, points);
    }

    private static DateTimeOffset FloorToWindow(DateTimeOffset value, TimeSpan bucketSize)
    {
        var utcTicks = value.UtcDateTime.Ticks;
        var ticks = utcTicks - (utcTicks % bucketSize.Ticks);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private sealed record MaterializedWindowPolicy(string Name, TimeSpan WindowSize, TimeSpan BucketSize);
}
