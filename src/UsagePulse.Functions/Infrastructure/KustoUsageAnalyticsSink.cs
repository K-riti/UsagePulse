using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UsagePulse.Contracts;
using UsagePulse.Functions.Configuration;
using UsagePulse.Processing.Abstractions;

namespace UsagePulse.Functions.Infrastructure;

public sealed class KustoUsageAnalyticsSink : IUsageAnalyticsSink
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory httpClientFactory;
    private readonly UsagePulseSettings settings;
    private readonly ILogger<KustoUsageAnalyticsSink> logger;

    public KustoUsageAnalyticsSink(
        IHttpClientFactory httpClientFactory,
        UsagePulseSettings settings,
        ILogger<KustoUsageAnalyticsSink> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.settings = settings;
        this.logger = logger;
    }

    public async Task WriteAsync(UsageEvent usageEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.KustoIngestionEndpoint))
        {
            logger.LogInformation("Kusto endpoint not configured, skipped analytics write for {EventId}.", usageEvent.EventId);
            return;
        }

        var client = httpClientFactory.CreateClient(nameof(KustoUsageAnalyticsSink));
        using var response = await client.PostAsJsonAsync(settings.KustoIngestionEndpoint, usageEvent, SerializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
