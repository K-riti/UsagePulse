namespace UsagePulse.Contracts;

public enum ValidationErrorCode
{
    MissingEventId = 1,
    MissingTenantId = 2,
    MissingFeature = 3,
    InvalidQuantity = 4,
    InvalidOccurredAt = 5
}
