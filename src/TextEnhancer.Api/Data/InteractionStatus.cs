namespace TextEnhancer.Api.Data;

public enum InteractionStatus
{
    Success,
    LlmError,
    PiiRejected,
    OffTopicRejected,
    ValidationError
}
