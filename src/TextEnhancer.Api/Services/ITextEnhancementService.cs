using TextEnhancer.Api.Models;

namespace TextEnhancer.Api.Services;

public record EnhancementResult(
    string EnhancedText,
    EnhancedNote Sections,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    long LatencyMs);

public interface ITextEnhancementService
{
    Task<EnhancementResult> EnhanceAsync(string note, CancellationToken ct);

    IAsyncEnumerable<ChatStreamChunk> EnhanceStreamAsync(string note, CancellationToken ct);
}
