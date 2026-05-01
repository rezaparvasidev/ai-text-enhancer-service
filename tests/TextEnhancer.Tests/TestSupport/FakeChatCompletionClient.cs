using System.Runtime.CompilerServices;
using TextEnhancer.Api.Services;

namespace TextEnhancer.Tests.TestSupport;

/// <summary>
/// Configurable fake chat client for integration tests. Each test mutates the public delegates to
/// shape the response or fail the call.
/// </summary>
public class FakeChatCompletionClient : IChatCompletionClient
{
    /// <summary>
    /// Default handler routes by system prompt: relevance classifier calls (which use the
    /// "RELEVANT:/IRRELEVANT:" output contract) get a RELEVANT response; everything else gets
    /// the canned enhancement output. Tests override this to simulate failures or rejections.
    /// </summary>
    public Func<string, string, ChatCompletionResult> CompleteHandler { get; set; }
        = (sys, _) => sys.Contains("relevance classifier", StringComparison.OrdinalIgnoreCase)
            ? new ChatCompletionResult("RELEVANT: looks like a landscaping job note", 30, 8, "gpt-4o-test")
            : new ChatCompletionResult("- enhanced output", 10, 5, "gpt-4o-test");

    public Func<string, string, IEnumerable<ChatStreamChunk>> StreamHandler { get; set; }
        = (_, _) => new[]
        {
            new ChatStreamChunk("- streamed", IsFinished: false),
            new ChatStreamChunk(" output", IsFinished: false),
            new ChatStreamChunk(string.Empty, IsFinished: true,
                PromptTokens: 10, CompletionTokens: 5, Model: "gpt-4o-test")
        };

    public Task<ChatCompletionResult> CompleteAsync(string systemPrompt, string userInput, CancellationToken ct)
        => Task.FromResult(CompleteHandler(systemPrompt, userInput));

    public async IAsyncEnumerable<ChatStreamChunk> StreamAsync(
        string systemPrompt,
        string userInput,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var chunk in StreamHandler(systemPrompt, userInput))
        {
            yield return chunk;
            await Task.Yield();
        }
    }
}
