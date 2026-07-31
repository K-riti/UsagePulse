using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace UsagePulse.QueryApi.Configuration;

public static class CosmosFactory
{
    public static CosmosClient Create(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<UsagePulseReadOptions>>().Value;
        return options.CosmosConnectionString is { Length: > 0 }
            ? new CosmosClient(options.CosmosConnectionString)
            : new CosmosClient(options.CosmosEndpoint, new DefaultAzureCredential());
    }
}
