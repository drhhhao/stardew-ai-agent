using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewMCP.Agent;

/// <summary>One giftable object with one villager's taste for it (the game's code), as the gift-taste text sees it.</summary>
/// <param name="Name">Name the farmer reads (display name).</param>
/// <param name="Group">Plural group word from <see cref="GiftTasteMath.GroupOf"/>, or "" when the object belongs to no group.</param>
/// <param name="Taste">The game's taste code: 0 love, 2 like, 4 dislike, 6 hate, 7 special (Stardrop Tea), 8 neutral.</param>
/// <param name="Have">The farmer has one in the pack or a chest.</param>
public sealed record GiftItem(string Name, string Group, int Taste, bool Have = false);

/// <summary>
/// Text for gift tastes in villagers() (PLAN B7): which objects belong in the lists, folding whole categories ("all fish")
/// so a line stays short, and who loves or hates one item. No game types: the taste of each item comes from the game's own
/// NPC.getGiftTasteForThisItem in <see cref="NpcInfo"/>.
/// </summary>
public static class GiftTasteMath
{
    public const int Love = 0, Like = 2, Dislike = 4, Hate = 6, Special = 7, Neutral = 8;

    /// <summary>The category the game gives trinkets ((TR) items, Data/Trinkets): they are gifts too ("category_trinket" in the tastes).</summary>
    public const int TrinketCategory = -101;

    /// <summary>A group is folded into one entry when it has at least this many items...</summary>
    public const int MinGroup = 4;

    /// <summary>...and at least this share of them has the same taste.</summary>
    public const double GroupShare = 0.75;

    /// <summary>Exceptions named after "most X" when there are this many or fewer.</summary>
    public const int MaxExceptions = 5;

    /// <summary>Groups whose kept items are named after the group ("[have: ...]").</summary>
    public const int MaxHaveNames = 3;

    /// <summary>Taste codes in the order the lines are written, with their labels.</summary>
    public static readonly (int Code, string Label)[] Lines = { (Love, "Loves"), (Like, "Likes"), (Dislike, "Dislikes"), (Hate, "Hates") };

    /// <summary>Friendship points of one gift by taste, with NPC.receiveGift's quality and birthday multipliers.</summary>
    public const string PointsNote = "A normal-quality gift gives love +80, like +45, neutral +20, dislike -20, hate -40 friendship points; silver/gold/iridium multiply loves and likes by 1.1/1.25/1.5; 8x on their birthday.";

    /// <summary>The game's code folded into the five tastes: Stardrop Tea's special 7 is a love (+250), unknown codes are neutral.</summary>
    public static int Normalize(int code) => code switch { Special => Love, Love or Like or Dislike or Hate => code, _ => Neutral };

    public static string Word(int code) => Normalize(code) switch { Love => "loved", Like => "liked", Dislike => "disliked", Hate => "hated", _ => "neutral" };

    /// <summary>Plural group word for an object's category (Objects.json Category / Type); "" for objects in no useful group.</summary>
    public static string GroupOf(int category, string? type)
    {
        if (string.Equals(type, "Arch", StringComparison.Ordinal)) return "artifacts";
        return category switch
        {
            -2 => "gems",
            -4 => "fish",
            -5 => "eggs",
            -6 => "milk",
            -7 => "cooked dishes",
            -8 => "crafted items",
            -12 => "minerals",
            -15 => "ores and bars",
            -16 => "building materials",
            -18 => "animal goods",
            -19 => "fertilizers",
            -20 => "trash",
            -21 => "bait",
            -22 => "tackle",
            -23 => "sea goods",
            -24 => "floors and paths",
            -26 => "artisan goods",
            -27 => "tree syrups",
            -28 => "monster loot",
            -74 => "seeds and saplings",
            -75 => "vegetables",
            -79 => "fruit",
            -80 => "flowers",
            -81 => "forage",
            -102 => "books",
            -103 => "skill books",
            TrinketCategory => "trinkets",
            _ => ""
        };
    }

    /// <summary>Objects that are not gifts a farmer can carry around: quest and story items, map markers, things used up on pickup.</summary>
    private static readonly HashSet<string> NotGifts = new(StringComparer.OrdinalIgnoreCase)
    {
        "73",          // Golden Walnut (a counter, not an item)
        "434",         // Stardrop (eaten when picked up)
        "458", "460",  // Bouquet, Mermaid's Pendant (romance items, not gifts)
        "277",         // Wilted Bouquet (ends dating)
        "809",         // Movie Ticket (an invitation, not a gift)
        "858",         // Qi Gem (a currency)
        "922", "923", "924", // supply crates
        "930",         // ??? (unused)
        "GoldCoin", "PetLicense", "StardropTea"
    };

    /// <summary>
    /// What the game does instead when a farmer hands over an object it never takes as a gift (NPC.tryToReceiveActiveObject
    /// handles these before the gift code), or null for an ordinary item. Bouquet and Mermaid's Pendant are NpcInfo.SpecialGifts.
    /// </summary>
    /// <param name="qualifiedId">Qualified item id, e.g. "(O)809".</param>
    public static string? NotAGiftUse(string qualifiedId) => qualifiedId switch
    {
        "(O)277" => "giving it to someone you date ends the relationship",
        "(O)809" => "giving it invites them to the movie theater",
        "(O)897" => "given to Pierre it unlocks his stock list",
        _ => null
    };

    /// <summary>Object types that never belong in a taste list (Objects.json Type).</summary>
    private static readonly HashSet<string> NotGiftTypes = new(StringComparer.OrdinalIgnoreCase) { "Litter", "Quest", "interactive", "Ring" };

    /// <summary>Whether an object belongs in a taste list: a concrete item a farmer can own and give.</summary>
    /// <param name="id">Unqualified object id (Data/Objects key).</param>
    /// <param name="name">Display name; unreadable names (with "_" or empty) are left out.</param>
    public static bool Listable(string id, string name, string? type, int category, bool canBeGivenAsGift)
    {
        if (!canBeGivenAsGift || category == -999 || NotGifts.Contains(id)) return false;
        if (type != null && NotGiftTypes.Contains(type)) return false;
        // "asdf" objects are secret notes, map spots and the like, except books (-102, -103).
        if (string.Equals(type, "asdf", StringComparison.OrdinalIgnoreCase) && category != -102 && category != -103) return false;
        return !string.IsNullOrWhiteSpace(name) && !name.Contains('_') && !name.StartsWith("Error", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// "Loves: ... | Likes: ... | Dislikes: ... | Hates: ..." for one villager. A group of at least <see cref="MinGroup"/> items
    /// where <see cref="GroupShare"/> share the same taste becomes one entry ("all fish", or "most fish (not Carp)"); the
    /// other items of the group still appear by name under their own taste. Items the farmer has come first, marked [have].
    /// </summary>
    /// <param name="maxNamed">Named loves and likes per line before "+N more" (group entries are always shown).</param>
    /// <param name="maxNamedBad">Named dislikes and hates per line: most are odds and ends nobody carries, and kept items come first anyway.</param>
    public static string Summary(IEnumerable<GiftItem> items, int maxNamed = 30, int maxNamedBad = 12)
    {
        var all = items
            .Select(i => i with { Taste = Normalize(i.Taste) })
            .GroupBy(i => (i.Name, i.Taste))
            .Select(g => g.First() with { Have = g.Any(x => x.Have) })
            .ToList();

        var folded = new HashSet<GiftItem>();
        var groupEntries = new Dictionary<int, List<string>>();
        foreach (var g in all.Where(i => i.Group.Length > 0).GroupBy(i => i.Group).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var members = g.ToList();
            if (members.Count < MinGroup) continue;
            var top = members.GroupBy(m => m.Taste).OrderByDescending(t => t.Count()).ThenBy(t => t.Key).First();
            if (top.Key == Neutral || top.Count() < GroupShare * members.Count) continue;
            var others = members.Where(m => m.Taste != top.Key).Select(m => m.Name).Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            var entry = others.Count == 0 ? $"all {g.Key}"
                : others.Count <= MaxExceptions ? $"most {g.Key} (not {string.Join(", ", others)})"
                : $"most {g.Key}";
            var have = top.Where(m => m.Have).Select(m => m.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            if (have.Count > 0)
                entry += $" [have: {string.Join(", ", have.Take(MaxHaveNames))}{(have.Count > MaxHaveNames ? $" +{have.Count - MaxHaveNames}" : "")}]";
            if (!groupEntries.TryGetValue(top.Key, out var list)) groupEntries[top.Key] = list = new List<string>();
            list.Add(entry);
            foreach (var m in top) folded.Add(m);
        }

        var parts = new List<string>();
        foreach (var (code, label) in Lines)
        {
            int max = code is Love or Like ? maxNamed : maxNamedBad;
            var named = all.Where(i => i.Taste == code && !folded.Contains(i))
                .OrderByDescending(i => i.Have).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .Select(i => i.Have ? i.Name + " [have]" : i.Name).ToList();
            var entries = named.Take(max).ToList();
            if (named.Count > max) entries.Add($"+{named.Count - max} more");
            if (groupEntries.TryGetValue(code, out var groups)) entries.AddRange(groups);
            parts.Add($"{label}: {(entries.Count == 0 ? "none" : string.Join(", ", entries))}");
        }
        return string.Join(" | ", parts);
    }

    /// <summary>
    /// Who loves, likes, dislikes and hates one item: "loved by A, B | liked by C | ... | everyone else is neutral", names
    /// sorted. <paramref name="complete"/> says the list covers every villager (game data); otherwise it is what the farmer
    /// found out, neutral ones named, and the rest is unknown.
    /// </summary>
    public static string Who(IEnumerable<(string Villager, int Taste)> tastes, bool complete)
    {
        var list = tastes.Select(t => (t.Villager, Taste: Normalize(t.Taste))).ToList();
        var parts = new List<string>();
        foreach (var (code, _) in Lines)
        {
            var names = list.Where(t => t.Taste == code).Select(t => t.Villager).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            if (names.Count > 0) parts.Add($"{Word(code)} by {string.Join(", ", names)}");
        }
        var neutral = list.Where(t => t.Taste == Neutral).Select(t => t.Villager).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        if (complete)
        {
            if (parts.Count == 0) return "everyone is neutral about it";
            if (neutral.Count > 0) parts.Add("everyone else is neutral");
        }
        else if (neutral.Count > 0)
            parts.Add($"neutral for {string.Join(", ", neutral)}");
        return string.Join(" | ", parts);
    }

    /// <summary>
    /// Index of the entry the farmer meant by <paramref name="query"/>: an exact shown name, English name or id (any case,
    /// qualified like "(TR)FrogEgg" or not) first, then the shortest name containing it. -1 when nothing fits.
    /// </summary>
    /// <param name="entries">Items by qualified id ("(O)72", "(TR)FrogEgg"), shown name and internal name.</param>
    public static int Match(IReadOnlyList<(string Id, string Display, string Name)> entries, string query)
    {
        var q = query.Trim();
        if (q.Length == 0) return -1;
        bool qualified = q.StartsWith('(');
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.Display.Equals(q, StringComparison.OrdinalIgnoreCase) || e.Name.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                (qualified ? e.Id : Bare(e.Id)).Equals(q, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        int best = -1;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (!e.Display.Contains(q, StringComparison.OrdinalIgnoreCase) && !e.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
            if (best < 0 || e.Display.Length < entries[best].Display.Length) best = i;
        }
        return best;
    }

    /// <summary>An item id without its "(O)" / "(TR)" type prefix.</summary>
    public static string Bare(string id)
    {
        if (!id.StartsWith('(')) return id;
        int end = id.IndexOf(')');
        return end < 0 ? id : id[(end + 1)..];
    }
}
