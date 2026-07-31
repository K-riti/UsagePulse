using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Azure;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Ingest;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using UsagePulse.Contracts;
using UsagePulse.Functions.Configuration;
using UsagePulse.Functions.StorageModels;
using UsagePulse.Processing.Abstractions;
using UsagePulse.Serialization;

namespace UsagePulse.Functions.Infrastructure;

public sealed class KustoUsageAnalyticsSink : IUsageAnalyticsSink, IAsyncDisposable
{
    private static readonly IReadOnlyList<SummaryWindowPolicy> SummaryWindows =
    [
        new("5m", TimeSpan.FromMinutes(1)),
        new("1h", TimeSpan.FromMinutes(5)),
        new("24h", TimeSpan.FromHours(1))
    ];

    private readonly UsagePulseSettings settings;
    private readonly ILogger<KustoUsageAnalyticsSink> logger;
    private readonly Container? summaryViewsContainer;
    private readonly IKustoQueuedIngestClient? ingestClient;
    private readonly Channel<UsageEvent> batchChannel = Channel.CreateUnbounded<UsageEvent>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });
    private readonly CancellationTokenSource shutdown = new();
    private readonly Task flushLoop;

    public KustoUsageAnalyticsSink(
        UsagePulseSettings settings,
        CosmosClient cosmosClient,
        ILogger<KustoUsageAnalyticsSink> logger)
    {
        this.settings = settings;
        this.logger = logger;

        if (!string.IsNullOrWhiteSpace(settings.SummaryViewsContainer))
        {
            summaryViewsContainer = cosmosClient.GetContainer(settings.CosmosDatabase, settings.SummaryViewsContainer);
        }

        if (!string.IsNullOrWhiteSpace(settings.KustoClusterUri))
        {
            var kustoConnection = new KustoConnectionStringBuilder(settings.KustoClusterUri);
            kustoConnection = string.IsNullOrWhiteSpace(settings.KustoManagedIdentityClientId)
                ? kustoConnection.WithAadSystemManagedIdentity()
                : kustoConnection.WithAadUserManagedIdentity(settings.KustoManagedIdentityClientId);
            ingestClient = KustoIngestFactory.CreateQueuedIngestClient(kustoConnection);
        }

        flushLoop = Task.Run(() => ProcessBatchesAsync(shutdown.Token));
    }

    public async Task WriteAsync(UsageEvent usageEvent, CancellationToken cancellationToken)
    {
        await UpdateMaterializedViewsAsync(usageEvent, cancellationToken);
        await batchChannel.Writer.WriteAsync(usageEvent, cancellationToken);
    }

    private async Task ProcessBatchesAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.KustoFlushIntervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                while (TryReadBatch(out var batch))
                {
                    await FlushBatchAsync(batch, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        while (TryReadBatch(out var batch))
        {
            await FlushBatchAsync(batch, CancellationToken.None);
        }
    }

    private bool TryReadBatch(out List<UsageEvent> batch)
    {
        batch = [];
        while (batch.Count < settings.KustoBatchSize && batchChannel.Reader.TryRead(out var usageEvent))
        {
            batch.Add(usageEvent);
        }

        return batch.Count > 0;
    }

    private async Task FlushBatchAsync(IReadOnlyCollection<UsageEvent> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        if (ingestClient is null)
        {
            logger.LogInformation("Kusto cluster URI not configured, skipped batched analytics write for {Count} events.", batch.Count);
            return;
        }

        await using var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            foreach (var usageEvent in batch)
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(usageEvent, UsagePulseJsonDefaults.Options));
            }
        }

        stream.Position = 0;
        var ingestionProperties = new KustoQueuedIngestionProperties(settings.KustoDatabase, settings.KustoTable)
        {
            Format = DataSourceFormat.multijson
        };

        cancellationToken.ThrowIfCancellationRequested();
        await ingestClient.IngestFromStreamAsync(stream, ingestionProperties);
        logger.LogInformation("Queued {Count} usage events for Kusto ingestion.", batch.Count);
    }

    private async Task UpdateMaterializedViewsAsync(UsageEvent usageEvent, CancellationToken cancellationToken)
    {
        if (summaryViewsContainer is null)
        {
            return;
        }

        foreach (var window in SummaryWindows)
        {
            var bucketStart = FloorToWindow(usageEvent.OccurredAt, window.BucketSize);
            var bucketEnd = bucketStart.Add(window.BucketSize);
            var id = $"{usageEvent.TenantId}:{window.Name}:{bucketStart:O}";

            UsageSummaryViewDocument document;
            try
            {
                var response = await summaryViewsContainer.ReadItemAsync<UsageSummaryViewDocument>(id, new PartitionKey(usageEvent.TenantId), cancellationToken: cancellationToken);
                document = response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                document = new UsageSummaryViewDocument
                {
                    Id = id,
                    TenantId = usageEvent.TenantId,
                    Window = window.Name,
                    BucketStart = bucketStart,
                    BucketEnd = bucketEnd,
                    EventCount = 0,
                    TotalQuantity = 0,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
            }

            var featureBreakdown = new Dictionary<string, int>(document.FeatureBreakdown, StringComparer.OrdinalIgnoreCase)
            {
                [usageEvent.Feature] = document.FeatureBreakdown.GetValueOrDefault(usageEvent.Feature) + usageEvent.Quantity
            };

            var updated = document with
            {
                EventCount = document.EventCount + 1,
                TotalQuantity = document.TotalQuantity + usageEvent.Quantity,
                FeatureBreakdown = featureBreakdown,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await summaryViewsContainer.UpsertItemAsync(updated, new PartitionKey(updated.TenantId), cancellationToken: cancellationToken);
        }
    }

    private static DateTimeOffset FloorToWindow(DateTimeOffset value, TimeSpan bucketSize)
    {
        var utcTicks = value.UtcDateTime.Ticks;
        var ticks = utcTicks - (utcTicks % bucketSize.Ticks);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    public async ValueTask DisposeAsync()
    {
        batchChannel.Writer.TryComplete();
        shutdown.Cancel();
        await flushLoop;
        shutdown.Dispose();
        ingestClient?.Dispose();
    }

    private sealed record SummaryWindowPolicy(string Name, TimeSpan BucketSize);
}
