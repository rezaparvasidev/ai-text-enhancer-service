using TextEnhancer.Api.Services;

namespace TextEnhancer.Tests;

public class PiiGuardTests
{
    private readonly PiiGuard _guard = new();

    [Fact]
    public void CleanText_HasNoPii()
    {
        var result = _guard.Inspect("did full mow edging and cleanup customer seemed happy");

        Assert.False(result.HasPii);
        Assert.Empty(result.DetectedTypes);
    }

    [Theory]
    [InlineData("contact me at john.smith@example.com")]
    [InlineData("Email JOHN+work@SUB.example.co.uk for invoice")]
    public void Email_IsDetected(string text)
    {
        var result = _guard.Inspect(text);

        Assert.True(result.HasPii);
        Assert.Contains("email", result.DetectedTypes);
    }

    [Theory]
    [InlineData("call 555-123-4567 about the visit")]
    [InlineData("phone (555) 123-4567 to reschedule")]
    [InlineData("ph 555.123.4567 if needed")]
    [InlineData("US +1 555 123 4567 line")]
    public void PhoneNumber_IsDetected(string text)
    {
        var result = _guard.Inspect(text);

        Assert.True(result.HasPii);
        Assert.Contains("phone", result.DetectedTypes);
    }

    [Fact]
    public void TenDigitOrderId_IsNotMistakenForPhone()
    {
        // No separators, embedded in alphanumeric — should not trigger phone regex.
        var result = _guard.Inspect("invoice number 5551234567xyz");

        Assert.DoesNotContain("phone", result.DetectedTypes);
    }

    [Fact]
    public void Ssn_IsDetected()
    {
        var result = _guard.Inspect("filed under 123-45-6789 in records");

        Assert.True(result.HasPii);
        Assert.Contains("ssn", result.DetectedTypes);
    }

    [Fact]
    public void EmptyOrWhitespace_IsClean()
    {
        Assert.False(_guard.Inspect("").HasPii);
        Assert.False(_guard.Inspect("   ").HasPii);
    }
}
