using TextEnhancer.Api.Prompts;

namespace TextEnhancer.Api.Services;

public class LlmRelevanceGuard : IRelevanceGuard
{
    private readonly IChatCompletionClient _chat;

    public LlmRelevanceGuard(IChatCompletionClient chat)
    {
        _chat = chat;
    }

    public async Task<RelevanceResult> CheckAsync(string note, CancellationToken ct)
    {
        var result = await _chat.CompleteAsync(RelevancePrompt.Value, note, ct);
        return Parse(result.Text);
    }

    /// <summary>
    /// Parses the classifier's single-line response. Fail-open: if the model returns something
    /// unexpected, treat the note as relevant rather than block a real submission. The endpoint
    /// still logs token usage / latency, so a misbehaving classifier is observable.
    /// </summary>
    public static RelevanceResult Parse(string text)
    {
        var trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.StartsWith("RELEVANT:", StringComparison.OrdinalIgnoreCase))
            return new RelevanceResult(true, trimmed["RELEVANT:".Length..].Trim());

        if (trimmed.StartsWith("IRRELEVANT:", StringComparison.OrdinalIgnoreCase))
            return new RelevanceResult(false, trimmed["IRRELEVANT:".Length..].Trim());

        return new RelevanceResult(true, "Classifier returned an unparseable response; allowing through.");
    }
}
