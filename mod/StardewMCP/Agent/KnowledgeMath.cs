using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace StardewMCP.Agent;

/// <summary>What a crop can do if planted on a given day. Produced by <see cref="CropMath.Plan"/>.</summary>
/// <param name="InSeason">The crop grows in the season it is planted in.</param>
/// <param name="DaysAvailable">Growing days left from the planting day to the end of the last season the crop grows in.</param>
/// <param name="Matures">The crop is ready before it would die at a season change.</param>
/// <param name="LastPlantDay">Latest day of the starting season it can be planted and still mature (28 or more: any day).</param>
/// <param name="Harvests">Harvests from one planting (0 when it will not mature).</param>
/// <param name="Plantings">Back-to-back plantings that fit (always 1 for crops that regrow).</param>
/// <param name="TileDays">Days one tile is busy with these plantings.</param>
public sealed record CropPlan(bool InSeason, int DaysAvailable, bool Matures, int LastPlantDay, int Harvests, int Plantings, int TileDays);

/// <summary>
/// Planning math for the crops tool. No game types, so it can be tested without the game:
/// runner/tests/test_crop_math.py is a line-for-line Python port; keep the two in step.
/// </summary>
public static class CropMath
{
    public const int DaysPerSeason = 28;
    private static readonly string[] SeasonNames = { "spring", "summer", "fall", "winter" };

    /// <summary>0 spring, 1 summer, 2 fall, 3 winter; -1 when the name is not a season.</summary>
    public static int SeasonIndex(string? name)
    {
        switch (name?.Trim().ToLowerInvariant())
        {
            case "spring": return 0;
            case "summer": return 1;
            case "fall":
            case "autumn": return 2;
            case "winter": return 3;
            default: return -1;
        }
    }

    public static string SeasonName(int index) => SeasonNames[((index % 4) + 4) % 4];

    /// <summary>Days from planting to the first harvest: the sum of the crop's growth phases.</summary>
    public static int GrowthDays(IEnumerable<int> daysInPhase)
    {
        int total = 0;
        foreach (var d in daysInPhase)
            if (d > 0) total += d;
        return total;
    }

    /// <summary>
    /// Growing days a crop planted on <paramref name="startDay"/> of <paramref name="season"/> has before it dies.
    /// A crop planted and watered on day d is ready on day d + growth, so it must be ready by day 28.
    /// It keeps growing through the following seasons it also grows in (corn: summer and fall), never past winter.
    /// </summary>
    public static int DaysAvailable(int season, int startDay, IReadOnlyList<bool> growsIn)
    {
        if (season < 0 || season > 3 || growsIn.Count < 4 || !growsIn[season]) return 0;
        int days = DaysPerSeason - startDay;
        for (int s = season + 1; s < 4 && growsIn[s]; s++) days += DaysPerSeason;
        return Math.Max(days, 0);
    }

    /// <summary>Plan one crop. <paramref name="regrowDays"/> of 0 or less means it is harvested once and gone.</summary>
    public static CropPlan Plan(int season, int startDay, IReadOnlyList<bool> growsIn, int growthDays, int regrowDays)
    {
        int avail = DaysAvailable(season, startDay, growsIn);
        bool inSeason = season >= 0 && season <= 3 && growsIn.Count >= 4 && growsIn[season];
        if (!inSeason || growthDays <= 0)
            return new CropPlan(inSeason, avail, false, 0, 0, 0, 0);

        int lastPlant = startDay + (avail - growthDays); // planting later than this day is too late
        if (growthDays > avail)
            return new CropPlan(true, avail, false, lastPlant, 0, 0, 0);

        if (regrowDays > 0)
        {
            int harvests = 1 + (avail - growthDays) / regrowDays;
            return new CropPlan(true, avail, true, lastPlant, harvests, 1, growthDays + (harvests - 1) * regrowDays);
        }
        int plantings = avail / growthDays;
        return new CropPlan(true, avail, true, lastPlant, 1, plantings, plantings * growthDays);
    }

    /// <summary>The (season, day) that is <paramref name="days"/> days after day <paramref name="day"/> of <paramref name="season"/>.
    /// A season index of 4 or more means the next year.</summary>
    public static (int season, int day) Advance(int season, int day, int days)
    {
        int total = Math.Max(day + days, 1);
        return (season + (total - 1) / DaysPerSeason, (total - 1) % DaysPerSeason + 1);
    }

    /// <summary>Items per harvest: the average of the stack range plus the chance of an extra one.</summary>
    public static double ExpectedYield(int minStack, int maxStack, double extraChance)
    {
        int lo = Math.Max(1, minStack), hi = Math.Max(lo, maxStack);
        return (lo + hi) / 2.0 + Math.Max(0.0, extraChance);
    }

    /// <summary>Gold made on one tile with the plan, after paying for the seeds of every planting.</summary>
    public static double NetGold(CropPlan plan, double yieldPerHarvest, int sellPrice, int seedCost) =>
        plan.Plantings * (plan.Harvests * yieldPerHarvest * sellPrice - seedCost);

    public static double GoldPerTilePerDay(CropPlan plan, double net) => plan.TileDays > 0 ? net / plan.TileDays : 0.0;

    /// <summary>Sell price at a quality (0 normal, 1 silver, 2 gold, 4 iridium): the base price times 1 + 0.25 per quality step.</summary>
    public static int PriceAtQuality(int basePrice, int quality) => (int)(basePrice * (1f + quality * 0.25f));

    /// <summary>Price in a shop's item list. Without a fixed price the game sells an object for twice its base price.</summary>
    public static (int price, bool estimated) ShopPrice(int listedPrice, int objectBasePrice) =>
        listedPrice > 0 ? (listedPrice, false) : (Math.Max(objectBasePrice, 0) * 2, true);

    /// <summary>Qualified form of an item id: object ids such as "24" become "(O)24"; ids that carry a type already stay as they are.</summary>
    public static string Qualify(string? id)
    {
        id = (id ?? "").Trim();
        return id.StartsWith("(") ? id : "(O)" + id;
    }

    /// <summary>The bare object id: "(O)24" and "24" both give "24". Other item types keep their type prefix.</summary>
    public static string Unqualify(string? id)
    {
        id = (id ?? "").Trim();
        return id.StartsWith("(O)", StringComparison.OrdinalIgnoreCase) ? id.Substring(3) : id;
    }

    public static bool SameItem(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
        string.Equals(Qualify(a), Qualify(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Growth speed-up of a fertilizer argument (HoeDirt.GetFertilizerSpeedBoost): speed_gro 0.1, deluxe_speed_gro 0.25,
    /// hyper_speed_gro 0.33; 0 for none or empty; -1 when the name is not a speed fertilizer. Spaces, dashes and case are ignored.
    /// </summary>
    public static float FertilizerBoost(string? name)
    {
        var letters = new StringBuilder();
        foreach (var ch in name ?? "")
            if (char.IsLetter(ch)) letters.Append(char.ToLowerInvariant(ch));
        switch (letters.ToString())
        {
            case "":
            case "none": return 0f;
            case "speedgro":
            case "speed": return 0.1f;
            case "deluxespeedgro":
            case "deluxespeed": return 0.25f;
            case "hyperspeedgro":
            case "hyperspeed": return 0.33f;
            default: return -1f;
        }
    }

    /// <summary>The boost HoeDirt applies at planting: the fertilizer's, plus 0.1 for the Agriculturist profession (the same float sum as the game).</summary>
    public static float GrowthBoost(float fertilizer, bool agriculturist)
    {
        float boost = Math.Max(fertilizer, 0f);
        if (agriculturist) boost += 0.1f;
        return boost;
    }

    /// <summary>
    /// Port of HoeDirt.applySpeedIncreases: the crop's phases after a growth boost. The cut is ceil(total days * boost);
    /// up to three passes take one day off each phase in turn, never taking the first phase below 1 day.
    /// </summary>
    public static int[] SpeedUpPhases(IReadOnlyList<int> daysInPhase, float boost)
    {
        if (boost <= 0f) return daysInPhase.ToArray();
        var phases = new List<int>(daysInPhase) { 99999 }; // Crop.ResetPhaseDays adds the final "fully grown" phase
        int total = 0;
        for (int i = 0; i < phases.Count - 1; i++) total += phases[i];
        int cut = (int)Math.Ceiling((float)total * boost);
        int pass = 0;
        while (cut > 0 && pass < 3)
        {
            for (int j = 0; j < phases.Count; j++)
            {
                if ((j > 0 || phases[j] > 1) && phases[j] != 99999 && phases[j] > 0)
                {
                    phases[j]--;
                    cut--;
                }
                if (cut <= 0) break;
            }
            pass++;
        }
        phases.RemoveAt(phases.Count - 1);
        return phases.ToArray();
    }

    /// <summary>Days a fruit tree bears in a year: 28 per season it fruits in, every day of the year in the Greenhouse.</summary>
    public static int FruitDaysPerYear(int seasonCount, bool greenhouse) =>
        greenhouse ? 4 * DaysPerSeason : Math.Clamp(seasonCount, 0, 4) * DaysPerSeason;

    /// <summary>Days a planted sapling needs before it bears fruit (FruitTree.daysUntilMature starts at 28).</summary>
    public const int SaplingDays = 28;
}

/// <summary>One term of a game state query as written in Data/Shops conditions ("!YEAR 2" is a negated YEAR with args ["2"]).</summary>
public sealed record ConditionTerm(bool Negated, string Key, IReadOnlyList<string> Args);

/// <summary>
/// Reads shop conditions (game state queries) without the game: which seasons an offer is sold in and a short label
/// ("year 2+, Mon/Wed"). Whether the other parts hold now is asked of the game (GameStateQuery, ignoring <see cref="TimeKeys"/>).
/// </summary>
public static class ShopConditions
{
    /// <summary>Query keys that say when, not whether: the planner reads them itself, so the unlock check ignores them.</summary>
    public static readonly string[] TimeKeys =
        { "SEASON", "LOCATION_SEASON", "DAY_OF_WEEK", "DAY_OF_MONTH", "SEASON_DAY", "RANDOM", "SYNCED_RANDOM", "SYNCED_CHOICE", "SYNCED_SUMMER_RAIN_RANDOM" };

    private static readonly string[] Skills = { "FARMING", "FISHING", "FORAGING", "MINING", "COMBAT", "LUCK" };

    /// <summary>Splits a condition at top-level commas (quotes respected), then each part at spaces.</summary>
    public static List<ConditionTerm> Parse(string? condition)
    {
        var terms = new List<ConditionTerm>();
        if (string.IsNullOrWhiteSpace(condition)) return terms;
        foreach (var part in SplitQuoted(condition, ','))
        {
            var words = SplitQuoted(part, ' ');
            if (words.Count == 0) continue;
            string key = words[0];
            bool negated = key.StartsWith("!");
            if (negated) key = key.Substring(1);
            terms.Add(new ConditionTerm(negated, key.ToUpperInvariant(), words.Skip(1).Select(w => w.Trim('"')).ToList()));
        }
        return terms;
    }

    private static List<string> SplitQuoted(string text, char separator)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;
        foreach (var ch in text)
        {
            if (ch == '"') quoted = !quoted;
            if (ch == separator && !quoted)
            {
                if (current.ToString().Trim().Length > 0) parts.Add(current.ToString().Trim());
                current.Clear();
            }
            else current.Append(ch);
        }
        if (current.ToString().Trim().Length > 0) parts.Add(current.ToString().Trim());
        return parts;
    }

    /// <summary>Seasons the offer is sold in (index 0 spring .. 3 winter), or null when the condition does not limit the season.</summary>
    public static bool[]? Seasons(IEnumerable<ConditionTerm> terms)
    {
        bool[]? result = null;
        foreach (var t in terms)
        {
            if (t.Key != "SEASON" && t.Key != "LOCATION_SEASON") continue;
            var listed = new bool[4];
            foreach (var a in t.Key == "LOCATION_SEASON" ? t.Args.Skip(1) : t.Args)
            {
                int i = CropMath.SeasonIndex(a);
                if (i >= 0) listed[i] = true;
            }
            result ??= new[] { true, true, true, true };
            for (int i = 0; i < 4; i++) result[i] &= t.Negated ? !listed[i] : listed[i];
        }
        return result;
    }

    /// <summary>Short label of a condition, e.g. "spring, year 2+" or "Fishing 6+"; empty when there is no condition.</summary>
    public static string Label(IEnumerable<ConditionTerm> terms, bool includeSeasons)
    {
        var list = terms.ToList();
        var parts = new List<string>();
        var seasons = Seasons(list);
        if (includeSeasons && seasons != null)
            parts.Add(seasons.Any(x => x) ? string.Join("/", Enumerable.Range(0, 4).Where(i => seasons[i]).Select(CropMath.SeasonName)) : "never");
        foreach (var t in list)
        {
            var label = TermLabel(t);
            if (label.Length > 0 && !parts.Contains(label)) parts.Add(label);
        }
        return string.Join(", ", parts);
    }

    private static string TermLabel(ConditionTerm t)
    {
        string Arg(int i) => i < t.Args.Count ? t.Args[i] : "";
        int.TryParse(Arg(0), NumberStyles.Integer, CultureInfo.InvariantCulture, out int first);
        switch (t.Key)
        {
            case "SEASON":
            case "LOCATION_SEASON":
                return "";
            case "YEAR":
                if (t.Negated) return first <= 2 ? "year 1 only" : $"before year {first}";
                return t.Args.Count > 1 ? $"years {Arg(0)}-{Arg(1)}" : $"year {first}+";
            case "DAY_OF_WEEK":
                return (t.Negated ? "not " : "") + string.Join("/", t.Args.Select(d => d.Length > 3 ? d.Substring(0, 3) : d));
            case "DAY_OF_MONTH":
                return (t.Negated ? "not on day " : "day ") + string.Join("/", t.Args);
            case "RANDOM":
            case "SYNCED_RANDOM":
            case "SYNCED_CHOICE":
            case "SYNCED_SUMMER_RAIN_RANDOM":
                return "sometimes";
            case "MINE_LOWEST_LEVEL_REACHED":
                return t.Negated ? $"before mine floor {Arg(0)}" : $"after mine floor {Arg(0)}";
            case "PLAYER_HAS_MAIL":
                return (t.Negated ? "until letter " : "after letter ") + Arg(1);
            case "PLAYER_HAS_SEEN_EVENT":
                return (t.Negated ? "until event " : "after event ") + Arg(1);
            case "PLAYER_HAS_CRAFTING_RECIPE":
            case "PLAYER_HAS_COOKING_RECIPE":
                return (t.Negated ? "if you lack the recipe " : "if you know the recipe ") + string.Join(" ", t.Args.Skip(1));
            case "IS_MULTIPLAYER":
                return t.Negated ? "single player only" : "multiplayer only";
            case "PLAYER_HAS_TRASH_CAN_LEVEL": // player min [max]
            {
                int.TryParse(Arg(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int min);
                bool hasMax = int.TryParse(Arg(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out int max);
                string level = hasMax && max == min ? ItemFacts.ToolLevelName(min) : hasMax ? $"{ItemFacts.ToolLevelName(min)} to {ItemFacts.ToolLevelName(max)}" : $"{ItemFacts.ToolLevelName(min)} or better";
                return (t.Negated ? "unless your trash can is " : "when your trash can is ") + level;
            }
        }
        foreach (var skill in Skills)
            if (t.Key == $"PLAYER_{skill}_LEVEL" || t.Key == $"PLAYER_BASE_{skill}_LEVEL")
            {
                string name = skill.Substring(0, 1) + skill.Substring(1).ToLowerInvariant();
                return t.Negated ? $"below {name} {Arg(1)}" : $"{name} {Arg(1)}+";
            }
        return "if " + (t.Negated ? "not " : "") + t.Key + (t.Args.Count > 0 ? " " + string.Join(" ", t.Args) : "");
    }
}

/// <summary>Readable names of Data/Shops ids, with the festival days for festival stalls.</summary>
public static class ShopLabels
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SeedShop"] = "Pierre's (SeedShop)",
        ["Joja"] = "Joja Mart (Joja)",
        ["Sandy"] = "Oasis (Sandy)",
        ["Traveler"] = "Traveling Cart (Fri/Sun)",
        ["AnimalShop"] = "Marnie's Ranch",
        ["Blacksmith"] = "Clint's Blacksmith",
        ["ClintUpgrade"] = "Clint's tool upgrades",
        ["Carpenter"] = "Robin's Carpenter Shop",
        ["FishShop"] = "Willy's Fish Shop",
        ["Saloon"] = "Stardrop Saloon",
        ["Hospital"] = "Harvey's Clinic",
        ["AdventureShop"] = "Adventurer's Guild",
        ["AdventureGuildRecovery"] = "Adventurer's Guild item recovery",
        ["Dwarf"] = "Dwarf (Mines)",
        ["VolcanoShop"] = "Volcano Dwarf",
        ["ShadowShop"] = "Krobus (Sewers)",
        ["DesertTrade"] = "Desert Trader",
        ["IslandTrade"] = "Island Trader",
        ["ResortBar"] = "Ginger Island resort bar",
        ["QiGemShop"] = "Qi's Walnut Room",
        ["Casino"] = "Casino",
        ["HatMouse"] = "Hat Mouse",
        ["Bookseller"] = "Bookseller",
        ["BooksellerTrade"] = "Bookseller trades",
        ["Raccoon"] = "Raccoon (Giant Stump)",
        ["IceCreamStand"] = "Ice cream stand",
        ["BoxOffice"] = "Movie Theater box office",
        ["Concessions"] = "Movie Theater concessions",
        ["LostItems"] = "Lost and Found",
        ["PetAdoption"] = "Marnie's pet adoption"
    };

    /// <summary>When a festival stall is open: its Data/Shops entries have no SEASON condition, the festival date says it.</summary>
    /// <param name="Season">0 spring .. 3 winter.</param>
    public sealed record FestivalWindow(string Name, int Season, int FirstDay, int LastDay);

    private static readonly Dictionary<string, FestivalWindow> Festivals = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Festival_EggFestival_Pierre"] = new("Egg Festival", 0, 13, 13),
        ["Festival_FlowerDance_Pierre"] = new("Flower Dance", 0, 24, 24),
        ["Festival_Luau_Pierre"] = new("Luau", 1, 11, 11),
        ["Festival_DanceOfTheMoonlightJellies_Pierre"] = new("Moonlight Jellies", 1, 28, 28),
        ["Festival_StardewValleyFair_StarTokens"] = new("Stardew Valley Fair", 2, 16, 16),
        ["Festival_SpiritsEve_Pierre"] = new("Spirit's Eve", 2, 27, 27),
        ["Festival_FestivalOfIce_TravelingMerchant"] = new("Festival of Ice", 3, 8, 8),
        ["Festival_NightMarket_MagicBoat_Day1"] = new("Night Market", 3, 15, 15),
        ["Festival_NightMarket_MagicBoat_Day2"] = new("Night Market", 3, 16, 16),
        ["Festival_NightMarket_MagicBoat_Day3"] = new("Night Market", 3, 17, 17),
        ["Festival_NightMarket_DecorationBoat"] = new("Night Market", 3, 15, 17),
        ["Festival_FeastOfTheWinterStar_Pierre"] = new("Feast of the Winter Star", 3, 25, 25),
        ["DesertFestival_EggShop"] = new("Desert Festival egg shop", 0, 15, 17)
    };

    private const string DesertPrefix = "DesertFestival_";

    /// <summary>The festival a stall belongs to (Festival_* and DesertFestival_* shops), or null for a shop open all year.</summary>
    public static FestivalWindow? Festival(string? shopId)
    {
        shopId ??= "";
        if (Festivals.TryGetValue(shopId, out var window)) return window;
        if (shopId.StartsWith(DesertPrefix, StringComparison.OrdinalIgnoreCase))
            return new FestivalWindow($"Desert Festival ({shopId.Substring(DesertPrefix.Length)})", 0, 15, 17);
        return null;
    }

    public static string For(string? shopId)
    {
        shopId ??= "";
        if (Known.TryGetValue(shopId, out var label)) return label;
        if (Festival(shopId) is { } f)
            return $"{f.Name}, {CropMath.SeasonName(f.Season)} {f.FirstDay}{(f.LastDay > f.FirstDay ? $"-{f.LastDay}" : "")}";
        return shopId;
    }

    /// <summary>Unit of a shop's Currency field (ShopMenu.chargePlayer): 0 gold, 1 star tokens, 2 Qi coins, 4 Qi gems.</summary>
    public static string CurrencyUnit(int currency) => currency switch
    {
        0 => "g",
        1 => " star tokens",
        2 => " Qi coins",
        4 => " Qi gems",
        _ => " (other currency)"
    };
}

/// <summary>One shop's offer of an item, priced the way ShopBuilder prices it (see KnowledgeTools).</summary>
/// <param name="ShopId">Data/Shops key.</param>
/// <param name="Price">Price in <paramref name="Unit"/> (0 for a pure trade).</param>
/// <param name="Unit">"g" for gold, else the currency (" star tokens"...).</param>
/// <param name="TradeName">Item also asked for, or "".</param>
/// <param name="TradeCount">How many of the trade item.</param>
/// <param name="Estimated">The price could not be computed and is a guess.</param>
/// <param name="RandomPrice">The shop rolls the price each day (Traveling Cart).</param>
/// <param name="Unlocked">The non-time parts of the condition hold now (year, letters, skill level...).</param>
/// <param name="Condition">The raw Data/Shops condition, or "".</param>
public sealed record PriceOffer(string ShopId, int Price, string Unit, string TradeName, int TradeCount, bool Estimated, bool RandomPrice, bool Unlocked, string Condition);

/// <summary>Which shop offer is the real price to plan with, and how an offer reads.</summary>
public static class ShopPricing
{
    /// <summary>A plain gold price known exactly (no trade item, no rolled price).</summary>
    public static bool IsGold(PriceOffer o) => o.Unit == "g" && o.TradeName.Length == 0 && o.Price > 0 && !o.Estimated && !o.RandomPrice;

    /// <summary>
    /// The offer is sold in the season (0-3); a season index outside 0-3 means any season (Greenhouse).
    /// A festival stall sells only in its festival's season.
    /// </summary>
    public static bool SoldIn(PriceOffer o, int season)
    {
        if (season < 0 || season > 3) return true;
        if (ShopLabels.Festival(o.ShopId) is { } festival && festival.Season != season) return false;
        var seasons = ShopConditions.Seasons(ShopConditions.Parse(o.Condition));
        return seasons == null || seasons[season];
    }

    /// <summary>A festival stall in <paramref name="season"/> whose last day is before <paramref name="fromDay"/>: over for this year.</summary>
    public static bool Passed(PriceOffer o, int season, int fromDay) =>
        season >= 0 && season <= 3 && ShopLabels.Festival(o.ShopId) is { } f && f.Season == season && f.LastDay < fromDay;

    /// <summary>
    /// The cheapest gold price you can pay (unlocked offers only). Offers sold in <paramref name="season"/> come first;
    /// when none is, the cheapest from another season (buy ahead). Festival stalls of <paramref name="season"/> that
    /// closed before <paramref name="fromDay"/> do not count. Null when the item has no such price.
    /// </summary>
    public static PriceOffer? Cheapest(IEnumerable<PriceOffer> offers, int season, int fromDay = 1)
    {
        var real = offers.Where(o => IsGold(o) && o.Unlocked && !Passed(o, season, fromDay)).ToList();
        var inSeason = real.Where(o => SoldIn(o, season)).ToList();
        return (inSeason.Count > 0 ? inSeason : real).OrderBy(o => o.Price).FirstOrDefault();
    }

    /// <summary>"Pierre's (SeedShop) 60g (year 2+, not unlocked yet)"; seasons are shown when <paramref name="showSeasons"/>.</summary>
    public static string Text(PriceOffer o, bool showSeasons)
    {
        string price;
        if (o.RandomPrice) price = "random price";
        else if (o.TradeName.Length > 0 && o.Price <= 0) price = $"{o.TradeCount} {o.TradeName}";
        else
        {
            price = $"{(o.Estimated ? "about " : "")}{o.Price}{o.Unit}";
            if (o.TradeName.Length > 0) price += $" + {o.TradeCount} {o.TradeName}";
        }
        var notes = ShopConditions.Label(ShopConditions.Parse(o.Condition), showSeasons);
        if (!o.Unlocked) notes = notes.Length > 0 ? notes + ", not unlocked yet" : "not available now";
        return $"{ShopLabels.For(o.ShopId)} {price}{(notes.Length > 0 ? $" ({notes})" : "")}";
    }
}

/// <summary>Short text for item facts that need no game: buffs, recipe unlocks, Queen of Sauce dates, weapons, tool upgrades.</summary>
public static class ItemFacts
{
    /// <summary>How a recipe is learned, from field 4 (crafting) or 3 (cooking) of its Data/*Recipes line.</summary>
    /// <param name="Kind">"default" (known from the start), "skill", "friendship" or "other" (event, letter, quest, TV, shop).</param>
    public sealed record Unlock(string Kind, string Who, int Level);

    public static Unlock RecipeUnlock(string? recipe, bool cooking)
    {
        var fields = (recipe ?? "").Split('/');
        int index = cooking ? 3 : 4;
        var words = index < fields.Length ? fields[index].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
        if (words.Length == 0) return new Unlock("other", "", 0);
        string head = words[0].ToLowerInvariant();
        int Level(int i) => i < words.Length && int.TryParse(words[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
        switch (head)
        {
            case "default": return new Unlock("default", "", 0);
            case "s" when words.Length >= 3: return new Unlock("skill", Capital(words[1]), Level(2));
            case "farming":
            case "fishing":
            case "combat":
            case "mining":
            case "foraging":
            case "luck":
                return new Unlock("skill", Capital(words[0]), Level(1));
            case "f" when words.Length >= 3: return new Unlock("friendship", words[1], Level(2));
            default: return new Unlock("other", "", 0);
        }
    }

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();

    /// <summary>"Farming level 3", "a letter from Lewis at 3 hearts" (only when <paramref name="omniscient"/>), "known from the start"; "" when unknown.</summary>
    public static string UnlockText(Unlock u, bool omniscient) => u.Kind switch
    {
        "default" => "known from the start",
        "skill" => $"{u.Who} level {u.Level}",
        "friendship" when omniscient => $"a letter from {u.Who} at {u.Level} hearts",
        _ => ""
    };

    /// <summary>
    /// The Sunday a Queen of Sauce week first airs (TV.getWeeklyRecipe: week = DaysPlayed % 224 / 7, day 224 is week 32).
    /// Year is 1 or 2 of the two-year cycle.
    /// </summary>
    public static (int year, int season, int day) TvWeekDate(int week)
    {
        int daysPlayed = Math.Clamp(week, 1, 32) * 7; // DaysPlayed is 1 on spring 1 of year 1
        int index = daysPlayed - 1;
        return (index / 112 + 1, index % 112 / 28, index % 28 + 1);
    }

    public static string TvText(int week)
    {
        var (year, season, day) = TvWeekDate(week);
        return $"Queen of Sauce (TV) on Sunday {CropMath.SeasonName(season)} {day} of year {year} (then every 2 years; Wednesday reruns of past weeks)";
    }

    /// <summary>"Farming +2", "Speed -1", "Attack +10%" for a BuffAttributesData field; "" for a zero value.</summary>
    public static string BuffEffect(string field, float value)
    {
        if (Math.Abs(value) < 0.0001f) return "";
        string name = field switch
        {
            "CombatLevel" => "Combat",
            "FarmingLevel" => "Farming",
            "FishingLevel" => "Fishing",
            "MiningLevel" => "Mining",
            "LuckLevel" => "Luck",
            "ForagingLevel" => "Foraging",
            "MaxStamina" => "Max Energy",
            "MagneticRadius" => "Magnetism",
            "AttackMultiplier" => "Attack",
            "KnockbackMultiplier" => "Knockback",
            "WeaponSpeedMultiplier" => "Weapon Speed",
            "CriticalChanceMultiplier" => "Crit Chance",
            "CriticalPowerMultiplier" => "Crit Power",
            "WeaponPrecisionMultiplier" => "Precision",
            _ => field
        };
        string sign = value > 0 ? "+" : "-";
        return field.EndsWith("Multiplier")
            ? $"{name} {sign}{(Math.Abs(value) * 100).ToString("0.#", CultureInfo.InvariantCulture)}%"
            : $"{name} {sign}{Math.Abs(value).ToString("0.#", CultureInfo.InvariantCulture)}";
    }

    /// <summary>Food buff length in game minutes (Data/Objects Buffs Duration): -2 is the rest of the day.</summary>
    public static string GameMinutes(int minutes)
    {
        if (minutes == -2) return "the rest of the day";
        if (minutes <= 0) return "";
        int h = minutes / 60, m = minutes % 60;
        return h > 0 ? (m > 0 ? $"{h}h {m}m" : $"{h}h") : $"{m}m";
    }

    /// <summary>
    /// What a Data/Buffs entry without stat effects does: its description on one line without the final period, plus
    /// the hard-coded rule for Oil of Garlic (buff 23: MineShaft spawns no monsters, except in the Skull Cavern).
    /// </summary>
    public static string BuffDescription(string? buffId, string? description)
    {
        string text = string.Join(" ", (description ?? "").Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        if (buffId == "23")
            text = (text.Length > 0 ? text + "; " : "") + "no monsters spawn on mine floors you enter (Skull Cavern excepted)";
        return text;
    }

    /// <summary>One buff line: "Farming +2, Max Energy +50 for 10h"; "(debuff)" is added for debuffs.</summary>
    public static string BuffLine(string name, IEnumerable<string> effects, string duration, bool debuff)
    {
        var list = effects.Where(e => e.Length > 0).ToList();
        var sb = new StringBuilder();
        if (name.Length > 0) sb.Append(name).Append(debuff ? " (debuff)" : "").Append(list.Count > 0 ? ": " : "");
        else if (debuff) sb.Append("debuff: ");
        sb.Append(string.Join(", ", list));
        if (duration.Length > 0) sb.Append(" for ").Append(duration);
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Data/Weapons Type: 1 dagger, 2 club, 3 sword, 4 slingshot. 0 is the omitted default and the game turns it into
    /// a normal sword when it creates the weapon (MeleeWeapon.ReloadData: type 0 becomes 3).
    /// </summary>
    public static string WeaponType(int type) => type switch
    {
        0 or 3 => "sword",
        1 => "dagger",
        2 => "club",
        4 => "slingshot",
        _ => "weapon"
    };

    /// <summary>The bar Clint wants for a conventional tool upgrade to a level (ShopBuilder): 1 copper .. 4 iridium.</summary>
    public static string ToolUpgradeBarId(int level) => level switch
    {
        2 => "(O)335",
        3 => "(O)336",
        4 => "(O)337",
        _ => "(O)334"
    };

    public static string ToolLevelName(int level) => level switch
    {
        0 => "basic",
        1 => "copper",
        2 => "steel",
        3 => "gold",
        4 => "iridium",
        _ => $"level {level}"
    };

    /// <summary>
    /// Readable kind of a Data/Tools entry from its ClassName: "axe", "fishing rod", "trash can" (GenericTool *TrashCan),
    /// "return scepter" (Wand); other class names are split into lower-case words.
    /// </summary>
    public static string ToolKind(string? className, string? itemId)
    {
        switch (className ?? "")
        {
            case "": return "tool";
            case "Wand": return "return scepter";
            case "GenericTool":
                return (itemId ?? "").EndsWith("TrashCan", StringComparison.OrdinalIgnoreCase) ? "trash can" : "tool";
        }
        var sb = new StringBuilder();
        foreach (char ch in className!)
        {
            if (char.IsUpper(ch) && sb.Length > 0) sb.Append(' ');
            sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    /// <summary>
    /// The metal name of a tool's UpgradeLevel ("copper") for tools on Clint's metal ladder (axe, hoe, pickaxe,
    /// watering can, pan, trash can); "" for the others (fishing rods use the level for a different ladder).
    /// </summary>
    public static string ToolLevelText(string kind, int level)
    {
        if (level < 0) return "";
        return kind is "axe" or "hoe" or "pickaxe" or "watering can" or "pan" or "trash can" ? ToolLevelName(level) : "";
    }
}

/// <summary>Reads recipe lines of Data/CraftingRecipes and Data/CookingRecipes. Also game-free; ported in test_crop_math.py.</summary>
public static class RecipeMath
{
    /// <summary>
    /// How many of an item a recipe needs. The first "/" field lists ingredients as "id count id count ...";
    /// a negative id is a whole category (-5 any egg), matched against the item's category.
    /// Returns (0, false) when the recipe does not use the item.
    /// </summary>
    public static (int count, bool viaCategory) IngredientCount(string? recipe, string itemId, int category)
    {
        if (string.IsNullOrEmpty(recipe)) return (0, false);
        var tokens = recipe.Split('/')[0].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        int viaCategory = 0;
        string wanted = CropMath.Unqualify(itemId);
        for (int i = 0; i + 1 < tokens.Length; i += 2)
        {
            if (!int.TryParse(tokens[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n <= 0) continue;
            string id = CropMath.Unqualify(tokens[i]);
            if (string.Equals(id, wanted, StringComparison.OrdinalIgnoreCase)) return (n, false);
            if (category != 0 && id == category.ToString(CultureInfo.InvariantCulture) && viaCategory == 0) viaCategory = n;
        }
        return (viaCategory, viaCategory > 0);
    }

    /// <summary>
    /// What a recipe makes (CraftingRecipe constructor): field 2 is "id count id count ...", field 3 of a crafting
    /// recipe says whether the ids are big craftables. Returns the ids, the count per craft and that flag.
    /// </summary>
    public static (List<string> ids, int count, bool bigCraftable) Outputs(string? recipe, bool cooking)
    {
        var fields = (recipe ?? "").Split('/');
        var ids = new List<string>();
        int count = 1;
        var tokens = fields.Length > 2 ? fields[2].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
        for (int i = 0; i < tokens.Length; i += 2)
        {
            ids.Add(tokens[i]);
            count = i + 1 < tokens.Length && int.TryParse(tokens[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 1;
        }
        bool big = !cooking && fields.Length > 3 && bool.TryParse(fields[3].Trim(), out bool b) && b;
        return (ids, count, big);
    }
}
