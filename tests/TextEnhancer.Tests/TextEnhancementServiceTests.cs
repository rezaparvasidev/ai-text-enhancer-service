using Moq;
using TextEnhancer.Api.Prompts;
using TextEnhancer.Api.Services;

namespace TextEnhancer.Tests;

public class TextEnhancementServiceTests
{
    [Fact]
    public async Task EnhanceAsync_ForwardsSystemPromptAndUserNote()
    {
        var chat = new Mock<IChatCompletionClient>();
        chat.Setup(c => c.CompleteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatCompletionResult("- ok", 10, 5, "gpt-4o"));

        var sut = new TextEnhancementService(chat.Object);

        await sut.EnhanceAsync("raw note", CancellationToken.None);

        chat.Verify(c => c.CompleteAsync(
            SystemPrompt.Value,
            "raw note",
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnhanceAsync_PopulatesAllResultFields()
    {
        var chat = new Mock<IChatCompletionClient>();
        chat.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatCompletionResult("- bullet", 100, 25, "gpt-4o"));

        var sut = new TextEnhancementService(chat.Object);

        var result = await sut.EnhanceAsync("note", CancellationToken.None);

        Assert.Equal("- bullet", result.EnhancedText);
        Assert.Equal("gpt-4o", result.Model);
        Assert.Equal(100, result.PromptTokens);
        Assert.Equal(25, result.CompletionTokens);
        Assert.True(result.LatencyMs >= 0);
    }

    [Fact]
    public async Task EnhanceAsync_PropagatesUnderlyingExceptions()
    {
        var chat = new Mock<IChatCompletionClient>();
        chat.Setup(c => c.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("upstream failure"));

        var sut = new TextEnhancementService(chat.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.EnhanceAsync("note", CancellationToken.None));
    }
}
