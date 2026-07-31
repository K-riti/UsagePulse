using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using UsagePulse.Functions.Configuration;
using UsagePulse.Functions.Infrastructure;
using UsagePulse.Processing;
using UsagePulse.Processing.Abstractions;
using UsagePulse.Processing.Options;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddOptions<UsagePulseSettings>()
    .Bind(builder.Configuration.GetSection("UsagePulse"))
    .ValidateDataAnnotations()
    .Validate(settings =>
            settings.AllowConnectionStringFallback
                ? !string.IsNullOrWhiteSpace(settings.ServiceBusConnectionString) || !string.IsNullOrWhiteSpace(settings.ServiceBusNamespace)
                : !string.IsNullOrWhiteSpace(settings.ServiceBusNamespace),
        "Managed Identity is the default. Configure ServiceBusNamespace, or explicitly enable AllowConnectionStringFallback with ServiceBusConnectionString.")
    .Validate(settings =>
            settings.AllowConnectionStringFallback
                ? !string.IsNullOrWhiteSpace(settings.CosmosConnectionString) || !string.IsNullOrWhiteSpace(settings.CosmosEndpoint)
                : !string.IsNullOrWhiteSpace(settings.CosmosEndpoint),
        "Managed Identity is the default. Configure CosmosEndpoint, or explicitly enable AllowConnectionStringFallback with CosmosConnectionString.")
    .Validate(settings => settings.MinimumCompatibleSchemaVersion <= settings.CurrentSchemaVersion,
        "MinimumCompatibleSchemaVersion must be less than or equal to CurrentSchemaVersion.")
    .ValidateOnStart();

builder.Services.AddOptions<UsagePulsePipelineOptions>()
    .Bind(builder.Configuration.GetSection("UsagePulse"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<UsagePulseSettings>>().Value);
builder.Services.AddSingleton<UsageIngressPolicyEvaluator>();
builder.Services.AddUsagePulseProcessing();
builder.Services.AddHttpClient();

builder.Services.AddSingleton<ServiceBusClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<UsagePulseSettings>>().Value;
    return settings.AllowConnectionStringFallback && !string.IsNullOrWhiteSpace(settings.ServiceBusConnectionString)
        ? new ServiceBusClient(settings.ServiceBusConnectionString)
        : new ServiceBusClient(settings.ServiceBusNamespace, new DefaultAzureCredential());
});

builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<UsagePulseSettings>>().Value;
    return settings.AllowConnectionStringFallback && !string.IsNullOrWhiteSpace(settings.CosmosConnectionString)
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
