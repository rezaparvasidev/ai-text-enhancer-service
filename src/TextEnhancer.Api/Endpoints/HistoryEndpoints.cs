using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TextEnhancer.Api.Data;
using TextEnhancer.Api.Models;

namespace TextEnhancer.Api.Endpoints;

public static class HistoryEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IEndpointRouteBuilder MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/history", async (
            AppDbContext db,
            int? page,
            int? pageSize,
            CancellationToken ct) =>
        {
            var p = Math.Max(1, page ?? 1);
            var ps = Math.Clamp(pageSize ?? 20, 1, 100);

            var total = await db.Interactions.CountAsync(ct);
            var rows = await db.Interactions
                .OrderByDescending(i => i.CreatedUtc)
                .Skip((p - 1) * ps)
                .Take(ps)
                .ToListAsync(ct);

            var items = rows.Select(i => new HistoryItem(
                i.Id,
                i.CreatedUtc,
                i.InputText,
                i.OutputText,
                DeserializeSections(i.EnhancedSectionsJson),
                i.Model,
                i.PromptTokens,
                i.CompletionTokens,
                i.LatencyMs,
                i.Status.ToString(),
                i.ErrorMessage)).ToList();

            return Results.Ok(new HistoryPage(items, p, ps, total));
        })
        .WithName("GetHistory")
        .WithSummary("Returns a paginated list of past enhancement interactions, newest first.")
        .Produces<HistoryPage>(StatusCodes.Status200OK);

        return app;
    }

    private static EnhancedNote? DeserializeSections(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<EnhancedNote>(json, JsonOptions); }
        catch (JsonException) { return null; }
    }
}
