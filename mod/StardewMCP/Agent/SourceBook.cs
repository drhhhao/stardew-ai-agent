using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewMCP.Agent;

/// <summary>Kinds of item source, in the order <see cref="SourceBook.Lines"/> prints them (most useful first).</summary>
public enum SourceKind { Crop, FruitTree, Animal, Fish, CrabPot, Forage, Machine, Shop, ArtifactSpot, Geode, Monster }

/// <summary>
/// The reverse index behind <see cref="KnowledgeSources.For"/>: facts "item X comes from Y" are added while the game
/// data is read, and <see cref="Lines"/> turns them into one short line per kind of source. Game-free (unit tests).
/// </summary>
public sealed class SourceBook
{
    public const int MaxLines = 8, MaxNames = 6, MaxGroups = 3, MaxTexts = 4;

    private sealed record Place(string Name, int Seasons, string Detail, int BossLevel, int CatchLimit);
    private sealed record Text(string Value, int Seasons, double Rank, int Order);

    private readonly Dictionary<string, Dictionary<SourceKind, List<Place>>> _places = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<SourceKind, List<Text>>> _texts = new(StringComparer.OrdinalIgnoreCase);
    private int _order;

    /// <summary>Items with at least one source.</summary>
    public int Count => _places.Keys.Concat(_texts.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    /// <summary>
    /// The item is found in a place (forage, fish, artifact spot). Places with the same seasons and detail share a group:
    /// "forage: Forest, Mountain (spring)". <paramref name="bossLevel"/> >= 0 marks a legendary fish, shown only when allowed;
    /// <paramref name="catchLimit"/> >= 0 a place that gives it only that many times (left out once the player has them).
    /// </summary>
    public void AddPlace(string qid, SourceKind kind, string place, int seasons = SeasonMask.All, string detail = "", int bossLevel = -1, int catchLimit = -1)
    {
        if (string.IsNullOrWhiteSpace(qid) || string.IsNullOrWhiteSpace(place) || (seasons & SeasonMask.All) == 0) return;
        Get(_places, qid, kind).Add(new Place(place, seasons & SeasonMask.All, detail ?? "", bossLevel, catchLimit));
    }

    /// <summary>The item comes from something named (a crop's seed, a machine, a shop, a monster); higher rank is listed first.</summary>
    public void AddText(string qid, SourceKind kind, string text, int seasons = SeasonMask.All, double rank = 0)
    {
        if (string.IsNullOrWhiteSpace(qid) || string.IsNullOrWhiteSpace(text) || (seasons & SeasonMask.All) == 0) return;
        Get(_texts, qid, kind).Add(new Text(text, seasons & SeasonMask.All, rank, _order++));
    }

    private static List<T> Get<T>(Dictionary<string, Dictionary<SourceKind, List<T>>> map, string qid, SourceKind kind)
    {
        if (!map.TryGetValue(qid, out var byKind)) map[qid] = byKind = new Dictionary<SourceKind, List<T>>();
        if (!byKind.TryGetValue(kind, out var list)) byKind[kind] = list = new List<T>();
        return list;
    }

    /// <summary>
    /// One line per kind of source, most useful kind first, at most <paramref name="maxLines"/>. Legendary places are left
    /// out unless <paramref name="showBoss"/> allows their level, and places with a catch limit once <paramref name="caught"/>
    /// (how many the player has caught) reaches it.
    /// </summary>
    public IReadOnlyList<string> Lines(string qid, Func<int, bool>? showBoss = null, int maxLines = MaxLines, int caught = 0)
    {
        var lines = new List<string>();
        _places.TryGetValue(qid ?? "", out var places);
        _texts.TryGetValue(qid ?? "", out var texts);
        foreach (SourceKind kind in Enum.GetValues(typeof(SourceKind)))
        {
            if (lines.Count >= maxLines) break;
            var parts = new List<string>();
            if (places != null && places.TryGetValue(kind, out var p))
            {
                var shown = p.Where(x => (x.BossLevel < 0 || showBoss == null || showBoss(x.BossLevel)) && !FishRules.LimitReached(x.CatchLimit, caught)).ToList();
                if (shown.Count > 0) parts.Add(PlaceText(shown));
            }
            if (texts != null && texts.TryGetValue(kind, out var t) && t.Count > 0)
                parts.Add(TextText(t, kind == SourceKind.Shop ? MaxTexts + 1 : MaxTexts));
            if (parts.Count > 0) lines.Add($"{Label(kind)}: {string.Join("; ", parts)}");
        }
        return lines;
    }

    public static string Label(SourceKind kind) => kind switch
    {
        SourceKind.Crop => "crop",
        SourceKind.FruitTree => "fruit tree",
        SourceKind.Animal => "animal",
        SourceKind.Fish => "fish",
        SourceKind.CrabPot => "crab pot",
        SourceKind.Forage => "forage",
        SourceKind.Machine => "machine",
        SourceKind.Shop => "shop",
        SourceKind.ArtifactSpot => "artifact spot",
        SourceKind.Geode => "geode",
        SourceKind.Monster => "monster drop",
        _ => kind.ToString()
    };

    private static string PlaceText(List<Place> places)
    {
        var groups = places.GroupBy(x => (x.Seasons, x.Detail)).ToList();
        var shown = groups.Take(MaxGroups).Select(g =>
        {
            var names = g.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var text = Names(names, MaxNames);
            var seasons = SeasonMask.Text(g.Key.Seasons);
            if (seasons.Length > 0) text += $" ({seasons})";
            if (g.Key.Detail.Length > 0) text += ", " + g.Key.Detail;
            return text;
        }).ToList();
        if (groups.Count > MaxGroups) shown.Add($"and {groups.Count - MaxGroups} more");
        return string.Join("; ", shown);
    }

    private static string TextText(List<Text> texts, int max)
    {
        var items = texts.OrderByDescending(x => x.Rank).ThenBy(x => x.Order)
            .Select(x => x.Value + (SeasonMask.Text(x.Seasons) is { Length: > 0 } s ? $" ({s})" : ""))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return Names(items, max);
    }

    /// <summary>"A, B, C" or "A, B, and 3 more".</summary>
    public static string Names(IReadOnlyList<string> names, int max)
    {
        if (names.Count <= max) return string.Join(", ", names);
        return string.Join(", ", names.Take(max)) + $", and {names.Count - max} more";
    }

    // ------------------------------------------------------------------ shops

    /// <summary>Shops that are not a place to buy from (catalogues, recovery, tool upgrades).</summary>
    public static bool SkipShop(string shopId) =>
        shopId.Contains("Catalogue", StringComparison.OrdinalIgnoreCase) || shopId is "AdventureGuildRecovery" or "LostItems" or "ClintUpgrade";

    /// <summary>Readable name of a Data/Shops id.</summary>
    public static string ShopLabel(string shopId)
    {
        switch (shopId)
        {
            case "SeedShop": return "Pierre's";
            case "Joja": return "Joja Mart";
            case "FishShop": return "Willy's";
            case "AnimalShop": return "Marnie's";
            case "Carpenter": return "Robin's";
            case "Blacksmith": return "Clint's";
            case "Saloon": return "the Saloon";
            case "Hospital": return "Harvey's clinic";
            case "Traveler": return "Traveling Cart (Fri/Sun, random stock)";
            case "Sandy": return "Sandy's Oasis";
            case "DesertTrade": return "Desert Trader";
            case "Dwarf": return "the Dwarf";
            case "ShadowShop": return "Krobus";
            case "AdventureShop": return "Adventurer's Guild";
            case "Casino": return "the Casino";
            case "QiGemShop": return "Qi's Walnut Room";
            case "IslandTrade": return "Island Trader";
            case "ResortBar": return "Ginger Island resort bar";
            case "VolcanoShop": return "Volcano Dwarf";
            case "HatMouse": return "Hat Mouse";
            case "IceCreamStand": return "ice cream stand";
            case "BoxOffice": return "movie theater box office";
            case "Concessions": return "movie theater concessions";
            case "Bookseller": return "Bookseller";
            case "BooksellerTrade": return "Bookseller trade";
            case "Raccoon": return "Raccoon";
            case "PetAdoption": return "Marnie's pet adoption";
            case "Festival_StardewValleyFair_StarTokens": return "Stardew Valley Fair (star tokens)";
        }
        if (shopId.StartsWith("Festival_NightMarket")) return "Night Market";
        if (shopId.StartsWith("DesertFestival_")) return "Desert Festival";
        if (shopId.StartsWith("Festival_"))
        {
            var rest = shopId.Substring("Festival_".Length);
            int us = rest.IndexOf('_');
            return FishRules.Words(us > 0 ? rest.Substring(0, us) : rest);
        }
        return FishRules.Words(shopId);
    }

    public static string CurrencyUnit(int currency) => currency switch
    {
        0 => "g",
        1 => " star tokens",
        2 => " Qi coins",
        4 => " Qi gems",
        _ => ""
    };

    // ------------------------------------------------------------------ geodes

    /// <summary>
    /// The items Utility.getTreasureFromGeode (1.6.15) gives from code when a geode's GeodeDropsDefaultItems is set and the
    /// data drop is skipped: Stone, Clay, the geode's crystal (Earth Crystal, Frozen Tear, Fire Quartz; all three for the Omni
    /// Geode) and its ores and coal (Geode: copper, iron, coal; Frozen: + gold; others: + gold, iridium).
    /// </summary>
    public static IReadOnlyList<string> GeodeFallback(string geodeQid)
    {
        var list = new List<string> { "(O)390", "(O)330" };
        switch (geodeQid)
        {
            case "(O)749": list.AddRange(new[] { "(O)82", "(O)84", "(O)86" }); break;
            case "(O)535": list.Add("(O)86"); break;
            case "(O)536": list.Add("(O)84"); break;
            default: list.Add("(O)82"); break;
        }
        switch (geodeQid)
        {
            case "(O)535": list.AddRange(new[] { "(O)378", "(O)380", "(O)382" }); break;
            case "(O)536": list.AddRange(new[] { "(O)378", "(O)380", "(O)382", "(O)384" }); break;
            default: list.AddRange(new[] { "(O)378", "(O)380", "(O)382", "(O)384", "(O)386" }); break;
        }
        return list;
    }

    /// <summary>
    /// What Utility.getTreasureFromGeode (1.6.15) gives from code for a Mystery Box or Golden Mystery Box (fixed ids only:
    /// the random seasonal crop and raccoon seed are left out; ranges are expanded, ids that do not exist are dropped by the
    /// caller). The regular box gives the common table; after 10 boxes opened it may give the rare table instead. The
    /// golden box always gives the rare table, plus the Golden Animal Cracker and the Auto-Petter. Empty for other items.
    /// </summary>
    public static IReadOnlyList<string> MysteryBoxDrops(string boxQid)
    {
        bool golden = boxQid == "(O)GoldenMysteryBox";
        if (!golden && boxQid != "(O)MysteryBox") return Array.Empty<string>();
        var list = new List<string>();
        if (golden) list.AddRange(new[] { "(O)GoldenAnimalCracker", "(BC)272" });
        // rare table (golden always, regular after 10 boxes opened)
        list.AddRange(new[] { "(O)279", "(O)74", "(O)166", "(O)Book_Mystery", "(O)PurpleBook", "(O)797", "(O)373", "(H)MysteryHat", "(S)MysteryShirt", "(WP)MoreWalls:11" });
        list.AddRange(new[] { "(O)288", "(O)253", "(O)687", "(O)695", "(O)242", "(O)204", "(O)369", "(O)466", "(O)773", "(O)688" });
        for (int i = 628; i < 634; i++) list.Add($"(O){i}");
        list.AddRange(new[] { "(W)60", "(O)533", "(O)534", "(O)621", "(O)MysteryBox" });
        for (int i = 0; i < 5; i++) list.Add($"(O)SkillBook_{i}");
        if (!golden)
        {
            // common table
            list.AddRange(new[] { "(O)395", "(O)287" });
            for (int i = 727; i < 734; i++) list.Add($"(O){i}");
            for (int i = 194; i < 240; i++) if (i != 217) list.Add($"(O){i}");
            list.AddRange(new[] { "(O)709", "(O)369", "(O)466", "(O)688", "(O)689", "(O)770", "(O)MixedFlowerSeeds",
                "(O)525", "(O)529", "(O)888", "(O)531", "(O)532", "(O)MysteryBox", "(O)690" });
        }
        return list.Distinct().ToList();
    }

    /// <summary>"id_o_262" (a building conversion tag) -> "(O)262"; null for other tags.</summary>
    public static string? TagItemId(string? tag)
    {
        if (tag == null || !tag.StartsWith("id_", StringComparison.OrdinalIgnoreCase)) return null;
        var rest = tag.Substring(3);
        int us = rest.IndexOf('_');
        if (us <= 0 || us == rest.Length - 1) return null;
        return $"({rest.Substring(0, us).ToUpperInvariant()}){rest.Substring(us + 1)}";
    }

    /// <summary>"category_fruits" -> "fruits", "item_wheat" -> "wheat", "keg_wine" -> "keg wine".</summary>
    public static string TagText(string tag)
    {
        foreach (var prefix in new[] { "category_", "item_" })
            if (tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return tag.Substring(prefix.Length).Replace('_', ' ');
        return tag.Replace('_', ' ');
    }
}
