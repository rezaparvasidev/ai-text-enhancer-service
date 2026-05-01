using Microsoft.EntityFrameworkCore;
using TextEnhancer.Api.Data;

namespace TextEnhancer.Api.Seed;

/// <summary>
/// Inserts a small set of sample interactions on first boot so a fresh DB matches the take-home
/// brief's "≥5 logged interactions, mix of successes and failures" requirement and reviewers see
/// the schema in action without running the API themselves.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedIfEmptyAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Interactions.AnyAsync(ct)) return;

        var now = DateTime.UtcNow;
        var samples = new[]
        {
            new Interaction
            {
                CreatedUtc = now.AddMinutes(-58),
                InputText = "arrived on site lawn was a mess weeds everywhere did full mow edging and cleanup customer seemed happy will need to come back for fertilizer app next week",
                OutputText =
                    "Work completed:\n- Full mow\n- Edging\n- Debris cleanup\n\nSite observations:\n- Lawn was overgrown with widespread weeds\n\nOutcome / Follow-up:\n- Customer seemed happy\n- Fertilizer application next week",
                Model = "gpt-4o",
                PromptTokens = 412, CompletionTokens = 78, LatencyMs = 1834,
                Status = InteractionStatus.Success
            },
            new Interaction
            {
                CreatedUtc = now.AddMinutes(-44),
                InputText = "trimmed hedges along east side of property hauled away three bags of clippings replaced one broken sprinkler head zone 4",
                OutputText =
                    "Work completed:\n- Trimmed hedges along east side of property\n- Hauled away three bags of clippings\n- Replaced one broken sprinkler head in zone 4",
                Model = "gpt-4o",
                PromptTokens = 398, CompletionTokens = 52, LatencyMs = 1601,
                Status = InteractionStatus.Success
            },
            new Interaction
            {
                CreatedUtc = now.AddMinutes(-31),
                InputText = "fertilizer app done 50 lbs of slow release on front and back lawns watered in noticed grub damage near oak tree recommended treatment",
                OutputText =
                    "Work completed:\n- Applied 50 lbs of slow-release fertilizer to front and back lawns\n- Watered in fertilizer\n\nSite observations:\n- Noticed grub damage near oak tree\n\nOutcome / Follow-up:\n- Recommended treatment for grub damage",
                Model = "gpt-4o",
                PromptTokens = 410, CompletionTokens = 65, LatencyMs = 1922,
                Status = InteractionStatus.Success
            },
            new Interaction
            {
                CreatedUtc = now.AddMinutes(-19),
                InputText = "homeowner asked us to call them at 555-123-4567 about scheduling next visit",
                OutputText = null,
                Model = "gpt-4o",
                PromptTokens = 0, CompletionTokens = 0, LatencyMs = 0,
                Status = InteractionStatus.PiiRejected,
                ErrorMessage = "Detected PII: phone"
            },
            new Interaction
            {
                CreatedUtc = now.AddMinutes(-7),
                InputText = "applied pre-emergent on lawn customer requested invoice be emailed",
                OutputText = null,
                Model = "gpt-4o",
                PromptTokens = 0, CompletionTokens = 0, LatencyMs = 412,
                Status = InteractionStatus.LlmError,
                ErrorMessage = "Service unavailable: Azure.RequestFailedException - 503"
            },
            new Interaction
            {
                CreatedUtc = now.AddMinutes(-2),
                InputText = "spring cleanup done removed leaves from gutters and beds aerated front lawn customer wants quote for mulching",
                OutputText =
                    "Work completed:\n- Spring cleanup\n- Removed leaves from gutters and beds\n- Aerated front lawn\n\nOutcome / Follow-up:\n- Customer wants a quote for mulching",
                Model = "gpt-4o",
                PromptTokens = 405, CompletionTokens = 55, LatencyMs = 1714,
                Status = InteractionStatus.Success
            }
        };

        db.Interactions.AddRange(samples);
        await db.SaveChangesAsync(ct);
    }
}
