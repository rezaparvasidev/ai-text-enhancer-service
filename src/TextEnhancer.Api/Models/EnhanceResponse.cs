namespace TextEnhancer.Api.Models;

public record EnhanceResponse(
    string EnhancedText,
    EnhancedNote? EnhancedSections,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    long LatencyMs);
