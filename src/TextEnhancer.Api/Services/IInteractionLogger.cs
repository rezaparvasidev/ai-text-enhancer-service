using TextEnhancer.Api.Data;

namespace TextEnhancer.Api.Services;

public interface IInteractionLogger
{
    Task LogAsync(
        string inputText,
        string? outputText,
        string model,
        int promptTokens,
        int completionTokens,
        long latencyMs,
        InteractionStatus status,
        string? errorMessage,
        CancellationToken ct,
        string? enhancedSectionsJson = null);
}
