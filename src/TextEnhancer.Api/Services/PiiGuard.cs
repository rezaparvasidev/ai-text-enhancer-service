using System.Text.RegularExpressions;

namespace TextEnhancer.Api.Services;

/// <summary>
/// Lightweight regex-based PII detector. Intentionally conservative — false positives are preferred
/// over leaking PII to the LLM. Not a substitute for a real DLP system.
/// </summary>
public partial class PiiGuard : IPiiGuard
{
    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    // North American phone numbers in common formats: 555-555-5555, (555) 555-5555, 555.555.5555,
    // 5555555555, +1 555 555 5555. Requires at least one separator or country code so we don't
    // false-positive on every 10-digit number (e.g., a license plate or order id).
    [GeneratedRegex(@"(?:\+?1[\s.\-]?)?(?:\(\d{3}\)\s?|\d{3}[\s.\-])\d{3}[\s.\-]\d{4}\b")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b")]
    private static partial Regex SsnRegex();

    public PiiCheckResult Inspect(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new PiiCheckResult(false, Array.Empty<string>());

        var detected = new List<string>();
        if (EmailRegex().IsMatch(text)) detected.Add("email");
        if (PhoneRegex().IsMatch(text)) detected.Add("phone");
        if (SsnRegex().IsMatch(text)) detected.Add("ssn");

        return new PiiCheckResult(detected.Count > 0, detected);
    }
}
