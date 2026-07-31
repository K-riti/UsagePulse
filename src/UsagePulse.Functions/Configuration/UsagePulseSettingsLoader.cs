using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace UsagePulse.Functions.Configuration;

public static class UsagePulseSettingsLoader
{
    public static UsagePulseSettings Load(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<IOptions<UsagePulseSettings>>().Value;
    }
}
