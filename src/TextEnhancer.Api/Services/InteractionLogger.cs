using TextEnhancer.Api.Data;

namespace TextEnhancer.Api.Services;

public class InteractionLogger : IInteractionLogger
{
    private readonly AppDbContext _db;
    private readonly ILogger<InteractionLogger> _log;

    public InteractionLogger(AppDbContext db, ILogger<InteractionLogger> log)
    {
        _db = db;
        _log = log;
    }

    public async Task LogAsync(
        string inputText,
        string? outputText,
        string model,
        int promptTokens,
        int completionTokens,
        long latencyMs,
        InteractionStatus status,
        string? errorMessage,
        CancellationToken ct)
    {
        try
        {
            _db.Interactions.Add(new Interaction
            {
                CreatedUtc = DateTime.UtcNow,
                InputText = inputText,
                OutputText = outputText,
                Model = model,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                LatencyMs = latencyMs,
                Status = status,
                ErrorMessage = errorMessage
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Logging must never mask the original outcome. Emit to ILogger and move on.
            _log.LogError(ex, "Failed to persist interaction log (status={Status})", status);
        }
    }
}
