using Microsoft.Extensions.DependencyInjection;
using UsagePulse.Processing.Abstractions;
using UsagePulse.Processing.Services;
using UsagePulse.Processing.Services.Pipeline;

namespace UsagePulse.Processing;

public static class DependencyInjection
{
    public static IServiceCollection AddUsagePulseProcessing(this IServiceCollection services)
    {
        services.AddSingleton<IUsageEventValidationStage, UsageEventValidationStage>();
        services.AddSingleton<IUsageEventDeduplicationStage, UsageEventDeduplicationStage>();
        services.AddSingleton<IUsageEventPersistenceStage, UsageEventPersistenceStage>();
        services.AddSingleton<IUsageEventAnalyticsStage, UsageEventAnalyticsStage>();
        services.AddSingleton<IUsageEventFinalizeStage, UsageEventFinalizeStage>();
        services.AddSingleton<IUsageEventProcessor, UsageEventProcessor>();
        return services;
    }
}
