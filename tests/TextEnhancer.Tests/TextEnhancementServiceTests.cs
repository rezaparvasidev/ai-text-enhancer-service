using Moq;
using TextEnhancer.Api.Prompts;
using TextEnhancer.Api.Services;

namespace TextEnhancer.Tests;

public class TextEnhancementServiceTests
{
    private const string ValidSectionsJson =
        """{"workCompleted":["Full mow","Edging"],"siteObservations":[],"materialsEquipment":[],"outcomeFollowUp":["Fertilizer next week"]}""";

    [Fact]
    public async Task EnhanceAsync_ForwardsStructuredPromptAndUserNote()
    {
        var chat = new Mock<IChatCompletionClient>();
        chat.Setup(c => c.CompleteStructuredAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatCompletionResult(ValidSectionsJson, 10, 5, "gpt-4o"));

        var sut = new TextEnhancementService(chat.Object);

        await sut.EnhanceAsync("raw note", CancellationToken.None);

        chat.Verify(c => c.CompleteStructuredAsync(
            StructuredEnhancementPrompt.Value,
            "raw note",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnhanceAsync_DeserializesSectionsAndRendersDisplayText()
    {
        var chat = new Mock<IChatCompletionClient>();
        chat.Setup(c => c.CompleteStructuredAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatCompletionResult(ValidSectionsJson, 100, 25, "gpt-4o"));

        var sut = new TextEnhancementService(chat.Object);

        var result = await sut.EnhanceAsync("note", CancellationToken.None);

        Assert.Equal(new[] { "Full mow", "Edging" }, result.Sections.WorkCompleted);
        Assert.Equal(new[] { "Fertilizer next week" }, result.Sections.OutcomeFollowUp);
        Assert.Empty(result.Sections.SiteObservations);
        Assert.Contains("Work completed:", result.EnhancedText);
        Assert.Contains("- Full mow", result.EnhancedText);
        Assert.Contains("Outcome / Follow-up:", result.EnhancedText);
        Assert.Equal("gpt-4o", result.Model);
        Assert.Equal(100, result.PromptTokens);
        Assert.Equal(25, result.CompletionTokens);
        Assert.True(result.LatencyMs >= 0);
    }

    [Fact]
    public async Task EnhanceAsync_PropagatesUnderlyingExceptions()
    {
        var chat = new Mock<IChatCompletionClient>();
        chat.Setup(c => c.CompleteStructuredAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("upstream failure"));

        var sut = new TextEnhancementService(chat.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.EnhanceAsync("note", CancellationToken.None));
    }
}
