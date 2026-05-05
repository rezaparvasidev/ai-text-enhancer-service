namespace TextEnhancer.Api.Models;

public record EnhancedNote(
    IReadOnlyList<string> WorkCompleted,
    IReadOnlyList<string> SiteObservations,
    IReadOnlyList<string> MaterialsEquipment,
    IReadOnlyList<string> OutcomeFollowUp);
