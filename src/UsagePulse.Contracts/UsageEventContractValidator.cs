namespace UsagePulse.Contracts;

public static class UsageEventContractValidator
{
    public static ProcessingFailure? Validate(UsageEvent usageEvent)
    {
        if (string.IsNullOrWhiteSpace(usageEvent.UserId))
        {
            return new ProcessingFailure(DeadLetterReasonCode.ValidationFailed, "UserId is required.", ValidationErrorCode.MissingUserId);
        }

        if (usageEvent.Quantity <= 0)
        {
            return new ProcessingFailure(DeadLetterReasonCode.ValidationFailed, "Quantity must be greater than 0.", ValidationErrorCode.InvalidQuantity);
        }

        if (usageEvent.OccurredAt == default)
        {
            return new ProcessingFailure(DeadLetterReasonCode.ValidationFailed, "OccurredAt is required.", ValidationErrorCode.InvalidOccurredAt);
        }

        return null;
    }
}
