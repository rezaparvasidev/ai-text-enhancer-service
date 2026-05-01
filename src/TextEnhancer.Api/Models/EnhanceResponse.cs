namespace TextEnhancer.Api.Models;

public record EnhanceResponse(
    string EnhancedText,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    long LatencyMs);
