namespace TextEnhancer.Api.Services;

public record RelevanceResult(bool IsRelevant, string Reason);

public interface IRelevanceGuard
{
    Task<RelevanceResult> CheckAsync(string note, CancellationToken ct);
}
