namespace TextEnhancer.Api.Services;

public record ChatCompletionResult(
    string Text,
    int PromptTokens,
    int CompletionTokens,
    string Model);

public record ChatStreamChunk(
    string DeltaText,
    bool IsFinished,
    int PromptTokens = 0,
    int CompletionTokens = 0,
    string? Model = null);

/// <summary>
/// Thin abstraction over the Azure OpenAI ChatClient. Exists so the LLM call can be mocked in tests
/// without depending on the SDK's concrete <c>OpenAI.Chat.ChatClient</c>.
/// </summary>
public interface IChatCompletionClient
{
    Task<ChatCompletionResult> CompleteAsync(string systemPrompt, string userInput, CancellationToken ct);

    IAsyncEnumerable<ChatStreamChunk> StreamAsync(string systemPrompt, string userInput, CancellationToken ct);
}
