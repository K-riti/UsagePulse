using UsagePulse.Contracts;

namespace UsagePulse.Processing.Services.Pipeline;

public sealed class UsageEventValidationStage : IUsageEventValidationStage
{
    public ValidationOutcome Validate(UsageEvent usageEvent)
    {
        if (string.IsNullOrWhiteSpace(usageEvent.EventId))
        {
            return ValidationOutcome.Invalid(ValidationErrorCode.MissingEventId, "EventId is required.");
        }

        if (string.IsNullOrWhiteSpace(usageEvent.TenantId))
        {
            return ValidationOutcome.Invalid(ValidationErrorCode.MissingTenantId, "TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(usageEvent.Feature))
        {
            return ValidationOutcome.Invalid(ValidationErrorCode.MissingFeature, "Feature is required.");
        }

        if (usageEvent.Quantity <= 0)
        {
            return ValidationOutcome.Invalid(ValidationErrorCode.InvalidQuantity, "Quantity must be greater than 0.");
        }

        if (usageEvent.OccurredAt == default)
        {
            return ValidationOutcome.Invalid(ValidationErrorCode.InvalidOccurredAt, "OccurredAt is required.");
        }

        return ValidationOutcome.Valid();
    }
}
