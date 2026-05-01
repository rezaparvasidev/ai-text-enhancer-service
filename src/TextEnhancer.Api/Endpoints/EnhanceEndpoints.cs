using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TextEnhancer.Api.Data;
using TextEnhancer.Api.Models;
using TextEnhancer.Api.Services;

namespace TextEnhancer.Api.Endpoints;

public static class EnhanceEndpoints
{
    public static IEndpointRouteBuilder MapEnhanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/enhance", HandleEnhance)
            .WithName("EnhanceNote")
            .WithSummary("Enhance a raw technician note into a polished, bulleted report.")
            .Produces<EnhanceResponse>(StatusCodes.Status200OK)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status502BadGateway);

        app.MapPost("/api/enhance/stream", HandleEnhanceStream)
            .WithName("EnhanceNoteStream")
            .WithSummary("Stream an enhanced note via Server-Sent Events as it is generated.");

        return app;
    }

    private static async Task<IResult> HandleEnhance(
        EnhanceRequest? request,
        ITextEnhancementService enhancementService,
        IPiiGuard piiGuard,
        IInteractionLogger logger,
        IOptions<AzureOpenAIOptions> opts,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var endpointLog = loggerFactory.CreateLogger("EnhanceEndpoint");
        var note = (request?.Note ?? string.Empty).Trim();
        var modelLabel = opts.Value.Deployment;

        if (string.IsNullOrEmpty(note) || note.Length > 5000)
        {
            await logger.LogAsync(note, null, modelLabel, 0, 0, 0,
                InteractionStatus.ValidationError, "Note empty or exceeds 5000 chars.", ct);
            return Results.BadRequest(new ErrorResponse(
                "validation_error",
                "Field 'note' is required and must be 1-5000 characters."));
        }

        var piiResult = piiGuard.Inspect(note);
        if (piiResult.HasPii)
        {
            var detected = string.Join(", ", piiResult.DetectedTypes);
            await logger.LogAsync(note, null, modelLabel, 0, 0, 0,
                InteractionStatus.PiiRejected, $"Detected PII: {detected}", ct);
            return Results.BadRequest(new ErrorResponse(
                "pii_rejected",
                $"Note appears to contain personally identifiable information ({detected}). Please remove it and resubmit."));
        }

        try
        {
            var result = await enhancementService.EnhanceAsync(note, ct);
            await logger.LogAsync(note, result.EnhancedText, result.Model,
                result.PromptTokens, result.CompletionTokens, result.LatencyMs,
                InteractionStatus.Success, null, ct);

            return Results.Ok(new EnhanceResponse(
                result.EnhancedText,
                result.Model,
                result.PromptTokens,
                result.CompletionTokens,
                result.LatencyMs));
        }
        catch (Exception ex)
        {
            endpointLog.LogError(ex, "LLM call failed.");
            await logger.LogAsync(note, null, modelLabel, 0, 0, 0,
                InteractionStatus.LlmError, ex.Message, ct);
            return Results.Json(
                new ErrorResponse("llm_error", "The enhancement service is currently unavailable. Please try again."),
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task HandleEnhanceStream(
        HttpContext http,
        EnhanceRequest? request,
        ITextEnhancementService enhancementService,
        IPiiGuard piiGuard,
        IInteractionLogger logger,
        IOptions<AzureOpenAIOptions> opts,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var endpointLog = loggerFactory.CreateLogger("EnhanceStreamEndpoint");
        var note = (request?.Note ?? string.Empty).Trim();
        var modelLabel = opts.Value.Deployment;

        if (string.IsNullOrEmpty(note) || note.Length > 5000)
        {
            await logger.LogAsync(note, null, modelLabel, 0, 0, 0,
                InteractionStatus.ValidationError, "Note empty or exceeds 5000 chars.", ct);
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsJsonAsync(new ErrorResponse(
                "validation_error",
                "Field 'note' is required and must be 1-5000 characters."), ct);
            return;
        }

        var piiResult = piiGuard.Inspect(note);
        if (piiResult.HasPii)
        {
            var detected = string.Join(", ", piiResult.DetectedTypes);
            await logger.LogAsync(note, null, modelLabel, 0, 0, 0,
                InteractionStatus.PiiRejected, $"Detected PII: {detected}", ct);
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsJsonAsync(new ErrorResponse(
                "pii_rejected",
                $"Note appears to contain personally identifiable information ({detected})."), ct);
            return;
        }

        http.Response.Headers["Content-Type"] = "text/event-stream";
        http.Response.Headers["Cache-Control"] = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";

        var sw = Stopwatch.StartNew();
        var collected = new StringBuilder();
        var promptTokens = 0;
        var completionTokens = 0;
        var modelUsed = modelLabel;

        try
        {
            await foreach (var chunk in enhancementService.EnhanceStreamAsync(note, ct))
            {
                if (chunk.IsFinished)
                {
                    promptTokens = chunk.PromptTokens;
                    completionTokens = chunk.CompletionTokens;
                    modelUsed = chunk.Model ?? modelLabel;
                    sw.Stop();
                    var donePayload = JsonSerializer.Serialize(new
                    {
                        done = true,
                        model = modelUsed,
                        promptTokens,
                        completionTokens,
                        latencyMs = sw.ElapsedMilliseconds
                    });
                    await http.Response.WriteAsync($"event: done\ndata: {donePayload}\n\n", ct);
                    await http.Response.Body.FlushAsync(ct);
                }
                else if (!string.IsNullOrEmpty(chunk.DeltaText))
                {
                    collected.Append(chunk.DeltaText);
                    var deltaPayload = JsonSerializer.Serialize(new { delta = chunk.DeltaText });
                    await http.Response.WriteAsync($"data: {deltaPayload}\n\n", ct);
                    await http.Response.Body.FlushAsync(ct);
                }
            }

            if (sw.IsRunning) sw.Stop();
            await logger.LogAsync(note, collected.ToString(), modelUsed,
                promptTokens, completionTokens, sw.ElapsedMilliseconds,
                InteractionStatus.Success, null, ct);
        }
        catch (Exception ex)
        {
            if (sw.IsRunning) sw.Stop();
            endpointLog.LogError(ex, "LLM streaming call failed.");
            await logger.LogAsync(note,
                collected.Length > 0 ? collected.ToString() : null,
                modelUsed, promptTokens, completionTokens, sw.ElapsedMilliseconds,
                InteractionStatus.LlmError, ex.Message, ct);

            var errPayload = JsonSerializer.Serialize(new
            {
                code = "llm_error",
                message = "The enhancement stream failed before completion."
            });
            try
            {
                await http.Response.WriteAsync($"event: error\ndata: {errPayload}\n\n", ct);
                await http.Response.Body.FlushAsync(ct);
            }
            catch
            {
                // Connection may already be torn down.
            }
        }
    }
}
