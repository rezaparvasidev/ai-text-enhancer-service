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

    /// <summary>
    /// Calls the model with Azure OpenAI Structured Outputs. <paramref name="jsonSchema"/> must be a
    /// JSON Schema (object form). The response is guaranteed to be a JSON document conforming to
    /// that schema; the caller deserializes <see cref="ChatCompletionResult.Text"/> into its DTO.
    /// </summary>
    Task<ChatCompletionResult> CompleteStructuredAsync(
        string systemPrompt,
        string userInput,
        string schemaName,
        string jsonSchema,
        CancellationToken ct);

    IAsyncEnumerable<ChatStreamChunk> StreamAsync(string systemPrompt, string userInput, CancellationToken ct);
}
