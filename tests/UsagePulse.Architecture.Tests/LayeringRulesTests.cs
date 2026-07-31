using UsagePulse.Contracts;
using UsagePulse.Processing.Abstractions;

namespace UsagePulse.Architecture.Tests;

public class LayeringRulesTests
{
    [Fact]
    public void ContractsAssembly_ShouldNotReferenceUpperLayers()
    {
        var references = typeof(UsageEvent).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();

        Assert.DoesNotContain("UsagePulse.Processing", references);
        Assert.DoesNotContain("UsagePulse.Functions", references);
        Assert.DoesNotContain("UsagePulse.QueryApi", references);
    }

    [Fact]
    public void ProcessingAssembly_ShouldNotReferenceAdapters()
    {
        var references = typeof(IUsageEventProcessor).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();

        Assert.DoesNotContain("UsagePulse.Functions", references);
        Assert.DoesNotContain("UsagePulse.QueryApi", references);
    }
}
