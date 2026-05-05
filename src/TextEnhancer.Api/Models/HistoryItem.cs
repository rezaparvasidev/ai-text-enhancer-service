namespace TextEnhancer.Api.Models;

public record HistoryItem(
    long Id,
    DateTime CreatedUtc,
    string InputText,
    string? OutputText,
    EnhancedNote? EnhancedSections,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    long LatencyMs,
    string Status,
    string? ErrorMessage);

public record HistoryPage(
    IReadOnlyList<HistoryItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
