using UsagePulse.Contracts;

namespace UsagePulse.Processing.Services.Pipeline;

public sealed class UsageEventValidationStage : IUsageEventValidationStage
{
    public ValidationOutcome Validate(UsageEvent usageEvent)
    {
        if (string.IsNullOrWhiteSpace(usageEvent.EventId.Value))
        {
            return ValidationOutcome.Invalid(ValidationErrorCode.MissingEventId, "EventId is required.");
        }

        if (string.IsNullOrWhiteSpace(usageEvent.TenantId.Value))
        {
            return ValidationOutcome.Invalid(ValidationErrorCode.MissingTenantId, "TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(usageEvent.Feature.Value))
        {
            return ValidationOutcome.Invalid(ValidationErrorCode.MissingFeature, "Feature is required.");
        }

        var contractFailure = UsageEventContractValidator.Validate(usageEvent);
        if (contractFailure is not null)
        {
            return ValidationOutcome.Invalid(contractFailure.ValidationCode ?? ValidationErrorCode.InvalidQuantity, contractFailure.Message);
        }

        return ValidationOutcome.Valid();
    }
}
