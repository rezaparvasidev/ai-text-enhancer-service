using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TextEnhancer.Api.Data;
using TextEnhancer.Api.Models;
using TextEnhancer.Api.Services;
using TextEnhancer.Tests.TestSupport;

namespace TextEnhancer.Tests;

public class EnhanceEndpointTests : IClassFixture<TextEnhancerWebAppFactory>
{
    private readonly TextEnhancerWebAppFactory _factory;

    public EnhanceEndpointTests(TextEnhancerWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unauthenticated_Api_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/enhance", new { note = "anything" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidNote_Returns200_AndLogsSuccess()
    {
        _factory.FakeChat.CompleteHandler = (_, _) =>
            new ChatCompletionResult("- bullet a\n- bullet b", 120, 30, "gpt-4o-test");
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/enhance", new { note = "raw technician note" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EnhanceResponse>();
        Assert.NotNull(body);
        Assert.Equal("- bullet a\n- bullet b", body!.EnhancedText);
        Assert.Equal("gpt-4o-test", body.Model);
        Assert.Equal(120, body.PromptTokens);
        Assert.Equal(30, body.CompletionTokens);
        Assert.True(body.LatencyMs >= 0);

        await AssertLastInteractionAsync(i =>
        {
            Assert.Equal(InteractionStatus.Success, i.Status);
            Assert.Equal("raw technician note", i.InputText);
            Assert.Equal("- bullet a\n- bullet b", i.OutputText);
            Assert.Equal(120, i.PromptTokens);
        });
    }

    [Fact]
    public async Task EmptyNote_Returns400_AndLogsValidationError()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/enhance", new { note = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("validation_error", error!.Code);

        await AssertLastInteractionAsync(i =>
            Assert.Equal(InteractionStatus.ValidationError, i.Status));
    }

    [Fact]
    public async Task NoteWithEmail_Returns400PiiRejected_AndLogs()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/enhance",
            new { note = "follow up with john@example.com about visit" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("pii_rejected", error!.Code);

        await AssertLastInteractionAsync(i =>
        {
            Assert.Equal(InteractionStatus.PiiRejected, i.Status);
            Assert.Contains("email", i.ErrorMessage);
            Assert.Null(i.OutputText);
        });
    }

    [Fact]
    public async Task OffTopicNote_Returns400_AndLogsOffTopicRejected()
    {
        // Make the classifier reject; enhancement handler shouldn't even be called.
        _factory.FakeChat.CompleteHandler = (sys, _) =>
            sys.Contains("relevance classifier", StringComparison.OrdinalIgnoreCase)
                ? new ChatCompletionResult("IRRELEVANT: this is a recipe, not a job note", 30, 10, "gpt-4o-test")
                : throw new Xunit.Sdk.XunitException("Enhancement should not be invoked when classifier rejects.");

        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/enhance",
            new { note = "to make pasta carbonara, beat eggs with pecorino..." });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("off_topic_rejected", error!.Code);

        await AssertLastInteractionAsync(i =>
        {
            Assert.Equal(InteractionStatus.OffTopicRejected, i.Status);
            Assert.Contains("recipe", i.ErrorMessage);
            Assert.Null(i.OutputText);
        });

        // restore default
        _factory.FakeChat.CompleteHandler = (sys, _) =>
            sys.Contains("relevance classifier", StringComparison.OrdinalIgnoreCase)
                ? new ChatCompletionResult("RELEVANT: looks like a landscaping job note", 30, 8, "gpt-4o-test")
                : new ChatCompletionResult("- enhanced output", 10, 5, "gpt-4o-test");
    }

    [Fact]
    public async Task LlmFailure_Returns502_AndLogsError()
    {
        _factory.FakeChat.CompleteHandler = (_, _) =>
            throw new InvalidOperationException("simulated AOAI 503");
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/enhance", new { note = "valid note text" });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("llm_error", error!.Code);
        Assert.DoesNotContain("simulated", error.Message); // do not leak internals

        await AssertLastInteractionAsync(i =>
        {
            Assert.Equal(InteractionStatus.LlmError, i.Status);
            Assert.Contains("simulated AOAI 503", i.ErrorMessage);
            Assert.Null(i.OutputText);
        });

        // restore handler so other tests in this fixture aren't affected
        _factory.FakeChat.CompleteHandler = (_, _) =>
            new ChatCompletionResult("- enhanced output", 10, 5, "gpt-4o-test");
    }

    [Fact]
    public async Task StreamEndpoint_EmitsSseDeltasAndDone()
    {
        _factory.FakeChat.StreamHandler = (_, _) => new[]
        {
            new ChatStreamChunk("- alpha", IsFinished: false),
            new ChatStreamChunk(" beta", IsFinished: false),
            new ChatStreamChunk(string.Empty, IsFinished: true,
                PromptTokens: 12, CompletionTokens: 4, Model: "gpt-4o-test")
        };
        var client = _factory.CreateAuthenticatedClient();

        var content = new StringContent(
            "{\"note\": \"some note\"}",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/enhance/stream", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("data: {\"delta\":\"- alpha\"}", body);
        Assert.Contains("data: {\"delta\":\" beta\"}", body);
        Assert.Contains("event: done", body);
        Assert.Contains("\"promptTokens\":12", body);

        await AssertLastInteractionAsync(i =>
        {
            Assert.Equal(InteractionStatus.Success, i.Status);
            Assert.Equal("- alpha beta", i.OutputText);
            Assert.Equal(12, i.PromptTokens);
        });
    }

    private async Task AssertLastInteractionAsync(Action<Interaction> assert)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var last = db.Interactions.OrderByDescending(i => i.Id).FirstOrDefault();
        Assert.NotNull(last);
        assert(last!);
        await Task.CompletedTask;
    }
}
