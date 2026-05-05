using System.Text;
using TextEnhancer.Api.Models;

namespace TextEnhancer.Api.Services;

/// <summary>
/// Converts an <see cref="EnhancedNote"/> to and from the human-readable bulleted text form
/// (`Heading:\n- item\n- item`). The structured non-streaming path uses <see cref="Render"/> to
/// produce a display string for the legacy "Output" column. The streaming path uses
/// <see cref="ParseFromBulletText"/> to back-fill structured sections from the collected bullet
/// text so streamed rows still populate the section-specific history columns.
/// </summary>
public static class EnhancedNoteFormatter
{
    private static readonly (string Heading, Func<EnhancedNote, IReadOnlyList<string>> Get)[] Sections =
    {
        ("Work completed",      n => n.WorkCompleted),
        ("Site observations",   n => n.SiteObservations),
        ("Materials / equipment", n => n.MaterialsEquipment),
        ("Outcome / Follow-up", n => n.OutcomeFollowUp),
    };

    public static string Render(EnhancedNote note)
    {
        var sb = new StringBuilder();
        foreach (var (heading, get) in Sections)
        {
            var items = get(note);
            if (items is null || items.Count == 0) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(heading).Append(":\n");
            for (var i = 0; i < items.Count; i++)
            {
                sb.Append("- ").Append(items[i]);
                if (i < items.Count - 1) sb.Append('\n');
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Best-effort parse of bulleted text emitted by the streaming prompt. Recognised headings
    /// are case-insensitive; unknown headings are ignored. Always returns a fully-populated
    /// <see cref="EnhancedNote"/> with empty arrays for missing sections, so downstream code does
    /// not need null checks.
    /// </summary>
    public static EnhancedNote ParseFromBulletText(string? text)
    {
        var work = new List<string>();
        var observations = new List<string>();
        var materials = new List<string>();
        var outcome = new List<string>();
        var current = (List<string>?)null;

        if (!string.IsNullOrWhiteSpace(text))
        {
            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r').Trim();
                if (line.Length == 0) continue;

                if (line.EndsWith(':') && !line.StartsWith('-'))
                {
                    var heading = line[..^1].Trim();
                    current = MatchHeading(heading, work, observations, materials, outcome);
                    continue;
                }

                if (current is null) continue;
                if (line.StartsWith("- "))   current.Add(line[2..].Trim());
                else if (line.StartsWith("-")) current.Add(line[1..].Trim());
                else                            current.Add(line);
            }
        }

        return new EnhancedNote(work, observations, materials, outcome);
    }

    private static List<string>? MatchHeading(
        string heading, List<string> work, List<string> obs, List<string> mat, List<string> outcome)
    {
        var h = heading.ToLowerInvariant();
        if (h.StartsWith("work"))                          return work;
        if (h.StartsWith("site"))                          return obs;
        if (h.StartsWith("material") || h.Contains("equipment")) return mat;
        if (h.StartsWith("outcome") || h.Contains("follow")) return outcome;
        return null;
    }
}
