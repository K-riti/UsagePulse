using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using UsagePulse.Contracts;
using UsagePulse.QueryApi.Configuration;

namespace UsagePulse.QueryApi.Services;

public sealed class UsageSummaryService
{
    private readonly Container container;

    public UsageSummaryService(CosmosClient cosmosClient, IOptions<UsagePulseReadOptions> options)
    {
        var readOptions = options.Value;
        container = cosmosClient.GetContainer(readOptions.CosmosDatabase, readOptions.EventsContainer);
    }

    public async Task<TenantUsageSummary> GetSummaryAsync(
        string tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var query = new QueryDefinition(
            "SELECT c.feature, c.quantity FROM c WHERE c.tenantId = @tenantId AND c.occurredAt >= @from AND c.occurredAt <= @to")
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@from", from)
            .WithParameter("@to", to);

        var featureBreakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var eventCount = 0;
        long totalQuantity = 0;

        using var iterator = container.GetItemQueryIterator<UsageProjection>(query);
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync(cancellationToken))
            {
                eventCount++;
                totalQuantity += item.quantity;
                featureBreakdown[item.feature] = featureBreakdown.GetValueOrDefault(item.feature) + item.quantity;
            }
        }

        return new TenantUsageSummary(tenantId, from, to, eventCount, totalQuantity, featureBreakdown);
    }

    private sealed record UsageProjection(string feature, int quantity);
}
