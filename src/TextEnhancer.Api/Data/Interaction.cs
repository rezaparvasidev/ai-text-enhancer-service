namespace TextEnhancer.Api.Data;

public class Interaction
{
    public long Id { get; set; }

    public DateTime CreatedUtc { get; set; }

    public string InputText { get; set; } = string.Empty;

    public string? OutputText { get; set; }

    public string Model { get; set; } = string.Empty;

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public long LatencyMs { get; set; }

    public InteractionStatus Status { get; set; }

    public string? ErrorMessage { get; set; }

    public string? EnhancedSectionsJson { get; set; }
}
