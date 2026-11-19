using Warhammer.Models.Campaign;

namespace Warhammer.Helpers;

public enum GmNoteView { Session, All, Inbox }

// How Table Mode finds notes: search text, tag chips, and the Session / All / Inbox tabs.
public static class GmNoteQuery
{
    public static IReadOnlyList<string> ParseTags(string? tags) =>
        (tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Every tag in use, lower-cased, with how many notes carry it, sorted by name.
    public static IReadOnlyList<(string Tag, int Count)> TagCounts(IEnumerable<GmNote> notes) =>
        notes.SelectMany(n => ParseTags(n.Tags).Select(t => t.ToLowerInvariant()).Distinct())
             .GroupBy(t => t)
             .OrderBy(g => g.Key, StringComparer.Ordinal)
             .Select(g => (g.Key, g.Count()))
             .ToList();

    public static bool IsFiltering(string? query, IReadOnlyCollection<string> tags) =>
        !string.IsNullOrWhiteSpace(query) || tags.Count > 0;

    // A search or a tag filter looks across every note, inbox included; otherwise the tab decides.
    public static IReadOnlyList<GmNote> Find(
        IEnumerable<GmNote> notes, IEnumerable<Node> nodes,
        string? query, IReadOnlyCollection<string> tags, GmNoteView view)
    {
        if (IsFiltering(query, tags))
        {
            var names = nodes.ToDictionary(n => n.Id, n => n.Name);
            return notes.Where(n => Matches(n, names, query, tags)).ToList();
        }
        return view switch
        {
            GmNoteView.Session => notes.Where(n => n.IsStarred).ToList(),
            GmNoteView.Inbox   => notes.Where(n => n.IsInbox).OrderByDescending(n => n.CreatedAt).ToList(),
            _                  => notes.Where(n => !n.IsInbox).OrderByDescending(n => n.CreatedAt).ToList(),
        };
    }

    public static bool Matches(GmNote note, IReadOnlyDictionary<string, string> nodeNames,
                               string? query, IReadOnlyCollection<string> tags)
    {
        var noteTags = ParseTags(note.Tags);
        if (!tags.All(t => noteTags.Contains(t, StringComparer.OrdinalIgnoreCase))) return false;
        if (string.IsNullOrWhiteSpace(query)) return true;

        var place = note.NodeId is not null && nodeNames.TryGetValue(note.NodeId, out var name) ? name : "";
        var q = query.Trim();
        return new[] { note.Title, note.Summary, note.Content, note.Secret, note.Tags, place }
            .Any(f => f?.Contains(q, StringComparison.OrdinalIgnoreCase) == true);
    }

    // Splits text into runs, flagging the ones that match the query, for <mark> highlighting.
    public static IReadOnlyList<(string Text, bool Match)> Highlight(string? text, string? query)
    {
        text ??= "";
        var q = query?.Trim() ?? "";
        if (q.Length == 0 || text.Length == 0) return [(text, false)];

        var runs = new List<(string, bool)>();
        var at = 0;
        while (at < text.Length)
        {
            var hit = text.IndexOf(q, at, StringComparison.OrdinalIgnoreCase);
            if (hit < 0) { runs.Add((text[at..], false)); break; }
            if (hit > at) runs.Add((text[at..hit], false));
            runs.Add((text.Substring(hit, q.Length), true));
            at = hit + q.Length;
        }
        return runs;
    }
}
