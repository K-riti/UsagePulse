using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UsagePulse.Functions.Configuration;
using UsagePulse.Functions.Infrastructure;
using UsagePulse.Processing.Abstractions;
using UsagePulse.Processing.Options;
using UsagePulse.Processing.Services;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.Configure<UsagePulseSettings>(builder.Configuration.GetSection("UsagePulse"));
builder.Services.Configure<UsagePulsePipelineOptions>(builder.Configuration.GetSection("UsagePulse"));
builder.Services.AddSingleton(sp => UsagePulseSettingsLoader.Load(sp));
builder.Services.AddSingleton<IUsageEventProcessor, UsageEventProcessor>();
builder.Services.AddHttpClient();

builder.Services.AddSingleton<ServiceBusClient>(sp =>
{
    var settings = UsagePulseSettingsLoader.Load(sp);
    return settings.ServiceBusConnectionString is { Length: > 0 }
        ? new ServiceBusClient(settings.ServiceBusConnectionString)
        : new ServiceBusClient(settings.ServiceBusNamespace, new DefaultAzureCredential());
});

builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var settings = UsagePulseSettingsLoader.Load(sp);
    return settings.CosmosConnectionString is { Length: > 0 }
        ? new CosmosClient(settings.CosmosConnectionString)
        : new CosmosClient(settings.CosmosEndpoint, new DefaultAzureCredential());
});

builder.Services.AddSingleton<IIdempotencyStore, CosmosIdempotencyStore>();
builder.Services.AddSingleton<IUsageEventRepository, CosmosUsageEventRepository>();
builder.Services.AddSingleton<IUsageAnalyticsSink, KustoUsageAnalyticsSink>();
builder.Services.AddSingleton<IDeadLetterSink, ServiceBusDeadLetterSink>();

if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry().UseFunctionsWorkerDefaults().UseAzureMonitorExporter();
}

builder.Build().Run();
