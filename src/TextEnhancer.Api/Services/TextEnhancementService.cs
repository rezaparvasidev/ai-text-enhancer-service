using System.Diagnostics;
using System.Runtime.CompilerServices;
using TextEnhancer.Api.Prompts;

namespace TextEnhancer.Api.Services;

public class TextEnhancementService : ITextEnhancementService
{
    private readonly IChatCompletionClient _chat;

    public TextEnhancementService(IChatCompletionClient chat)
    {
        _chat = chat;
    }

    public async Task<EnhancementResult> EnhanceAsync(string note, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var result = await _chat.CompleteAsync(SystemPrompt.Value, note, ct);
        sw.Stop();

        return new EnhancementResult(
            EnhancedText: result.Text,
            Model: result.Model,
            PromptTokens: result.PromptTokens,
            CompletionTokens: result.CompletionTokens,
            LatencyMs: sw.ElapsedMilliseconds);
    }

    public async IAsyncEnumerable<ChatStreamChunk> EnhanceStreamAsync(
        string note,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var chunk in _chat.StreamAsync(SystemPrompt.Value, note, ct))
        {
            yield return chunk;
        }
    }
}
