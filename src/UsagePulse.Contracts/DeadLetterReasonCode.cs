namespace UsagePulse.Contracts;

public enum DeadLetterReasonCode
{
    ValidationFailed = 1,
    ProcessingFailed = 2,
    InvalidPayload = 3,
    NullPayload = 4,
    CircuitOpen = 5,
    SchemaIncompatible = 6,
    QuotaExceeded = 7,
    ReplayFailed = 8
}
