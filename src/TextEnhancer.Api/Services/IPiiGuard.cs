namespace TextEnhancer.Api.Services;

public record PiiCheckResult(bool HasPii, IReadOnlyList<string> DetectedTypes);

public interface IPiiGuard
{
    PiiCheckResult Inspect(string text);
}
