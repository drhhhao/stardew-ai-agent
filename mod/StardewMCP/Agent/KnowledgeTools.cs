using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.Buffs;
using StardewValley.GameData.Shops;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Internal;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.Quests;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;
using StardewValley.TokenizableStrings;
using SObject = StardewValley.Object;

namespace StardewMCP.Agent;

/// <summary>
/// Game knowledge read from the game's own data (Game1.cropData, Game1.objectData, Data/Shops, the recipe files),
/// so it is always right and covers modded content too. Both tools are pure observations (Instant, no game time).
/// The planning arithmetic lives in <see cref="CropMath"/>, <see cref="RecipeMath"/>, <see cref="ShopPricing"/> and <see cref="ItemFacts"/>.
/// </summary>
public static class KnowledgeTools
{
    private const int MaxRecipes = 12, MaxShops = 8, MaxCandidates = 12, MaxSeedOffers = 5;

    public static List<ToolDef> All() => new()
    {
        new()
        {
            Name = "crops",
            Description = "Season planner for crops, read from the game's own data. For every crop that grows in a season (default: now) it gives the seed, growth and regrow days, " +
                          "the last day to plant so it still matures before the season ends, how many harvests fit, seed prices from every shop and festival, sell prices and a rough gold per tile per day. " +
                          "It marks seeds you carry, tells you which crops would die at the season change and lists the fruit trees. " +
                          "season=greenhouse plans the Greenhouse (all crops, a year from planting); fertilizer=speed_gro, deluxe_speed_gro or hyper_speed_gro shortens growth like the game does " +
                          "(Agriculturist is always counted when you have it). Check it before buying or planting seeds. Costs no game time.",
            Schema = AgentTools.Obj(new
            {
                season = AgentTools.Str("spring, summer, fall, winter or greenhouse (default: the current season; another season is planned from its day 1)"),
                fertilizer = AgentTools.Str("Speed fertilizer to plan with: speed_gro, deluxe_speed_gro or hyper_speed_gro (default: none)")
            }),
            Instant = true, AppendStatus = false,
            Run = (a, c) => Report(c, "crops", () => CropsReport(a))
        },
        new()
        {
            Name = "item_info",
            Description = "Look up any item by name: objects (crops, seeds, forage, fish, artisan goods, food, minerals, rings), machines and other big craftables, weapons, tools, boots and hats. " +
                          "Gives what it sells for (also with your professions), what eating it does (energy, health, buffs), which shops sell it and for how much, where it is found, " +
                          "what to keep it for (open bundles, Museum, delivery quests, special orders) and the quest goals it counts toward, the best machine outputs, how to learn its recipe, the recipes that use it and the crop it grows into or comes from. " +
                          "Weapons show damage, speed and crit; tools show their upgrade cost. Costs no game time.",
            Schema = AgentTools.Obj(new { name = AgentTools.Str("Item name, e.g. Parsnip, Parsnip Seeds, Keg, Steel Smallsword or Copper Pickaxe") }, "name"),
            Instant = true, AppendStatus = false,
            Run = (a, c) => Report(c, "item_info", () => ItemInfoReport(a))
        }
    };

    /// <summary>Runs a report; a data mismatch (a modded or changed game file) becomes a clear failure, not a crash.</summary>
    private static IEnumerator Report(CallContext c, string tool, Func<(bool ok, string text)> build)
    {
        (bool ok, string text) r;
        try { r = build(); }
        catch (Exception ex) { r = (false, $"{tool} could not read the game data ({ex.GetType().Name}: {ex.Message}). Tell the player; the game data may differ from what the mod expects."); }
        if (r.ok) c.Result = r.text; else c.Fail(r.text);
        yield break;
    }

    private static string Num(double v, string format = "0.#") => v.ToString(format, CultureInfo.InvariantCulture);

    private static string Plural(int n, string word) => n == 1 ? $"{n} {word}" : $"{n} {word}s";

    // ------------------------------------------------------------------ crops

    private sealed class CropRow
    {
        public string Seed = "", Harvest = "", Offers = "";
        public int Growth, BaseGrowth, Regrow, Sell, Have, SeedCost = -1;
        public bool Raised, Paddy, Giant;
        public double Yield, Net, PerDay;
        public CropPlan Plan = null!;
    }

    /// <summary>Effects of the fertilizers and soils a farmer can carry (the wiki's numbers), matched by the item's English name.</summary>
    private static readonly (string name, string effect)[] Boosters =
    {
        ("Speed-Gro", "crops grow 10% faster"),
        ("Deluxe Speed-Gro", "crops grow 25% faster"),
        ("Hyper Speed-Gro", "crops grow 33% faster"),
        ("Basic Fertilizer", "better crop quality (more Silver)"),
        ("Quality Fertilizer", "better crop quality (more Gold)"),
        ("Deluxe Fertilizer", "best crop quality (Gold and some Iridium)"),
        ("Basic Retaining Soil", "the soil stays watered overnight 33% of the time"),
        ("Quality Retaining Soil", "the soil stays watered overnight 66% of the time"),
        ("Deluxe Retaining Soil", "the soil always stays watered overnight")
    };

    private static (bool ok, string text) CropsReport(JsonElement a)
    {
        if (Game1.cropData == null || Game1.objectData == null) return (false, "The crop data is not loaded yet.");
        int current = CropMath.SeasonIndex(Game1.currentSeason);
        int season = current;
        bool greenhouse = false;
        var requested = AgentTools.S(a, "season").Trim();
        if (requested.Equals("greenhouse", StringComparison.OrdinalIgnoreCase)) greenhouse = true;
        else if (requested.Length > 0)
        {
            season = CropMath.SeasonIndex(requested);
            if (season < 0) return (false, $"Unknown season \"{requested}\". Use spring, summer, fall, winter or greenhouse.");
        }
        if (!greenhouse && season < 0) return (false, "Cannot tell the current season; pass season=spring, summer, fall, winter or greenhouse.");

        var fertArg = AgentTools.S(a, "fertilizer").Trim();
        float fertilizer = CropMath.FertilizerBoost(fertArg);
        if (fertilizer < 0) return (false, $"Unknown fertilizer \"{fertArg}\". Use speed_gro, deluxe_speed_gro or hyper_speed_gro, or leave it out.");
        bool agriculturist = Game1.player.professions.Contains(Farmer.agriculturist);
        float boost = CropMath.GrowthBoost(fertilizer, agriculturist);

        bool today = !greenhouse && season == current;
        int planSeason = greenhouse ? 0 : season;
        int startDay = greenhouse ? 0 : today ? Math.Clamp(Game1.dayOfMonth, 1, CropMath.DaysPerSeason) : 1;
        int priceSeason = greenhouse ? -1 : season;
        string seasonName = greenhouse ? "the Greenhouse" : CropMath.SeasonName(season);
        var carried = CarriedObjects();
        var objects = Game1.objectData;
        var shops = new ShopIndex();
        var giants = GiantCropHarvests();

        var ready = new List<CropRow>();
        var unpriced = new List<CropRow>();
        var late = new List<CropRow>();
        var bySeed = new Dictionary<string, CropRow>();
        foreach (var kv in Game1.cropData)
        {
            var data = kv.Value;
            if (data == null || data.DaysInPhase == null) continue;
            var grows = new bool[4];
            if (greenhouse) grows = new[] { true, true, true, true };
            else if (data.Seasons != null)
                foreach (var s in data.Seasons)
                {
                    int i = CropMath.SeasonIndex(s.ToString());
                    if (i >= 0) grows[i] = true;
                }
            if (!grows[planSeason]) continue;
            int baseGrowth = CropMath.GrowthDays(data.DaysInPhase);
            if (baseGrowth <= 0) continue;
            int growth = CropMath.GrowthDays(CropMath.SpeedUpPhases(data.DaysInPhase, boost));

            string harvestId = CropMath.Unqualify(data.HarvestItemId);
            int sell = objects.TryGetValue(harvestId, out var harvestData) && harvestData != null ? harvestData.Price : 0;

            var offers = shops.Offers(CropMath.Qualify(kv.Key), recipe: false);
            var cheapest = ShopPricing.Cheapest(offers, priceSeason, startDay);
            int seedCost = cheapest?.Price ?? -1;
            var shown = offers.Where(o => ShopPricing.SoldIn(o, priceSeason)).ToList();
            if (shown.Count == 0) shown = offers;
            var offerText = shown.OrderBy(o => ShopPricing.Passed(o, priceSeason, startDay) ? 1 : 0).ThenBy(o => ShopPricing.IsGold(o) ? 0 : 1).ThenBy(o => o.Price).Take(MaxSeedOffers)
                .Select(o => ShopPricing.Text(o, greenhouse || !ShopPricing.SoldIn(o, priceSeason)) + (ShopPricing.Passed(o, priceSeason, startDay) ? " (over for this year)" : "")).ToList();
            if (shown.Count > MaxSeedOffers) offerText.Add($"{shown.Count - MaxSeedOffers} more");

            var plan = CropMath.Plan(planSeason, startDay, grows, growth, data.RegrowDays);
            double perHarvest = CropMath.ExpectedYield(data.HarvestMinStack, data.HarvestMaxStack, data.ExtraHarvestChance);
            double net = CropMath.NetGold(plan, perHarvest, sell, Math.Max(seedCost, 0));
            carried.TryGetValue(kv.Key, out int have);
            var row = new CropRow
            {
                Seed = SafeName(CropMath.Qualify(kv.Key), kv.Key),
                Harvest = SafeName(CropMath.Qualify(harvestId), harvestId),
                Offers = string.Join(", ", offerText),
                Growth = growth, BaseGrowth = baseGrowth, Regrow = data.RegrowDays, Sell = sell, Have = have, SeedCost = seedCost,
                Raised = data.IsRaised, Paddy = data.IsPaddyCrop, Giant = giants.Contains(harvestId),
                Yield = perHarvest, Plan = plan, Net = net,
                PerDay = CropMath.GoldPerTilePerDay(plan, net)
            };
            (plan.Matures ? (seedCost > 0 ? ready : unpriced) : late).Add(row);
            bySeed[kv.Key] = row;
        }

        var sb = new StringBuilder();
        if (greenhouse)
            sb.AppendLine("Crops for the Greenhouse: every crop grows there all year. Each crop is planned over one year (4 x 28 days) from planting.");
        else
            sb.AppendLine(today
                ? $"Crops for {seasonName}. Today is {seasonName} {startDay}, so a crop planted and watered today has {CropMath.DaysPerSeason - startDay} growing days before {seasonName} ends."
                : $"Crops for {seasonName}, planned from day 1 of {seasonName} (today is {Game1.currentSeason} {Game1.dayOfMonth}).");
        sb.AppendLine("Numbers assume you water every day (rain does it for you), normal quality and the cheapest seed price you can pay now. g/tile/day = net gold / days the tile is busy.");
        var speed = new List<string>();
        if (fertilizer > 0) speed.Add($"{SafeName(FertilizerItem(fertilizer), "Speed-Gro")} applied before planting");
        if (agriculturist) speed.Add("your Agriculturist profession");
        if (speed.Count > 0)
            sb.AppendLine($"Growth days include {string.Join(" and ", speed)} ({Num(boost * 100, "0")}% faster; regrow days do not change).");

        AppendCarried(sb, carried, bySeed, planSeason, startDay, today, greenhouse);

        if (ready.Count == 0 && unpriced.Count == 0 && late.Count == 0)
            sb.AppendLine($"No crop grows outdoors in {seasonName}. The Greenhouse ignores seasons (season=greenhouse).");

        string when = greenhouse ? "in the Greenhouse" : today ? "today" : "on day 1";
        if (ready.Count > 0)
        {
            sb.AppendLine($"Will mature if planted {when} (best gold per tile per day first):");
            foreach (var r in ready.OrderByDescending(x => x.PerDay).ThenBy(x => x.Harvest))
                AppendReady(sb, r, planSeason, startDay, today, greenhouse);
        }
        if (unpriced.Count > 0)
        {
            sb.AppendLine($"Will mature, but no shop sells the seed for gold you can pay now (seed cost not counted, ranked separately):");
            foreach (var r in unpriced.OrderByDescending(x => x.PerDay).ThenBy(x => x.Harvest))
                AppendReady(sb, r, planSeason, startDay, today, greenhouse);
        }
        if (late.Count > 0)
        {
            sb.AppendLine($"Too late, would die when the season changes if planted {when}:");
            foreach (var r in late.OrderBy(x => x.Growth).ThenBy(x => x.Harvest))
                sb.AppendLine($"- {r.Harvest} (seeds: {r.Seed}){(r.Have > 0 ? $" — you carry {r.Have}" : "")}: needs {Plural(r.Growth, "day")}, only {r.Plan.DaysAvailable} left.");
        }

        var all = ready.Concat(unpriced).Concat(late).ToList();
        if (all.Any(x => x.Raised))
            sb.AppendLine("Trellis crops [trellis] block walking: leave a path between the rows.");
        if (all.Any(x => x.Paddy))
            sb.AppendLine("Paddy crops [paddy] grow 25% faster on tiles next to water (not counted above) and need no watering there.");
        if (all.Any(x => x.Giant))
            sb.AppendLine("Crops marked [giant] can merge into a giant crop when planted in a 3x3 block outdoors.");

        AppendTrees(sb, shops, season, greenhouse);

        var boosters = BoosterLines();
        if (boosters.Count > 0)
        {
            sb.AppendLine($"Fertilizer you carry (apply with plant before the seeds{(fertilizer > 0 ? "" : "; plan with it via fertilizer=speed_gro, deluxe_speed_gro or hyper_speed_gro")}):");
            foreach (var line in boosters) sb.AppendLine("  " + line);
        }
        sb.AppendLine("Prices come from the game's shop data (festival stalls and the Traveling Cart included; the Cart also sells random seeds). " +
                      "A Tiller sells crops for 10% more." + (greenhouse ? "" : " Crops in the Greenhouse ignore seasons (season=greenhouse)."));
        return (true, sb.ToString().TrimEnd());
    }

    private static string FertilizerItem(float boost) => boost >= 0.3f ? "(O)918" : boost >= 0.2f ? "(O)466" : "(O)465";

    /// <summary>Unqualified harvest ids that can form a giant crop (Data/GiantCrops FromItemId).</summary>
    private static HashSet<string> GiantCropHarvests()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var g in DataLoader.GiantCrops(Game1.content).Values)
                if (g?.FromItemId != null) set.Add(CropMath.Unqualify(g.FromItemId));
        }
        catch { /* no giant crop data: no tag */ }
        return set;
    }

    private static void AppendCarried(StringBuilder sb, Dictionary<string, int> carried, Dictionary<string, CropRow> bySeed, int season, int startDay, bool today, bool greenhouse)
    {
        var lines = new List<string>();
        foreach (var kv in carried)
        {
            if (!Game1.cropData.TryGetValue(kv.Key, out var data) || data == null) continue;
            if (bySeed.TryGetValue(kv.Key, out var row))
                lines.Add(greenhouse
                    ? $"{row.Seed} x{kv.Value}: {Plural(row.Growth, "day")} to grow in the Greenhouse"
                    : row.Plan.Matures
                        ? $"{row.Seed} x{kv.Value}: {PlantText(season, row.Plan.LastPlantDay, startDay, today).ToLowerInvariant()}"
                        : $"{row.Seed} x{kv.Value}: too late this season");
            else
                lines.Add($"{SafeName(CropMath.Qualify(kv.Key), kv.Key)} x{kv.Value}: does not grow in {CropMath.SeasonName(season)} (grows in {SeasonsText(data.Seasons?.Select(x => x.ToString()))})");
        }
        if (lines.Count > 0)
            sb.AppendLine("Seeds you carry — " + string.Join("; ", lines) + ".");
    }

    private static void AppendReady(StringBuilder sb, CropRow r, int season, int startDay, bool today, bool greenhouse)
    {
        var tags = (r.Raised ? " [trellis]" : "") + (r.Paddy ? " [paddy]" : "") + (r.Giant ? " [giant]" : "");
        sb.AppendLine($"- {r.Harvest} (seeds: {r.Seed}{(r.Have > 0 ? $", you carry {r.Have}" : "")}){tags}");
        sb.Append("    ");
        sb.Append($"{Plural(r.Growth, "day")} to grow{(r.Growth != r.BaseGrowth ? $" (normally {r.BaseGrowth})" : "")}, ");
        if (greenhouse)
        {
            sb.Append(r.Regrow > 0
                ? $"then a harvest every {r.Regrow} days ({Plural(r.Plan.Harvests, "harvest")} in a year). "
                : $"one harvest per planting ({Plural(r.Plan.Plantings, "planting")} fit in a year back to back). ");
        }
        else
        {
            var when = today ? "if planted today" : "if planted on day 1";
            var first = CropMath.Advance(season, startDay, r.Growth);
            sb.Append(r.Regrow > 0
                ? $"then a harvest every {r.Regrow} days ({Plural(r.Plan.Harvests, "harvest")} {when}). "
                : $"one harvest per planting ({Plural(r.Plan.Plantings, "planting")} fit back to back). ");
            sb.Append($"{PlantText(season, r.Plan.LastPlantDay, startDay, today)}; first harvest {DayLabel(first.season, first.day)} {when}. ");
        }
        sb.Append($"Sells {(r.Sell > 0 ? r.Sell + "g" : "for an unknown price")}{(Math.Abs(r.Yield - 1.0) > 0.001 ? $" x{Num(r.Yield, "0.##")} per harvest" : "")}. ");
        sb.Append(r.Offers.Length > 0 ? $"Seed: {r.Offers}. " : "Seed: no shop lists it (forage, Seed Maker, quest or random Traveling Cart stock), cost not counted. ");
        sb.AppendLine($"About {Num(r.PerDay)}g per tile per day (net {Num(r.Net, "0")}g per tile).");
    }

    /// <summary>Fruit trees that bear in the season (all of them for the Greenhouse): sapling price, fruit, a year's fruit value.</summary>
    private static void AppendTrees(StringBuilder sb, ShopIndex shops, int season, bool greenhouse)
    {
        var trees = Game1.fruitTreeData;
        if (trees == null || trees.Count == 0) return;
        var lines = new List<(double perYear, string text)>();
        foreach (var kv in trees)
        {
            var data = kv.Value;
            if (data == null) continue;
            var seasons = (data.Seasons ?? new List<Season>()).Select(s => CropMath.SeasonIndex(s.ToString())).Where(i => i >= 0).Distinct().ToList();
            if (!greenhouse && !seasons.Contains(season)) continue;
            var fruit = data.Fruit?.FirstOrDefault(f => f != null && !string.IsNullOrEmpty(f.ItemId) && (greenhouse || f.Season == null || CropMath.SeasonIndex(f.Season.ToString()) == season));
            if (fruit == null) continue;
            string fruitId = CropMath.Unqualify(fruit.ItemId);
            int fruitPrice = Game1.objectData.TryGetValue(fruitId, out var fd) && fd != null ? fd.Price : 0;
            int days = CropMath.FruitDaysPerYear(seasons.Count, greenhouse);
            double perYear = fruitPrice * days;

            string saplingQid = CropMath.Qualify(kv.Key);
            var offers = shops.Offers(saplingQid, recipe: false);
            var cheapest = ShopPricing.Cheapest(offers, -1);
            string sapling = SafeName(saplingQid, kv.Key);
            string buy = offers.Count == 0 ? "no shop sells it"
                : string.Join(", ", offers.OrderBy(o => ShopPricing.IsGold(o) ? 0 : 1).ThenBy(o => o.Price).Take(3).Select(o => ShopPricing.Text(o, true)));
            string payback = cheapest != null && fruitPrice > 0 ? $", the sapling pays back after {(cheapest.Price + fruitPrice - 1) / fruitPrice} fruit" : "";
            string where = greenhouse ? "every day in the Greenhouse" : $"a day in {string.Join("/", seasons.OrderBy(i => i).Select(CropMath.SeasonName))}";
            lines.Add((perYear, $"- {SafeName(CropMath.Qualify(fruitId), fruitId)} {(fruitPrice > 0 ? fruitPrice + "g" : "(no price)")}, 1 {where}: about {Num(perYear, "0")}g a year per tree{payback}. Sapling: {sapling}, {buy}."));
        }
        if (lines.Count == 0)
        {
            sb.AppendLine($"No fruit tree bears fruit in {CropMath.SeasonName(season)}; saplings still grow in any season.");
            return;
        }
        sb.AppendLine($"Fruit trees{(greenhouse ? " in the Greenhouse" : $" bearing in {CropMath.SeasonName(season)}")} (a sapling needs {CropMath.SaplingDays} days in any season with the 8 tiles around it clear, then gives 1 fruit a day in season; fruit quality rises each year):");
        foreach (var l in lines.OrderByDescending(x => x.perYear)) sb.AppendLine(l.text);
    }

    /// <summary>The last day to plant, or "any day" when the crop still matures if planted on the last day of the season.</summary>
    private static string PlantText(int season, int lastPlantDay, int startDay, bool today)
    {
        if (lastPlantDay >= CropMath.DaysPerSeason) return "Plant any day this season";
        return $"Plant by {CropMath.SeasonName(season)} {lastPlantDay}{(today && lastPlantDay == startDay ? " (today is the last day)" : "")}";
    }

    private static string DayLabel(int season, int day) =>
        $"{CropMath.SeasonName(season)} {day}{(season >= 4 ? " (next year)" : "")}";

    private static string SeasonsText(IEnumerable<string>? seasons) =>
        seasons == null ? "no season" : string.Join("/", seasons.Select(x => x.ToLowerInvariant()));

    private static Dictionary<string, int> CarriedObjects()
    {
        var have = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var it in Game1.player.Items)
        {
            if (it == null) continue;
            var id = CropMath.Unqualify(it.QualifiedItemId);
            have.TryGetValue(id, out int n);
            have[id] = n + it.Stack;
        }
        return have;
    }

    private static List<string> BoosterLines()
    {
        var lines = new List<string>();
        foreach (var it in Game1.player.Items)
        {
            if (it == null) continue;
            foreach (var (name, effect) in Boosters)
                if (string.Equals(it.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add($"{it.DisplayName} x{it.Stack}: {effect}");
                    break;
                }
        }
        return lines;
    }

    // ------------------------------------------------------------------ shops

    /// <summary>
    /// Every fixed item entry of every Data/Shops shop, by qualified item id; built once per report. Offers are priced the
    /// way ShopBuilder prices them (base price, profit margins, shop and item price modifiers), and their condition is
    /// split into "when" (season, weekday, chance: read by <see cref="ShopConditions"/>) and "whether" (year, letters,
    /// skill levels: checked now with GameStateQuery).
    /// </summary>
    private sealed class ShopIndex
    {
        private static readonly HashSet<string> TimeKeys = new(ShopConditions.TimeKeys, StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<(string shopId, ShopData shop, ShopItemData entry)>> byItem = new(StringComparer.OrdinalIgnoreCase);

        public ShopIndex()
        {
            foreach (var shop in DataLoader.Shops(Game1.content))
            {
                if (shop.Value?.Items == null) continue;
                foreach (var entry in shop.Value.Items)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId) || entry.ItemId.Contains(' ')) continue; // item queries (RANDOM_ITEMS...) are skipped
                    string? qid = null;
                    try { qid = ItemRegistry.QualifyItemId(entry.ItemId); } catch { }
                    qid ??= CropMath.Qualify(entry.ItemId);
                    if (!byItem.TryGetValue(qid, out var list)) byItem[qid] = list = new();
                    list.Add((shop.Key, shop.Value, entry));
                }
            }
        }

        /// <summary>Offers of the item (<paramref name="recipe"/>: offers of its recipe instead), one per distinct shop, price and condition.</summary>
        public List<PriceOffer> Offers(string qualifiedId, bool recipe)
        {
            var offers = new List<PriceOffer>();
            if (!byItem.TryGetValue(qualifiedId, out var list)) return offers;
            foreach (var (shopId, shop, entry) in list)
            {
                if (entry.IsRecipe != recipe) continue;
                var offer = Price(shopId, shop, entry, qualifiedId);
                if (!offers.Contains(offer)) offers.Add(offer);
            }
            return offers;
        }

        private static PriceOffer Price(string shopId, ShopData shop, ShopItemData entry, string qid)
        {
            bool randomPrice = HasRandom(entry.PriceModifiers) || (!entry.IgnoreShopPriceModifiers && HasRandom(shop.PriceModifiers));
            int price;
            bool estimated = false;
            try
            {
                var item = ItemRegistry.Create(qid, allowNull: true) ?? throw new InvalidOperationException("unknown item");
                if (entry.IsRecipe) item.IsRecipe = true;
                float value = ShopBuilder.GetBasePrice(new ItemQueryResult(item), shop, entry, item, false, entry.UseObjectDataPrice);
                if (!randomPrice)
                {
                    if (!entry.IgnoreShopPriceModifiers)
                        value = Utility.ApplyQuantityModifiers(value, shop.PriceModifiers, shop.PriceModifierMode, null, null, item);
                    value = Utility.ApplyQuantityModifiers(value, entry.PriceModifiers, entry.PriceModifierMode, null, null, item);
                }
                price = (int)value;
            }
            catch
            {
                int basePrice = Game1.objectData != null && Game1.objectData.TryGetValue(CropMath.Unqualify(qid), out var od) && od != null ? od.Price : 0;
                (price, estimated) = entry.TradeItemId != null && entry.Price < 0 ? (0, false) : CropMath.ShopPrice(entry.Price, basePrice);
            }

            string trade = "";
            int tradeCount = 0;
            if (!string.IsNullOrEmpty(entry.TradeItemId) && entry.TradeItemAmount >= 0)
            {
                trade = SafeName(entry.TradeItemId, entry.TradeItemId);
                tradeCount = entry.TradeItemAmount;
            }

            bool unlocked = true;
            if (!string.IsNullOrWhiteSpace(entry.Condition))
            {
                try { unlocked = GameStateQuery.CheckConditions(entry.Condition, ignoreQueryKeys: TimeKeys); }
                catch { unlocked = false; }
            }
            return new PriceOffer(shopId, price, ShopLabels.CurrencyUnit(shop.Currency), trade, tradeCount, estimated, randomPrice, unlocked, entry.Condition ?? "");
        }

        private static bool HasRandom(IList<QuantityModifier>? modifiers) =>
            modifiers != null && modifiers.Any(m => m?.RandomAmount != null && m.RandomAmount.Count > 0);
    }

    /// <summary>Display name of an item id, or the fallback when the game does not know it.</summary>
    private static string SafeName(string id, string fallback)
    {
        try { return ItemRegistry.GetData(id)?.DisplayName ?? fallback; }
        catch { return fallback; }
    }

    // ------------------------------------------------------------------ item_info

    private sealed record ItemRef(string Qid, string Display, string Name, int Rank);

    /// <summary>Item types searched by name, best first; flooring and wallpaper are left out (they all share one name).</summary>
    private static readonly string[] TypeOrder = { "(O)", "(BC)", "(W)", "(T)", "(B)", "(H)", "(TR)", "(F)", "(S)", "(P)", "(M)" };

    private static (bool ok, string text) ItemInfoReport(JsonElement a)
    {
        if (Game1.objectData == null) return (false, "The object data is not loaded yet.");
        var query = AgentTools.S(a, "name").Trim();
        if (query.Length == 0) return (false, "Give the item name, e.g. item_info(name=\"Parsnip\").");

        var (matches, exact) = FindItems(query);
        if (matches.Count == 0)
            return (false, $"No item called \"{query}\". item_info covers objects, big craftables (machines), weapons, tools, boots, hats, trinkets, furniture and clothes; check the spelling.");
        if (!exact && matches.Count > 1)
        {
            var names = matches.Select(m => m.Display).Distinct().OrderBy(n => n).Take(MaxCandidates);
            return (false, $"Several items match \"{query}\": {string.Join(", ", names)}{(matches.Count > MaxCandidates ? ", ..." : "")}. Ask again with the full name.");
        }

        var shops = new ShopIndex();
        var parts = matches.Take(3).Select(m => Describe(m, shops)).ToList();
        if (matches.Count > 3) parts.Add($"({matches.Count - 3} more items are also called \"{query}\".)");
        return (true, string.Join("\n\n", parts));
    }

    /// <summary>Items whose shown name, English name or id equals the query; else those whose names contain it. Objects come first.</summary>
    private static (List<ItemRef> matches, bool exact) FindItems(string query)
    {
        string q = query.ToLowerInvariant(), bareId = CropMath.Unqualify(query).ToLowerInvariant();
        bool qualified = query.StartsWith("(");
        var exact = new List<ItemRef>();
        var containing = new List<ItemRef>();
        foreach (var type in ItemRegistry.ItemTypes)
        {
            int rank = Array.IndexOf(TypeOrder, type.Identifier);
            if (rank < 0) continue;
            foreach (var id in type.GetAllIds())
            {
                ParsedItemData? data;
                try { data = ItemRegistry.GetData(type.Identifier + id); } catch { continue; } // ItemRegistry caches the parsed data
                if (data == null || data.IsErrorItem) continue;
                var internalName = data.InternalName ?? "";
                var display = string.IsNullOrEmpty(data.DisplayName) ? (internalName.Length > 0 ? internalName : id) : data.DisplayName;
                string d = display.ToLowerInvariant(), n = internalName.ToLowerInvariant();
                var r = new ItemRef(data.QualifiedItemId, display, internalName, rank);
                bool idMatch = qualified ? string.Equals(data.QualifiedItemId, query, StringComparison.OrdinalIgnoreCase)
                                         : rank == 0 && id.ToLowerInvariant() == bareId;
                if (d == q || n == q || idMatch) exact.Add(r);
                else if (d.Contains(q) || n.Contains(q)) containing.Add(r);
            }
        }
        var list = exact.Count > 0 ? exact : containing;
        return (list.OrderBy(r => r.Rank).ToList(), exact.Count > 0);
    }

    private static string Describe(ItemRef r, ShopIndex shops)
    {
        var data = ItemRegistry.GetDataOrErrorItem(r.Qid);
        var item = TryCreate(r.Qid);
        bool isObject = r.Qid.StartsWith("(O)");
        Game1.objectData.TryGetValue(data.ItemId, out var objData);
        if (!isObject) objData = null;

        string kind = "";
        try { kind = item?.getCategoryName() ?? ""; } catch { }
        if (kind.Length == 0) kind = TypeKind(r.Qid);

        var sb = new StringBuilder();
        sb.AppendLine($"{r.Display} (id {(isObject ? data.ItemId : r.Qid)}){(kind.Length > 0 ? " — " + kind : "")}");
        bool showDescription = !isObject || data.Category == SObject.ringCategory || data.ObjectType == "Ring";
        if (showDescription && !string.IsNullOrWhiteSpace(data.Description))
            sb.AppendLine("About: " + data.Description.Replace("\n", " ").Trim());

        if (objData != null) AppendObjectValue(sb, objData, item);
        else AppendOtherValue(sb, item);

        AppendTypeFacts(sb, r.Qid, data, item);

        var offers = shops.Offers(r.Qid, recipe: false);
        if (offers.Count == 0)
            sb.AppendLine("Shops: no shop lists it by item id (random stock, festivals and the Traveling Cart may still sell it).");
        else
            sb.AppendLine("Shops: " + string.Join("; ", offers.Take(MaxShops).Select(o => ShopPricing.Text(o, true))) + (offers.Count > MaxShops ? $"; and {offers.Count - MaxShops} more" : "") +
                          (offers.Any(x => x.Estimated) ? " ('about' = estimated as twice the base value; confirm with shop_list)." : "."));

        // The Shops line above prices the listed offers; Found keeps its shop sources only for random stock it alone reads.
        AppendList(sb, "Found", KnowledgeSources.For(r.Qid).Where(l => offers.Count == 0 || !l.StartsWith("shop:")).ToList());
        if (KnowledgeSources.Errors.Count > 0)
            sb.AppendLine($"(Some game data could not be read, so sources may be missing: {string.Join("; ", KnowledgeSources.Errors.Take(2))}.)");
        var (keep, counts) = KeepFor(r.Qid, item);
        AppendList(sb, "Keep for", keep);
        AppendList(sb, "Counts toward (on pickup or catch, nothing to hand in)", counts);
        AppendList(sb, "Machines", MachineTools.BestOutputs(r.Qid, 3));

        if (isObject) AppendCropLinks(sb, data.ItemId);
        AppendRecipeSources(sb, r.Qid, shops);
        if (objData != null)
        {
            AppendRecipes(sb, "Crafting", "Data/CraftingRecipes", false, data.ItemId, objData.Category);
            AppendRecipes(sb, "Cooking", "Data/CookingRecipes", true, data.ItemId, objData.Category);
        }
        return sb.ToString().TrimEnd();
    }

    private static string TypeKind(string qid)
    {
        string type = qid.Substring(0, qid.IndexOf(')') + 1);
        return type switch
        {
            "(BC)" => "big craftable",
            "(W)" => "weapon",
            "(T)" => "tool",
            "(B)" => "boots",
            "(H)" => "hat",
            "(TR)" => "trinket",
            "(F)" => "furniture",
            "(S)" => "shirt",
            "(P)" => "pants",
            "(M)" => "mannequin",
            _ => ""
        };
    }

    private static void AppendList(StringBuilder sb, string label, IReadOnlyList<string>? lines)
    {
        if (lines == null) return;
        var list = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (list.Count > 0) sb.AppendLine($"{label}: {string.Join("; ", list)}.");
    }

    /// <summary>Base sell prices, the payout with the farm's professions, food values and buffs of an object.</summary>
    private static void AppendObjectValue(StringBuilder sb, StardewValley.GameData.Objects.ObjectData data, Item? item)
    {
        if (data.Price > 0)
        {
            sb.AppendLine($"Sells for {data.Price}g at normal quality; Silver {CropMath.PriceAtQuality(data.Price, 1)}g, Gold {CropMath.PriceAtQuality(data.Price, 2)}g, " +
                          $"Iridium {CropMath.PriceAtQuality(data.Price, 4)}g where the item comes in those qualities.");
            var paid = YouGet(item);
            if (paid.Length > 0) sb.AppendLine(paid);
        }
        else
            sb.AppendLine("Has no sell price (base price 0).");

        if (data.Edibility > -300)
            sb.AppendLine(item is SObject food
                ? $"Edible: restores {food.staminaRecoveredOnConsumption()} energy and {food.healthRecoveredOnConsumption()} health (use the eat tool)."
                : "Edible.");
        else
            sb.AppendLine("Not edible.");

        var buffs = BuffLines(data);
        if (buffs.Count > 0) sb.AppendLine("Buffs: " + string.Join("; ", buffs) + ".");
    }

    /// <summary>"You get: 38g (Silver 47g, Gold 57g, Iridium 76g) with your farm's professions"; "" when the item has no price.</summary>
    private static string YouGet(Item? item)
    {
        if (item == null) return "";
        long me = Game1.player.UniqueMultiplayerID;
        if (item is not SObject) // rings and other objects without qualities
        {
            int sell = item.sellToStorePrice(me);
            return sell > 0 ? $"You get: {sell}g when sold to a shop." : "";
        }
        var prices = new List<int>();
        foreach (int quality in new[] { 0, 1, 2, 4 })
        {
            var copy = TryCreate(item.QualifiedItemId, quality);
            prices.Add(copy?.sellToStorePrice(me) ?? -1);
        }
        if (prices[0] <= 0) return "";
        return $"You get: {prices[0]}g when shipped or sold (Silver {prices[1]}g, Gold {prices[2]}g, Iridium {prices[3]}g), with the professions on your farm.";
    }

    private static void AppendOtherValue(StringBuilder sb, Item? item)
    {
        int sell = -1;
        try { sell = item?.sellToStorePrice(Game1.player.UniqueMultiplayerID) ?? -1; } catch { }
        sb.AppendLine(sell > 0 ? $"You get: {sell}g when sold to a shop." : "Has no sell price.");
    }

    /// <summary>Food and drink buffs from Data/Objects (custom stats in game minutes, or a Data/Buffs entry).</summary>
    private static List<string> BuffLines(StardewValley.GameData.Objects.ObjectData data)
    {
        var lines = new List<string>();
        if (data.Buffs == null) return lines;
        Dictionary<string, BuffData>? buffData = null;
        foreach (var b in data.Buffs)
        {
            if (b == null) continue;
            var effects = Effects(b.CustomAttributes);
            string name = "", duration = ItemFacts.GameMinutes(b.Duration);
            bool debuff = b.IsDebuff;
            if (!string.IsNullOrWhiteSpace(b.BuffId))
            {
                buffData ??= DataLoader.Buffs(Game1.content);
                if (buffData.TryGetValue(b.BuffId, out var bd) && bd != null)
                {
                    name = TokenParser.ParseText(bd.DisplayName) ?? b.BuffId;
                    var statEffects = Effects(bd.Effects);
                    effects.AddRange(statEffects);
                    if (statEffects.Count == 0) // a special buff (Oil of Garlic, Squid Ink Ravioli): say what it does
                        effects.Add(ItemFacts.BuffDescription(b.BuffId, TokenParser.ParseText(bd.Description)));
                    debuff |= bd.IsDebuff;
                    if (b.Duration == 0) // Data/Buffs durations are milliseconds of play; show them in game minutes like food buffs
                        duration = bd.Duration == -2 ? "the rest of the day" : ItemFacts.GameMinutes(bd.Duration / Game1.realMilliSecondsPerGameMinute);
                }
            }
            if (duration.Length > 0 && duration != "the rest of the day") duration += " game time";
            if (effects.Count == 0 && name.Length == 0) continue;
            lines.Add(ItemFacts.BuffLine(name, effects, duration, debuff));
        }
        return lines;
    }

    private static List<string> Effects(BuffAttributesData? a)
    {
        var list = new List<string>();
        if (a == null) return list;
        void Add(string field, float value)
        {
            var e = ItemFacts.BuffEffect(field, value);
            if (e.Length > 0) list.Add(e);
        }
        Add("FarmingLevel", a.FarmingLevel);
        Add("FishingLevel", a.FishingLevel);
        Add("MiningLevel", a.MiningLevel);
        Add("ForagingLevel", a.ForagingLevel);
        Add("CombatLevel", a.CombatLevel);
        Add("LuckLevel", a.LuckLevel);
        Add("MaxStamina", a.MaxStamina);
        Add("MagneticRadius", a.MagneticRadius);
        Add("Speed", a.Speed);
        Add("Defense", a.Defense);
        Add("Attack", a.Attack);
        Add("AttackMultiplier", a.AttackMultiplier);
        Add("Immunity", a.Immunity);
        Add("KnockbackMultiplier", a.KnockbackMultiplier);
        Add("WeaponSpeedMultiplier", a.WeaponSpeedMultiplier);
        Add("CriticalChanceMultiplier", a.CriticalChanceMultiplier);
        Add("CriticalPowerMultiplier", a.CriticalPowerMultiplier);
        Add("WeaponPrecisionMultiplier", a.WeaponPrecisionMultiplier);
        return list;
    }

    /// <summary>Weapon stats, tool level and upgrade cost, boot stats.</summary>
    private static void AppendTypeFacts(StringBuilder sb, string qid, ParsedItemData data, Item? item)
    {
        if (qid.StartsWith("(W)") && Game1.weaponData != null && Game1.weaponData.TryGetValue(data.ItemId, out var w) && w != null)
        {
            if (w.Type == 4)
                sb.AppendLine("Weapon: slingshot; the damage comes from the ammo (stones, ore, fruit...).");
            else
            {
                var stats = new List<string>
                {
                    $"damage {w.MinDamage}-{w.MaxDamage}",
                    $"speed {w.Speed.ToString("+0;-0;0", CultureInfo.InvariantCulture)}",
                    $"crit chance {Num(w.CritChance * 100)}% (x{Num(w.CritMultiplier)} damage)"
                };
                if (w.Defense != 0) stats.Add($"defense {w.Defense.ToString("+0;-0", CultureInfo.InvariantCulture)}");
                if (w.Precision != 0) stats.Add($"precision {w.Precision.ToString("+0;-0", CultureInfo.InvariantCulture)}");
                if (Math.Abs(w.Knockback - 1f) > 0.01f) stats.Add($"knockback {Num(w.Knockback)}");
                if (w.AreaOfEffect != 0) stats.Add($"area {w.AreaOfEffect.ToString("+0;-0", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"Weapon: {ItemFacts.WeaponType(w.Type)}, {string.Join(", ", stats)}.");
            }
        }
        else if (qid.StartsWith("(T)") && Game1.toolData != null && Game1.toolData.TryGetValue(data.ItemId, out var t) && t != null)
        {
            string kind = ItemFacts.ToolKind(t.ClassName, data.ItemId);
            var facts = new List<string> { kind };
            string level = ItemFacts.ToolLevelText(kind, t.UpgradeLevel);
            if (level.Length > 0) facts.Add(level);
            if (t.AttachmentSlots > 0) facts.Add(Plural(t.AttachmentSlots, "attachment slot"));
            sb.AppendLine($"Tool: {string.Join(", ", facts)}.");
            var upgrades = new List<string>();
            if (!string.IsNullOrEmpty(t.ConventionalUpgradeFrom))
                upgrades.Add($"Clint upgrades your {SafeName(t.ConventionalUpgradeFrom, t.ConventionalUpgradeFrom)} into it for {ShopBuilder.GetToolUpgradeConventionalPrice(t.UpgradeLevel)}g + 5 {SafeName(ItemFacts.ToolUpgradeBarId(t.UpgradeLevel), "bars")} (ready in 2 days)");
            if (t.UpgradeFrom != null)
                foreach (var u in t.UpgradeFrom)
                {
                    if (u == null) continue;
                    var cost = new List<string>();
                    // Price -1 means the tool's sale price (ItemQueryResolver TOOL_UPGRADES)
                    int gold = u.Price > -1 ? u.Price : Math.Max(0, item is Tool made ? made.salePrice() : t.SalePrice);
                    if (gold > 0) cost.Add(gold + "g");
                    if (!string.IsNullOrEmpty(u.TradeItemId)) cost.Add($"{u.TradeItemAmount} {SafeName(u.TradeItemId, u.TradeItemId)}");
                    string from = string.IsNullOrEmpty(u.RequireToolId) ? "" : $" from your {SafeName(u.RequireToolId, u.RequireToolId)}";
                    string cond = ShopConditions.Label(ShopConditions.Parse(u.Condition), true);
                    upgrades.Add($"Clint upgrade{from}{(cost.Count > 0 ? " for " + string.Join(" + ", cost) : "")}{(cond.Length > 0 ? $" ({cond})" : "")}");
                }
            if (upgrades.Count > 0) sb.AppendLine("Upgrade: " + string.Join("; ", upgrades) + ".");
        }
        else if (item is Boots boots)
            sb.AppendLine($"Boots: defense +{boots.defenseBonus.Value}, immunity +{boots.immunityBonus.Value}.");
    }

    /// <summary>
    /// Why to keep the item: open bundle slots, a Museum donation still missing, delivery quests and special orders that
    /// take the item. Second list: collection, fishing and harvest goals that count the item when it is picked up or
    /// caught (nothing is handed in), only while some are still missing.
    /// </summary>
    private static (List<string> keep, List<string> counts) KeepFor(string qid, Item? item)
    {
        var keep = new List<string>(BundleNeeds.For(qid));
        var counts = new List<string>();
        try
        {
            if (LibraryMuseum.IsItemSuitableForDonation(qid))
                keep.Add("Museum donation (not donated yet)");
        }
        catch { }

        foreach (var q in Game1.player.questLog)
        {
            if (q == null || q.completed.Value) continue;
            switch (q)
            {
                case ItemDeliveryQuest d when SameQid(d.ItemId.Value, qid):
                    keep.Add($"quest \"{q.GetName()}\": deliver {d.number.Value} to {d.target.Value}"); break;
                case ResourceCollectionQuest c when SameQid(c.ItemId.Value, qid) && c.number.Value > c.numberCollected.Value:
                    counts.Add($"quest \"{q.GetName()}\": collect {c.number.Value - c.numberCollected.Value} more, then talk to {c.target.Value}"); break;
                case FishingQuest f when SameQid(f.ItemId.Value, qid) && f.numberToFish.Value > f.numberFished.Value:
                    counts.Add($"quest \"{q.GetName()}\": catch {f.numberToFish.Value - f.numberFished.Value} more"); break;
                case ItemHarvestQuest h when SameQid(h.ItemId.Value, qid) && h.Number.Value > 0:
                    counts.Add($"quest \"{q.GetName()}\": harvest {h.Number.Value} more"); break;
            }
        }

        if (item != null)
        {
            HashSet<string>? tags = null;
            foreach (var order in Game1.player.team.specialOrders)
            {
                if (order == null || order.questState.Value != SpecialOrderStatus.InProgress) continue;
                foreach (var objective in order.objectives)
                {
                    if (objective == null || objective.IsComplete()) continue;
                    // Collect and Fish objectives count on pickup or catch; the others take the item
                    var (tagSets, onPickup) = objective switch
                    {
                        CollectObjective o => (o.acceptableContextTagSets, true),
                        FishObjective o => (o.acceptableContextTagSets, true),
                        DeliverObjective o => (o.acceptableContextTagSets, false),
                        DonateObjective o => (o.acceptableContextTagSets, false),
                        GiftObjective o => (o.acceptableContextTagSets, false),
                        ShipObjective o => (o.acceptableContextTagSets, false),
                        _ => ((Netcode.NetStringList?)null, false)
                    };
                    if (tagSets == null) continue;
                    tags ??= item.GetContextTags();
                    if (!tagSets.Any(set => set.Split(',').All(part => ItemContextTagManager.DoAnyTagsMatch(part.Split('/'), tags)))) continue;
                    (onPickup ? counts : keep).Add($"special order \"{order.GetName()}\": {objective.GetDescription()} ({objective.GetCount()}/{objective.GetMaxCount()})");
                }
            }
        }
        return (keep, counts);
    }

    private static bool SameQid(string id, string qid)
    {
        string? q = null;
        try { q = ItemRegistry.QualifyItemId(id); } catch { }
        return string.Equals(q ?? CropMath.Qualify(id), qid, StringComparison.OrdinalIgnoreCase);
    }

    private static Item? TryCreate(string qualifiedId, int quality = 0)
    {
        try { return ItemRegistry.Create(qualifiedId, 1, quality); }
        catch { return null; }
    }

    /// <summary>"Plants into X" for seeds and "Grows from Y" for harvests, so the AI knows to call crops.</summary>
    private static void AppendCropLinks(StringBuilder sb, string objectId)
    {
        if (Game1.cropData == null) return;
        if (Game1.cropData.TryGetValue(objectId, out var seedOf) && seedOf != null)
            sb.AppendLine($"Seed: plants into {SafeName(CropMath.Qualify(CropMath.Unqualify(seedOf.HarvestItemId)), seedOf.HarvestItemId ?? "?")}, " +
                          $"{CropLine(seedOf.Seasons?.Select(x => x.ToString()), seedOf.DaysInPhase, seedOf.RegrowDays)}. Use crops for the planting plan.");
        foreach (var kv in Game1.cropData)
        {
            if (kv.Value == null || kv.Key == objectId) continue;
            if (!string.Equals(CropMath.Unqualify(kv.Value.HarvestItemId), objectId, StringComparison.OrdinalIgnoreCase)) continue;
            sb.AppendLine($"Harvest: grown from {SafeName(CropMath.Qualify(kv.Key), kv.Key)}, {CropLine(kv.Value.Seasons?.Select(x => x.ToString()), kv.Value.DaysInPhase, kv.Value.RegrowDays)}.");
        }
        if (Game1.fruitTreeData != null && Game1.fruitTreeData.TryGetValue(objectId, out var tree) && tree != null)
            sb.AppendLine($"Sapling: grows into a fruit tree in {CropMath.SaplingDays} days (any season, 8 clear tiles around it), then fruits in {SeasonsText(tree.Seasons?.Select(x => x.ToString()))}. See crops for the trees.");
    }

    private static string CropLine(IEnumerable<string>? seasons, IEnumerable<int>? daysInPhase, int regrow)
    {
        int growth = daysInPhase == null ? 0 : CropMath.GrowthDays(daysInPhase);
        return $"grows in {SeasonsText(seasons)}, {Plural(growth, "day")}{(regrow > 0 ? $", then every {regrow} days" : "")}";
    }

    /// <summary>The crafting and cooking recipes that make the item: ingredients and how to learn them.</summary>
    private static void AppendRecipeSources(StringBuilder sb, string qid, ShopIndex shops)
    {
        Dictionary<string, string>? tv = null;
        foreach (bool cooking in new[] { false, true })
        {
            var recipes = cooking ? CraftingRecipe.cookingRecipes : CraftingRecipe.craftingRecipes;
            if (recipes == null) continue;
            foreach (var kv in recipes)
            {
                var (ids, count, big) = RecipeMath.Outputs(kv.Value, cooking);
                if (!ids.Any(id => big ? string.Equals(ItemRegistry.ManuallyQualifyItemId(id, "(BC)"), qid, StringComparison.OrdinalIgnoreCase) : SameQid(id, qid))) continue;

                var recipe = new CraftingRecipe(kv.Key, cooking);
                var ingredients = recipe.recipeList.Select(i => $"{i.Value} {recipe.getNameFromIndex(i.Key)}");
                bool known = cooking ? Game1.player.cookingRecipes.ContainsKey(kv.Key) : Game1.player.craftingRecipes.ContainsKey(kv.Key);

                var learn = new List<string>();
                string unlock = ItemFacts.UnlockText(ItemFacts.RecipeUnlock(kv.Value, cooking), Knowledge.Omniscient);
                if (unlock.Length > 0) learn.Add(unlock);
                learn.AddRange(shops.Offers(qid, recipe: true).Select(o => ShopPricing.Text(o, true)));
                if (cooking)
                {
                    try { tv ??= DataLoader.Tv_CookingChannel(Game1.content); } catch { tv = new(); }
                    foreach (var show in tv)
                        if (show.Value.Split('/')[0] == kv.Key && int.TryParse(show.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int week))
                            learn.Add(ItemFacts.TvText(week));
                }
                string how = known ? "you know it" : learn.Count > 0 ? "learn: " + string.Join("; ", learn) : "learn: another way (event, letter, quest or gift)";
                sb.AppendLine($"{(cooking ? "Cooking" : "Crafting")} recipe \"{recipe.DisplayName}\": {string.Join(", ", ingredients)} -> {count} {SafeName(qid, qid)}; {how}.");
            }
        }
    }

    private static void AppendRecipes(StringBuilder sb, string label, string asset, bool cooking, string itemId, int category)
    {
        var found = new List<(string name, int count, bool viaCategory, bool known)>();
        try
        {
            var recipes = Game1.content.Load<Dictionary<string, string>>(asset);
            foreach (var kv in recipes)
            {
                var (count, viaCategory) = RecipeMath.IngredientCount(kv.Value, itemId, category);
                if (count <= 0) continue;
                bool known = cooking ? Game1.player.cookingRecipes.ContainsKey(kv.Key) : Game1.player.craftingRecipes.ContainsKey(kv.Key);
                found.Add((kv.Key, count, viaCategory, known));
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"{label} recipes: could not be read ({ex.GetType().Name}).");
            return;
        }
        if (found.Count == 0) { sb.AppendLine($"{label} recipes: none use it."); return; }
        var shown = found.OrderByDescending(f => f.known).ThenBy(f => f.name).Take(MaxRecipes)
            .Select(f => $"{f.name} (x{f.count}{(f.viaCategory ? ", any item of its category" : "")}{(f.known ? ", you know it" : "")})");
        sb.AppendLine($"{label} recipes using it: {string.Join(", ", shown)}{(found.Count > MaxRecipes ? $", and {found.Count - MaxRecipes} more" : "")}.");
    }
}
