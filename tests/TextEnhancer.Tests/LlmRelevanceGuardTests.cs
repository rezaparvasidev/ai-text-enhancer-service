using Moq;
using TextEnhancer.Api.Prompts;
using TextEnhancer.Api.Services;

namespace TextEnhancer.Tests;

public class LlmRelevanceGuardTests
{
    [Theory]
    [InlineData("RELEVANT: matches landscaping work")]
    [InlineData("relevant: lower case still parses")]
    [InlineData("  RELEVANT:   trimmed whitespace ok  ")]
    public void Parse_Relevant_ReturnsTrue(string text)
    {
        var result = LlmRelevanceGuard.Parse(text);

        Assert.True(result.IsRelevant);
        Assert.NotEmpty(result.Reason);
    }

    [Theory]
    [InlineData("IRRELEVANT: off-topic")]
    [InlineData("irrelevant: chitchat detected")]
    public void Parse_Irrelevant_ReturnsFalse(string text)
    {
        var result = LlmRelevanceGuard.Parse(text);

        Assert.False(result.IsRelevant);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Yes, this is fine.")]
    [InlineData("Some unexpected free-form output without a prefix.")]
    public void Parse_Unparseable_FailsOpen(string text)
    {
        // Misbehaving classifier should not block real submissions.
        var result = LlmRelevanceGuard.Parse(text);

        Assert.True(result.IsRelevant);
        Assert.Contains("unparseable", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsync_PassesRelevancePromptToChatClient()
    {
        var chat = new Mock<IChatCompletionClient>();
        chat.Setup(c => c.CompleteAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatCompletionResult("RELEVANT: ok", 20, 5, "gpt-4o-test"));

        var sut = new LlmRelevanceGuard(chat.Object);

        var result = await sut.CheckAsync("mowed the lawn", CancellationToken.None);

        Assert.True(result.IsRelevant);
        chat.Verify(c => c.CompleteAsync(
            RelevancePrompt.Value, "mowed the lawn", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckAsync_PropagatesUnderlyingExceptions()
    {
        var chat = new Mock<IChatCompletionClient>();
        chat.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("classifier upstream"));

        var sut = new LlmRelevanceGuard(chat.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CheckAsync("any note", CancellationToken.None));
    }
}
