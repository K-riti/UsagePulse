using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Azure.Cosmos;
using UsagePulse.QueryApi.Configuration;
using UsagePulse.QueryApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenTelemetry().UseAzureMonitor();
builder.Services.AddOptions<UsagePulseReadOptions>()
    .Bind(builder.Configuration.GetSection("UsagePulse"))
    .ValidateDataAnnotations()
    .Validate(options =>
            options.AllowConnectionStringFallback
                ? !string.IsNullOrWhiteSpace(options.CosmosConnectionString) || !string.IsNullOrWhiteSpace(options.CosmosEndpoint)
                : !string.IsNullOrWhiteSpace(options.CosmosEndpoint),
        "Managed Identity is the default. Configure CosmosEndpoint, or explicitly enable AllowConnectionStringFallback with CosmosConnectionString.")
    .ValidateOnStart();
builder.Services.AddSingleton<CosmosClient>(sp => CosmosFactory.Create(sp));
builder.Services.AddSingleton<UsageSummaryService>();
builder.Services.AddSingleton<RealtimeDashboardService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/api/usage/{tenantId}/summary", async (
    string tenantId,
    DateTimeOffset? from,
    DateTimeOffset? to,
    UsageSummaryService summaryService,
    CancellationToken cancellationToken) =>
{
    var end = to ?? DateTimeOffset.UtcNow;
    var start = from ?? end.AddHours(-24);
    if (start > end)
    {
        return Results.BadRequest("'from' must be before 'to'.");
    }

    var summary = await summaryService.GetSummaryAsync(tenantId, start, end, cancellationToken);
    return Results.Ok(summary);
})
.WithName("GetTenantUsageSummary")
.WithOpenApi();

app.MapGet("/api/dashboard/{tenantId}/realtime", async (
    string tenantId,
    string? window,
    RealtimeDashboardService dashboardService,
    CancellationToken cancellationToken) =>
{
    var selectedWindow = string.IsNullOrWhiteSpace(window) ? "1h" : window;

    try
    {
        var dashboard = await dashboardService.GetRealtimeAsync(tenantId, selectedWindow, cancellationToken);
        return Results.Ok(dashboard);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(ex.Message);
    }
})
.WithName("GetTenantRealtimeDashboard")
.WithOpenApi();

app.Run();
