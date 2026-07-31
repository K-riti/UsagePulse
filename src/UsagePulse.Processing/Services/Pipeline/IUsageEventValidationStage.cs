using UsagePulse.Contracts;

namespace UsagePulse.Processing.Services.Pipeline;

public interface IUsageEventValidationStage
{
    ValidationOutcome Validate(UsageEvent usageEvent);
}
