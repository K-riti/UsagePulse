using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Azure.Cosmos;
using UsagePulse.QueryApi.Configuration;
using UsagePulse.QueryApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenTelemetry().UseAzureMonitor();
builder.Services.Configure<UsagePulseReadOptions>(builder.Configuration.GetSection("UsagePulse"));
builder.Services.AddSingleton<CosmosClient>(sp => CosmosFactory.Create(sp));
builder.Services.AddSingleton<UsageSummaryService>();

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

app.Run();
