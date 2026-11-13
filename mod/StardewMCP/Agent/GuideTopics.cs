using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace StardewMCP.Agent;

/// <summary>
/// The strategy guide's topics without the game: topic ids and aliases, the summary line each file starts with, and
/// <see cref="GuideBook"/>, which reads assets/guide/*.md (cached by file timestamp) and answers guide(topic).
/// </summary>
public static class GuideTopics
{
    public const string Overview = "overview";

    /// <summary>Topic ids in reading order; files not listed here come after them, alphabetically.</summary>
    public static readonly string[] Order =
    {
        "spring", "summer", "fall", "winter", "year2", "festivals", "money", "community_center",
        "mines", "fishing", "relationships", "progression", "animals"
    };

    /// <summary>Other words for a topic, keyed by <see cref="Key"/> form.</summary>
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["cc"] = "community_center", ["bundles"] = "community_center", ["joja"] = "community_center",
        ["mining"] = "mines", ["combat"] = "mines", ["skullcavern"] = "mines",
        ["gifts"] = "relationships", ["marriage"] = "relationships", ["friendship"] = "relationships",
        ["skills"] = "progression", ["professions"] = "progression", ["tools"] = "progression",
        ["buildings"] = "progression", ["house"] = "progression",
        ["greenhouse"] = "year2", ["late"] = "year2",
        ["crops"] = "money", ["processing"] = "money", ["kegs"] = "money",
        ["spring1"] = "spring"
    };

    /// <summary>A topic word reduced for matching: lower case, no spaces, hyphens, underscores, quotes or ".md".</summary>
    public static string Key(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim().Trim('"', '\'', '`').Trim().ToLowerInvariant();
        if (s.EndsWith(".md")) s = s.Substring(0, s.Length - 3);
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            if (ch != ' ' && ch != '-' && ch != '_') sb.Append(ch);
        return sb.ToString();
    }

    /// <summary>
    /// The topic id among <paramref name="ids"/> that <paramref name="topic"/> names (an id or an alias, any case, with
    /// spaces, hyphens or underscores, singular or plural); null when none does.
    /// </summary>
    public static string? Resolve(string? topic, IEnumerable<string> ids)
    {
        var key = Key(topic);
        if (key.Length == 0) return null;
        var byKey = new Dictionary<string, string>();
        foreach (var id in ids)
            byKey.TryAdd(Key(id), id);

        string? Find(string k)
        {
            if (byKey.TryGetValue(k, out var id)) return id;
            if (Aliases.TryGetValue(k, out var target) && byKey.TryGetValue(Key(target), out id)) return id;
            return null;
        }

        return Find(key)
               ?? Find(key + "s")
               ?? (key.Length > 1 && key.EndsWith("s") ? Find(key.Substring(0, key.Length - 1)) : null);
    }

    /// <summary>
    /// Splits a guide file into its summary (line 1, <c>&lt;!-- summary: ... --&gt;</c>) and the text after it.
    /// A file without that line has an empty summary and keeps all its text.
    /// </summary>
    public static (string Summary, string Body) Split(string text)
    {
        text = (text ?? "").Replace("\r\n", "\n").TrimStart('\uFEFF');
        int nl = text.IndexOf('\n');
        var first = (nl < 0 ? text : text.Substring(0, nl)).Trim();
        if (!first.StartsWith("<!--") || !first.EndsWith("-->"))
            return ("", text.Trim());
        var inner = first.Substring(4, first.Length - 7).Trim();
        if (inner.StartsWith("summary:", StringComparison.OrdinalIgnoreCase))
            inner = inner.Substring("summary:".Length).Trim();
        return (inner, nl < 0 ? "" : text.Substring(nl + 1).Trim());
    }

    /// <summary>Topic ids sorted in reading order (<see cref="Order"/>), then the others alphabetically.</summary>
    public static List<string> Sort(IEnumerable<string> ids) =>
        ids.OrderBy(id => { int i = Array.IndexOf(Order, id); return i < 0 ? Order.Length : i; })
           .ThenBy(id => id, StringComparer.Ordinal)
           .ToList();

    /// <summary>"Topics: spring - summary | summer - summary | ..." (the overview is left out: guide() without a topic is the overview).</summary>
    public static string Index(IEnumerable<(string Id, string Summary)> topics)
    {
        var parts = topics
            .Where(t => !t.Id.Equals(Overview, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Summary.Length == 0 ? t.Id : $"{t.Id} - {t.Summary.TrimEnd('.')}")
            .ToList();
        return parts.Count == 0 ? "Topics: (none)" : "Topics: " + string.Join(" | ", parts);
    }
}

/// <summary>The guide files of one folder, each read again only when its timestamp changes.</summary>
public sealed class GuideBook
{
    private sealed record Entry(DateTime Stamp, string Summary, string Body);

    private readonly string _dir;
    private readonly Dictionary<string, Entry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public GuideBook(string dir) => _dir = dir;

    public string Dir => _dir;

    /// <summary>Topic id (file name without .md) to its summary and text, in reading order.</summary>
    public List<(string Id, string Summary, string Body)> Topics()
    {
        lock (_lock)
        {
            var files = Directory.Exists(_dir) ? Directory.GetFiles(_dir, "*.md") : Array.Empty<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in files)
            {
                var id = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                seen.Add(id);
                var stamp = File.GetLastWriteTimeUtc(path);
                if (_cache.TryGetValue(id, out var old) && old.Stamp == stamp) continue;
                var (summary, body) = GuideTopics.Split(File.ReadAllText(path, Encoding.UTF8));
                _cache[id] = new Entry(stamp, summary, body);
            }
            foreach (var gone in _cache.Keys.Where(k => !seen.Contains(k)).ToList())
                _cache.Remove(gone);
            return GuideTopics.Sort(_cache.Keys).Select(id => (id, _cache[id].Summary, _cache[id].Body)).ToList();
        }
    }

    /// <summary>
    /// guide(topic): no topic (or "overview") gives the overview and the topic index, a known topic or alias gives
    /// that file, anything else fails with the index.
    /// </summary>
    public (bool Ok, string Text) Answer(string? topic)
    {
        var topics = Topics();
        if (topics.Count == 0)
            return (false, "The strategy guide files are missing (assets/guide/*.md next to the mod). Reinstall the mod.");
        var index = GuideTopics.Index(topics.Select(t => (t.Id, t.Summary)));

        var id = string.IsNullOrWhiteSpace(topic) ? GuideTopics.Overview : GuideTopics.Resolve(topic, topics.Select(t => t.Id));
        if (id == null)
            return (false, $"No guide topic \"{topic!.Trim()}\". {index}");

        var body = topics.Where(t => t.Id == id).Select(t => t.Body).FirstOrDefault() ?? "";
        if (id != GuideTopics.Overview)
            return (true, body.Length == 0 ? $"The guide topic \"{id}\" is empty." : body);

        return (true, body.Length == 0 ? index : body + "\n\n" + index);
    }
}
