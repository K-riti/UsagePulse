namespace UsagePulse.Contracts;

public enum DeadLetterReasonCode
{
    ValidationFailed = 1,
    ProcessingFailed = 2,
    InvalidPayload = 3,
    NullPayload = 4,
    CircuitOpen = 5
}
