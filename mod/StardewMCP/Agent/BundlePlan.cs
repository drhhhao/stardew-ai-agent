using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace StardewMCP.Agent;

// Game-free part of the bundles tool: parsing Data/Bundles entries, which item fits which slot, the "have" and
// season notes, and the ship-or-keep split. The game side (BundleReader in CommunityTools.cs, BundleNeeds.cs) reads
// the world state and the items and hands plain values in here.

/// <summary>What one bundle ingredient asks for. <see cref="Id"/> is the bundle data's own id: -1 gold, below -1 an item category, else an item id.</summary>
internal sealed record BundleNeed(int Slot, string Id, int Count, int Quality, bool Done)
{
    /// <summary>The qualified id of the item asked for ("(O)24"); null for gold and categories.</summary>
    public string? ItemId { get; init; }

    public bool IsGold => Id == "-1";

    /// <summary>The object category asked for ("any Fish" is -4), or null for an item or gold.</summary>
    public int? Category => int.TryParse(Id, out var c) && c < -1 ? c : null;
}

/// <summary>One bundle, parsed from <c>BundleData</c> and the donated flags in <c>Bundles</c>.</summary>
internal sealed class BundleInfo
{
    public int Index;
    public string Room = "";
    /// <summary>Position of <see cref="Room"/> in <see cref="BundlePlan.Rooms"/>, or -1 for a room this mod does not drive (the Abandoned Joja Mart).</summary>
    public int Area = -1;
    public string Name = "";
    public string Reward = "";
    /// <summary>How many of <see cref="Needs"/> must be filled (some bundles take 4 of 6).</summary>
    public int Required;
    public List<BundleNeed> Needs = new();

    public int Filled => Needs.Count(n => n.Done);
    public bool Complete => Filled >= Required;
    /// <summary>Slots still to fill before the bundle is complete (fewer than the open ones when the bundle takes N of M).</summary>
    public int Remaining => Math.Max(0, Required - Filled);
    public IEnumerable<BundleNeed> Open => Complete ? Enumerable.Empty<BundleNeed>() : Needs.Where(n => !n.Done);
}

/// <summary>One stack the ship-or-keep check can hold back for a bundle: its quality, size and whether it is in the pack (else a chest).</summary>
internal sealed record KeepStack(int Quality, int Count, bool InPack);

/// <summary>
/// One pack stack in the ship-or-keep check: why to keep it, how many, and the best route if it is not needed. A
/// <paramref name="Supply"/> (seeds, fertilizer, bait, tackle, crafted gear, materials, ores and bars, food) is never called safe to ship.
/// </summary>
internal sealed record KeepRow(string Label, int Stack, IReadOnlyList<string> Tags, int Keep, bool Shippable, string? Route = null, bool Supply = false);

internal static class BundlePlan
{
    /// <summary>The six rooms in the order the game numbers their areas (0-5, CommunityCenter.getAreaNumberFromName).</summary>
    public static readonly string[] Rooms = { "Pantry", "Crafts Room", "Fish Tank", "Boiler Room", "Vault", "Bulletin Board" };

    private static readonly string[] SeasonWords = { "spring", "summer", "fall", "winter" };
    private static readonly Regex SeasonWord = new(@"\b(spring|summer|fall|winter)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// One Data/Bundles entry: key "Room/index", value "name/reward/id count quality .../color/required/texture/label".
    /// <paramref name="flagsOf"/> gives the donated flags of a bundle index; <paramref name="qualify"/> turns an item id into a qualified one.
    /// Null when the entry can't be read.
    /// </summary>
    public static BundleInfo? Parse(string key, string value, Func<int, bool[]> flagsOf, Func<string, string> qualify)
    {
        var keyParts = key.Split('/');
        if (keyParts.Length < 2 || !int.TryParse(keyParts[1], out var index)) return null;
        var f = value.Split('/');
        if (f.Length < 3) return null;

        var info = new BundleInfo
        {
            Index = index,
            Room = keyParts[0],
            Area = Array.IndexOf(Rooms, keyParts[0]),
            Name = f[0],
            Reward = f[1]
        };
        var flags = flagsOf(index);
        var t = f[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i + 2 < t.Length; i += 3)
        {
            int slot = i / 3;
            int.TryParse(t[i + 1], out var count);
            int.TryParse(t[i + 2], out var quality);
            var need = new BundleNeed(slot, t[i], Math.Max(1, count), quality, slot < flags.Length && flags[slot]);
            info.Needs.Add(need.IsGold || need.Category != null ? need : need with { ItemId = qualify(t[i]) });
        }
        info.Required = f.Length > 4 && int.TryParse(f[4], out var req) && req > 0 ? Math.Min(req, info.Needs.Count) : info.Needs.Count;
        return info;
    }

    /// <summary>
    /// True when an item (qualified id and object category) fits the ingredient, quality aside (Bundle.IsValidItemForThisIngredientDescription:
    /// a category slot takes any item of the category, and the egg slot also takes the Dinosaur Egg).
    /// </summary>
    public static bool Accepts(BundleNeed n, string qualifiedItemId, int category)
    {
        if (n.IsGold) return false;
        if (n.Category is int cat) return category == cat || (cat == -5 && qualifiedItemId == "(O)107");
        return qualifiedItemId == (n.ItemId ?? (n.Id.StartsWith("(") ? n.Id : "(O)" + n.Id));
    }

    /// <summary>Accepts, at this quality, on a slot not filled yet.</summary>
    public static bool Fits(BundleNeed n, string qualifiedItemId, int category, int quality) =>
        !n.Done && quality >= n.Quality && Accepts(n, qualifiedItemId, category);

    public static string QualityWords(int quality) => quality switch
    {
        1 => "silver or better",
        2 => "gold or better",
        >= 3 => "iridium",
        _ => "any quality"
    };

    /// <summary>"Quality Crops (Pantry): 5, gold or better (any 3 of 4 still open)" for one open slot.</summary>
    public static string SlotText(BundleInfo b, BundleNeed n)
    {
        int open = b.Open.Count();
        var pick = b.Remaining < open ? $" (any {b.Remaining} of {open} still open)" : "";
        return $"{b.Name} ({b.Room}): {n.Count}, {QualityWords(n.Quality)}{pick}";
    }

    /// <summary>
    /// "have 1 pack + 3 Farm chest (64,15)", "have 0", "have 2 pack (short 3)". Up to <paramref name="maxPlaces"/> storage places
    /// are named (most first), the rest summed up.
    /// </summary>
    public static string HaveText(int need, int pack, IReadOnlyList<(string where, int count)> stored, int maxPlaces = 3)
    {
        var places = stored.Where(s => s.count > 0).OrderByDescending(s => s.count).ToList();
        int total = pack + places.Sum(s => s.count);
        if (total == 0) return "have 0";
        var parts = new List<string>();
        if (pack > 0) parts.Add($"{pack} pack");
        parts.AddRange(places.Take(maxPlaces).Select(s => $"{s.count} {s.where}"));
        if (places.Count > maxPlaces) parts.Add($"{places.Skip(maxPlaces).Sum(s => s.count)} in {places.Count - maxPlaces} more");
        return "have " + string.Join(" + ", parts) + (total < need ? $" (short {need - total})" : "");
    }

    /// <summary>The seasons a source line names ("fish: Beach, spring/summer, 6:00-19:00"); empty when it names none (not seasonal).</summary>
    public static HashSet<int> SeasonsIn(string source) =>
        SeasonWord.Matches(source).Select(m => Array.IndexOf(SeasonWords, m.Value.ToLowerInvariant())).ToHashSet();

    /// <summary>
    /// When every source is seasonal: "season ends in 5 days" (now is the last season it comes in), "last day of its season" (day 28),
    /// or "out of season (summer, fall)"; "" when a source is not seasonal, the next season has it too, or there are no sources.
    /// </summary>
    public static string SeasonNote(IEnumerable<string> sources, string? season, int day)
    {
        int now = CropMath.SeasonIndex(season);
        if (now < 0) return "";
        var union = new HashSet<int>();
        bool any = false;
        foreach (var s in sources)
        {
            any = true;
            var seasons = SeasonsIn(s);
            if (seasons.Count == 0) return "";
            union.UnionWith(seasons);
        }
        if (!any || union.Count == 4) return "";
        if (!union.Contains(now))
            return "out of season (" + string.Join(", ", union.OrderBy(i => (i - now + 4) % 4).Select(i => SeasonWords[i])) + ")";
        if (union.Contains((now + 1) % 4)) return "";
        int left = 28 - day;
        return left <= 0 ? "last day of its season" : $"season ends in {left} day{(left == 1 ? "" : "s")}";
    }

    /// <summary>
    /// Object categories kept for use rather than sold: seeds and saplings (-74), fertilizer (-19), bait (-21), tackle (-22),
    /// crafted gear (-8), floors and paths (-24), building materials (-16), ores, bars and coal (-15) and cooked food (-7).
    /// </summary>
    private static readonly HashSet<int> SupplyCategories = new() { -74, -19, -21, -22, -8, -24, -16, -15, -7 };

    /// <summary>True for an item kept to be used (see <see cref="SupplyCategories"/>), and for any item of type "Crafting" (torches, totems, tonics).</summary>
    public static bool IsSupply(int category, string? type) => SupplyCategories.Contains(category) || type == "Crafting";

    /// <summary>
    /// Which of the open slots to hold items back for. A bundle that takes N of M keeps only its <see cref="BundleInfo.Remaining"/>
    /// best slots: those the chests already cover, then those the pack covers, then the smallest. Returns slot indexes in order.
    /// <paramref name="fits"/>(stack, slot) says whether a stack can fill a slot.
    /// </summary>
    public static List<int> PickSlots(IReadOnlyList<(BundleInfo bundle, BundleNeed need)> slots, IReadOnlyList<KeepStack> stacks, Func<int, int, bool> fits)
    {
        double Cover(int i, bool chestsOnly)
        {
            int count = Math.Max(1, slots[i].need.Count);
            int have = Enumerable.Range(0, stacks.Count).Where(s => (!chestsOnly || !stacks[s].InPack) && fits(s, i)).Sum(s => Math.Max(0, stacks[s].Count));
            return Math.Min(have, count) / (double)count;
        }
        var picked = new List<int>();
        foreach (var g in Enumerable.Range(0, slots.Count).GroupBy(i => slots[i].bundle))
        {
            int remaining = g.Key.Remaining;
            picked.AddRange(remaining >= g.Count()
                ? g
                : g.OrderByDescending(i => Cover(i, true)).ThenByDescending(i => Cover(i, false))
                    .ThenBy(i => slots[i].need.Count).ThenBy(i => i).Take(remaining));
        }
        picked.Sort();
        return picked;
    }

    /// <summary>
    /// Which stacks to hold back for which open slots: the most demanding slots (highest quality) first, each taking chest
    /// stacks before pack stacks and lower quality before higher, so the pack keeps only what the chests do not already cover.
    /// <paramref name="fits"/>(stack, need) says whether a stack can fill a slot. Returns, per stack, (slot index, amount) pairs.
    /// </summary>
    public static List<(int need, int amount)>[] Allocate(IReadOnlyList<BundleNeed> needs, IReadOnlyList<KeepStack> stacks, Func<int, int, bool> fits)
    {
        var result = new List<(int need, int amount)>[stacks.Count];
        var left = new int[stacks.Count];
        for (int s = 0; s < stacks.Count; s++)
        {
            result[s] = new();
            left[s] = Math.Max(0, stacks[s].Count);
        }
        var order = Enumerable.Range(0, needs.Count).OrderByDescending(i => needs[i].Quality).ThenBy(i => i);
        foreach (var n in order)
        {
            int want = needs[n].Count;
            var candidates = Enumerable.Range(0, stacks.Count)
                .Where(s => left[s] > 0 && fits(s, n))
                .OrderBy(s => stacks[s].InPack ? 1 : 0).ThenBy(s => stacks[s].Quality).ThenBy(s => s);
            foreach (var s in candidates)
            {
                if (want <= 0) break;
                int take = Math.Min(want, left[s]);
                left[s] -= take;
                want -= take;
                result[s].Add((n, take));
            }
        }
        return result;
    }

    /// <summary>
    /// The ship-or-keep text: one line per stack with a reason to keep it or a better route, then "Supplies kept: ..." with
    /// what is left of the supplies, then "Safe to ship: ..." with what is left of every other shippable stack.
    /// </summary>
    public static string KeepReport(IEnumerable<string> header, IReadOnlyList<KeepRow> rows)
    {
        var sb = new StringBuilder();
        foreach (var h in header) sb.AppendLine(h);
        if (rows.Count == 0) return sb.Append("Your pack holds nothing to ship or keep.").ToString();
        foreach (var r in rows)
        {
            if (r.Tags.Count == 0 && r.Route == null) continue;
            var parts = new List<string>(r.Tags);
            if (r.Route != null) parts.Add("best: " + r.Route);
            int keep = Math.Min(r.Keep, r.Stack);
            var verdict = keep <= 0 ? "" : keep >= r.Stack ? " | keep all" : r.Supply ? $" | keep {keep} for this" : $" | keep {keep}, ship {r.Stack - keep}";
            sb.AppendLine($"- {r.Label} x{r.Stack}: {string.Join("; ", parts)}{verdict}");
        }
        string Left(KeepRow r) => $"{r.Label} x{r.Stack - Math.Max(0, r.Keep)}";
        var supplies = rows.Where(r => r.Supply && r.Keep < r.Stack).Select(Left).ToList();
        if (supplies.Count > 0) sb.AppendLine("Supplies kept (seeds, gear, materials, food): " + string.Join(", ", supplies) + ".");
        var safe = rows.Where(r => r.Shippable && !r.Supply && r.Keep < r.Stack).Select(Left).ToList();
        sb.Append("Safe to ship: ").Append(safe.Count == 0 ? "nothing" : string.Join(", ", safe)).Append('.');
        return sb.ToString();
    }
}
