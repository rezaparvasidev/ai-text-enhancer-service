using System.Runtime.CompilerServices;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace TextEnhancer.Api.Services;

/// <summary>
/// Adapter that talks to Azure OpenAI via the Azure.AI.OpenAI v2 SDK. The rest of the app only sees
/// <see cref="IChatCompletionClient"/>, so this is the only file that imports the SDK.
/// </summary>
public class AzureOpenAIChatCompletionClient : IChatCompletionClient
{
    private readonly ChatClient _chatClient;
    private readonly string _deployment;

    public AzureOpenAIChatCompletionClient(IOptions<AzureOpenAIOptions> options)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.Endpoint))
            throw new InvalidOperationException("AOAI:Endpoint is not configured.");
        if (string.IsNullOrWhiteSpace(opts.Deployment))
            throw new InvalidOperationException("AOAI:Deployment is not configured.");
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
            throw new InvalidOperationException("AOAI:ApiKey is not configured.");

        var aoai = new AzureOpenAIClient(new Uri(opts.Endpoint), new AzureKeyCredential(opts.ApiKey));
        _chatClient = aoai.GetChatClient(opts.Deployment);
        _deployment = opts.Deployment;
    }

    public async Task<ChatCompletionResult> CompleteAsync(string systemPrompt, string userInput, CancellationToken ct)
    {
        var messages = new ChatMessage[]
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userInput)
        };

        var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: ct);
        return ToResult(response.Value);
    }

    public async Task<ChatCompletionResult> CompleteStructuredAsync(
        string systemPrompt,
        string userInput,
        string schemaName,
        string jsonSchema,
        CancellationToken ct)
    {
        var messages = new ChatMessage[]
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userInput)
        };

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: schemaName,
                jsonSchema: BinaryData.FromString(jsonSchema),
                jsonSchemaIsStrict: true)
        };

        var response = await _chatClient.CompleteChatAsync(messages, options, ct);
        return ToResult(response.Value);
    }

    private ChatCompletionResult ToResult(ChatCompletion completion)
    {
        var text = completion.Content.Count > 0 ? completion.Content[0].Text : string.Empty;
        var prompt = completion.Usage?.InputTokenCount ?? 0;
        var output = completion.Usage?.OutputTokenCount ?? 0;
        var model = string.IsNullOrEmpty(completion.Model) ? _deployment : completion.Model;
        return new ChatCompletionResult(text, prompt, output, model);
    }

    public async IAsyncEnumerable<ChatStreamChunk> StreamAsync(
        string systemPrompt,
        string userInput,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var messages = new ChatMessage[]
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userInput)
        };

        var updates = _chatClient.CompleteChatStreamingAsync(messages, cancellationToken: ct);

        var prompt = 0;
        var output = 0;
        string? model = null;

        await foreach (var update in updates.WithCancellation(ct))
        {
            if (update.Usage is { } usage)
            {
                prompt = usage.InputTokenCount;
                output = usage.OutputTokenCount;
            }
            if (!string.IsNullOrEmpty(update.Model))
            {
                model = update.Model;
            }

            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    yield return new ChatStreamChunk(part.Text, IsFinished: false);
                }
            }
        }

        yield return new ChatStreamChunk(
            DeltaText: string.Empty,
            IsFinished: true,
            PromptTokens: prompt,
            CompletionTokens: output,
            Model: model ?? _deployment);
    }
}
