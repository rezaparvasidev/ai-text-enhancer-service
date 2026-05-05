using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TextEnhancer.Api.Models;
using TextEnhancer.Api.Prompts;

namespace TextEnhancer.Api.Services;

public class TextEnhancementService : ITextEnhancementService
{
    private const string SchemaName = "enhanced_note";

    private static readonly string SchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "workCompleted":      { "type": "array", "items": { "type": "string" } },
            "siteObservations":   { "type": "array", "items": { "type": "string" } },
            "materialsEquipment": { "type": "array", "items": { "type": "string" } },
            "outcomeFollowUp":    { "type": "array", "items": { "type": "string" } }
          },
          "required": ["workCompleted", "siteObservations", "materialsEquipment", "outcomeFollowUp"]
        }
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IChatCompletionClient _chat;

    public TextEnhancementService(IChatCompletionClient chat)
    {
        _chat = chat;
    }

    public async Task<EnhancementResult> EnhanceAsync(string note, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var result = await _chat.CompleteStructuredAsync(
            StructuredEnhancementPrompt.Value, note, SchemaName, SchemaJson, ct);
        sw.Stop();

        var sections = JsonSerializer.Deserialize<EnhancedNote>(result.Text, JsonOptions)
            ?? new EnhancedNote(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        var displayText = EnhancedNoteFormatter.Render(sections);

        return new EnhancementResult(
            EnhancedText: displayText,
            Sections: sections,
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
