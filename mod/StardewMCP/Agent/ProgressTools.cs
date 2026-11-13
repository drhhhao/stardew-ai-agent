using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TokenizableStrings;

namespace StardewMCP.Agent;

/// <summary>
/// progress: skills, next unlocks, professions, tool upgrades, mines, slayer goals and museum in one view, read
/// from the farmer and the game data (Data/CraftingRecipes, CookingRecipes, Tools, MonsterSlayerQuests,
/// MuseumRewards). Not omniscient: it shows only the player's own state and what the game itself lists.
/// The wording and arithmetic are in <see cref="ProgressMath"/>.
/// </summary>
public static class ProgressTools
{
    private const int MaxUnlockNames = 8;

    public static List<ToolDef> All() => new()
    {
        new()
        {
            Name = "progress",
            Description = "Your progress in one view: each skill's level, XP and XP to the next level, the recipes the next levels unlock, " +
                          "your professions and the choices ahead (with the level-10 options of each level-5 one), the next tool upgrade at Clint's " +
                          "with its price and the bars you have (pack and chests) and any tool being upgraded, the deepest Mines and Skull Cavern floors " +
                          "and the elevator, the Adventurer's Guild monster slayer goals, and the museum donations with the next rewards. Costs no game time.",
            Schema = AgentTools.Obj(new { }),
            Instant = true, AppendStatus = false,
            Run = (a, c) => Report(c, "progress", ProgressReport)
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

    private static (bool ok, string text) ProgressReport()
    {
        var p = Game1.player;
        if (p == null) return (false, "No farmer is loaded yet.");
        var sb = new StringBuilder();
        var stored = StoredCounts();

        // Each part on its own so one unreadable data file still leaves the rest.
        Section(sb, "Skills", () => SkillsLine(p));
        Section(sb, "Next unlocks", () => UnlocksLine(p));
        Section(sb, "Professions", () => ProfessionsText(p));
        Section(sb, "Tools", () => ToolsLine(p, stored));
        Section(sb, "Mines", () => ProgressMath.MinesText(p.deepestMineLevel, MineShaft.lowestLevelReached));
        Section(sb, "Slayer goals", () => SlayerLine());
        Section(sb, "Museum", () => MuseumLine(p));
        return (true, sb.ToString().TrimEnd());
    }

    private static void Section(StringBuilder sb, string name, Func<string> build)
    {
        string text;
        try { text = build(); }
        catch (Exception ex) { text = $"{name}: could not read ({ex.GetType().Name}: {ex.Message})."; }
        if (text.Length > 0) sb.AppendLine(text);
    }

    // ------------------------------------------------------------------ skills

    /// <summary>Skill indices 0-4 as the game numbers them (Farmer.GetSkillLevel): Farming, Fishing, Foraging, Mining, Combat.</summary>
    private static readonly int[] SkillIds = { 0, 1, 2, 3, 4 };

    private static string SkillsLine(Farmer p) =>
        "Skills: " + string.Join(" | ", SkillIds.Select(s =>
        {
            int level = p.GetUnmodifiedSkillLevel(s);
            int xp = s < p.experiencePoints.Length ? p.experiencePoints[s] : 0;
            return ProgressMath.SkillText(Professions.SkillName(s), level, xp, p.GetSkillLevel(s) - level);
        }));

    /// <summary>Recipes a skill level teaches (CraftingRecipe.TryParseLevelRequirement, as LevelUpMenu) that the farmer
    /// does not know yet: the nearest level per skill, and any of a level already reached (taught at the next level-up screen).</summary>
    private static string UnlocksLine(Farmer p)
    {
        var bySkill = new Dictionary<int, SortedDictionary<int, List<string>>>();
        void Scan(Dictionary<string, string>? recipes, bool cooking)
        {
            if (recipes == null) return;
            foreach (var kv in recipes)
            {
                if (!CraftingRecipe.TryParseLevelRequirement(kv.Key, kv.Value, cooking, out int skill, out int level, logErrors: false)) continue;
                if (skill < 0 || skill > 4) continue;
                bool known = cooking ? p.cookingRecipes.ContainsKey(kv.Key) : p.craftingRecipes.ContainsKey(kv.Key);
                if (known) continue;
                if (!bySkill.TryGetValue(skill, out var levels)) bySkill[skill] = levels = new SortedDictionary<int, List<string>>();
                if (!levels.TryGetValue(level, out var names)) levels[level] = names = new List<string>();
                var name = new CraftingRecipe(kv.Key, cooking).DisplayName ?? kv.Key;
                names.Add(cooking ? name + " (cooking)" : name);
            }
        }
        Scan(CraftingRecipe.craftingRecipes, false);
        Scan(CraftingRecipe.cookingRecipes, true);

        var parts = new List<string>();
        foreach (int s in SkillIds)
        {
            if (!bySkill.TryGetValue(s, out var levels)) continue;
            int current = p.GetUnmodifiedSkillLevel(s);
            var due = levels.Where(l => l.Key <= current).SelectMany(l => l.Value).ToList();
            var next = levels.FirstOrDefault(l => l.Key > current);
            var text = new List<string>();
            if (due.Count > 0) text.Add($"{Names(due)} (level reached: taught at the level-up screen tonight)");
            if (next.Value != null) text.Add($"{next.Key} {Names(next.Value)}");
            if (text.Count > 0) parts.Add($"{Professions.SkillName(s)} {string.Join("; ", text)}");
        }
        return "Next unlocks: " + (parts.Count > 0 ? string.Join(" | ", parts) : "every skill recipe is known.");
    }

    private static string Names(List<string> names) =>
        string.Join(", ", names.Take(MaxUnlockNames)) + (names.Count > MaxUnlockNames ? $" +{names.Count - MaxUnlockNames} more" : "");

    // ------------------------------------------------------------------ professions

    private static string ProfessionsText(Farmer p)
    {
        var lines = new List<string>();
        var owned = p.professions.Where(id => id >= 0 && id < 30).Select(Professions.Text).ToList();
        lines.Add("Professions: " + (owned.Count > 0 ? string.Join(" | ", owned) : "none yet (first choice at skill level 5)"));

        // The level counts as soon as the XP is gained, but the choice comes only at that night's level-up screen
        // (then it is a pending choice, listed below instead).
        var pendingSkills = AgentTools.Rt.PendingProfessions.Select(c => c.X).ToHashSet();
        foreach (int s in SkillIds)
        {
            if (pendingSkills.Contains(s)) continue;
            int level = p.GetUnmodifiedSkillLevel(s), b = s * 6;
            bool has10 = Enumerable.Range(b + 2, 4).Any(id => p.professions.Contains(id));
            if (has10) continue;
            int five = p.professions.Contains(b) ? b : p.professions.Contains(b + 1) ? b + 1 : -1;
            if (five < 0)
                lines.Add($"- {Professions.SkillName(s)} {When(level, 5)} you will choose: {Professions.Text(b)} (then at 10: {Professions.Subtree(b)}) OR " +
                          $"{Professions.Text(b + 1)} (then at 10: {Professions.Subtree(b + 1)})");
            else
            {
                var (x, y) = ProgressMath.Level10Of(five);
                lines.Add($"- {Professions.SkillName(s)} {When(level, 10)} you will choose: {Professions.Text(x)} OR {Professions.Text(y)}");
            }
        }
        var pending = Professions.PendingText(AgentTools.Rt, p);
        if (pending.Length > 0) lines.Add(pending);
        return string.Join("\n", lines);
    }

    private static string When(int level, int at) => level >= at ? $"{at} reached, tonight" : $"at {at}";

    // ------------------------------------------------------------------ tools

    /// <summary>Items in every chest and fridge, by qualified id (a Junimo chest's shared inventory counted once; no Mini-Shipping Bins or Hoppers).</summary>
    private static Dictionary<string, int> StoredCounts()
    {
        var counts = new Dictionary<string, int>();
        var seen = new HashSet<IInventory>(ReferenceEqualityComparer.Instance);
        void Add(Chest chest)
        {
            var items = chest.GetItemsForPlayer();
            if (items == null || !seen.Add(items)) return;
            foreach (var it in items)
                if (it != null)
                    counts[it.QualifiedItemId] = counts.GetValueOrDefault(it.QualifiedItemId) + it.Stack;
        }
        Utility.ForEachLocation(loc =>
        {
            if (loc.GetFridge(false) is { } fridge) Add(fridge);
            foreach (var obj in loc.objects.Values)
                if (obj is Chest chest && ChestLedger.IsStorage(chest)) Add(chest);
            return true;
        });
        return counts;
    }

    private static int PackCount(Farmer p, string qid) => p.Items.Where(i => i != null && i.QualifiedItemId == qid).Sum(i => i.Stack);

    private static string ItemName(string? id) =>
        string.IsNullOrEmpty(id) ? "" : ItemRegistry.GetData(id)?.DisplayName ?? id;

    /// <summary>
    /// The upgrade Clint offers next for each tool the farmer owns: ShopBuilder.GetToolUpgradeData's rule (the conventional
    /// upgrade from ConventionalUpgradeFrom, then UpgradeFrom entries; the first whose condition holds and whose required tool
    /// the farmer has), except that a tool kept in a chest counts too. Price as ItemQueryResolver.TOOL_UPGRADES.
    /// </summary>
    private static string ToolsLine(Farmer p, Dictionary<string, int> stored)
    {
        var parts = new List<string>();
        var upgrading = p.toolBeingUpgraded.Value;
        if (Game1.toolData != null)
        {
            foreach (var kv in Game1.toolData)
            {
                var data = kv.Value;
                if (data == null || upgrading?.ItemId == kv.Key) continue;
                var candidates = new List<StardewValley.GameData.Tools.ToolUpgradeData>();
                if (data.ConventionalUpgradeFrom != null)
                    candidates.Add(new()
                    {
                        RequireToolId = data.ConventionalUpgradeFrom,
                        Price = StardewValley.Internal.ShopBuilder.GetToolUpgradeConventionalPrice(data.UpgradeLevel),
                        TradeItemId = ConventionalBar(data.UpgradeLevel),
                        TradeItemAmount = 5
                    });
                if (data.UpgradeFrom != null) candidates.AddRange(data.UpgradeFrom.Where(u => u != null));

                foreach (var u in candidates)
                {
                    if (u.Condition != null && !GameStateQuery.CheckConditions(u.Condition, p.currentLocation, p)) continue;
                    string? from = u.RequireToolId == null ? null : ItemRegistry.QualifyItemId(u.RequireToolId) ?? u.RequireToolId;
                    bool inPack = from != null && PackCount(p, from) > 0;
                    bool inChest = from != null && stored.GetValueOrDefault(from) > 0;
                    if (from != null && !inPack && !inChest) continue;

                    var target = ItemRegistry.Create("(T)" + kv.Key);
                    int price = u.Price > -1 ? u.Price : Math.Max(0, target.salePrice());
                    var cost = $"{ProgressMath.N(price)}g";
                    if (!string.IsNullOrEmpty(u.TradeItemId))
                    {
                        var bar = ItemRegistry.QualifyItemId(u.TradeItemId) ?? u.TradeItemId;
                        cost += $" + {u.TradeItemAmount} {ItemName(bar)} ({ProgressMath.Have(PackCount(p, bar), stored.GetValueOrDefault(bar))})";
                    }
                    var what = from != null ? $"{ItemName(from)} -> {target.DisplayName}" : target.DisplayName;
                    parts.Add($"{what} {cost}{(from != null && !inPack ? " (the tool is in a chest: carry it to Clint)" : "")}");
                    break;
                }
            }
        }

        string busy;
        if (upgrading == null) busy = "upgrading: none";
        else
        {
            int days = p.daysLeftForToolUpgrade.Value;
            busy = days > 0
                ? $"upgrading: {upgrading.DisplayName}, ready in {days} day{(days == 1 ? "" : "s")} (Clint takes no other tool until you collect it)"
                : $"upgrading: {upgrading.DisplayName} is READY, collect it from Clint at the Blacksmith";
        }
        var list = parts.Count > 0 ? string.Join(" | ", parts) : "no upgrade available";
        return $"Tools (Clint's Blacksmith, 2 days each, one at a time): {list} | {busy}";
    }

    /// <summary>ShopBuilder.GetToolUpgradeConventionalTradeItem (private in the game): Copper, Iron, Gold, Iridium Bar.</summary>
    private static string ConventionalBar(int level) => level switch { 2 => "335", 3 => "336", 4 => "337", _ => "334" };

    // ------------------------------------------------------------------ slayer goals

    private static string SlayerLine()
    {
        var goals = DataLoader.MonsterSlayerQuests(Game1.content);
        var list = new List<ProgressMath.SlayerGoal>();
        foreach (var kv in goals)
        {
            var g = kv.Value;
            if (g == null) continue;
            int killed = g.Targets?.Sum(t => Game1.stats.getMonstersKilled(t)) ?? 0;
            var name = string.IsNullOrEmpty(g.DisplayName) ? kv.Key : TokenParser.ParseText(g.DisplayName);
            list.Add(new(name, killed, g.Count, ItemName(g.RewardItemId), RewardCollected(Game1.player, kv.Key, g)));
        }
        return ProgressMath.SlayerText(list);
    }

    /// <summary>
    /// Whether Gil has given a goal's reward. The game sets "Gil_&lt;id&gt;" only when a reward ITEM is taken
    /// (AdventureGuild.OnRewardCollected); a goal without one (FlameSpirits: dialogue + the Gil_Telephone flag) is
    /// collected once its flag is set, or as soon as it is done when it has no flag either.
    /// </summary>
    private static bool RewardCollected(Farmer p, string id, StardewValley.GameData.MonsterSlayerQuestData g) =>
        ProgressMath.SlayerRewardCollected(AdventureGuild.HasCollectedReward(p, id), g.RewardItemId != null,
            new[] { g.RewardFlag, g.RewardFlagAll, g.RewardDialogueFlag }, f => p.mailReceived.Contains(f));

    // ------------------------------------------------------------------ museum

    private static string MuseumLine(Farmer p)
    {
        if (Game1.getLocationFromName("ArchaeologyHouse") is not LibraryMuseum museum) return "Museum: not available.";
        var rewards = DataLoader.MuseumRewards(Game1.content);
        var counts = museum.GetDonatedByContextTag(rewards);
        int total = LibraryMuseum.totalArtifacts;
        int donated = museum.museumPieces.Length;
        int Donated(string tag) => counts.GetValueOrDefault(tag);
        // CanCollectReward with every count met tells whether this farmer may still get the reward at all.
        var allMet = counts.Keys.ToDictionary(k => k, _ => int.MaxValue);

        var waiting = new List<string>();
        var next = new List<(int missing, string text)>();
        foreach (var kv in rewards)
        {
            var r = kv.Value;
            if (r?.TargetContextTags == null) continue;
            if (!museum.CanCollectReward(r, kv.Key, p, allMet)) continue;
            string name = "a special reward";
            if (r.RewardItemId != null)
            {
                var item = ItemRegistry.Create(r.RewardItemId, r.RewardItemCount);
                item.IsRecipe = r.RewardItemIsRecipe;
                item.specialItem = r.RewardItemIsSpecial;
                if (p.mailReceived.Contains(museum.getRewardItemKey(item))) continue;
                // An object's DisplayName already ends in " (Recipe)" when IsRecipe is set (Object.DisplayName).
                name = item.DisplayName + (r.RewardItemIsRecipe && item is not StardewValley.Object ? " (Recipe)" : "") + (r.RewardItemCount > 1 ? $" x{r.RewardItemCount}" : "");
            }
            var needs = r.TargetContextTags.Select(t => new ProgressMath.MuseumNeed(t.Tag ?? "", t.Count)).ToList();
            int missing = ProgressMath.Missing(needs, Donated, total);
            if (missing == 0) waiting.Add(name);
            else next.Add((missing, $"{name}: {ProgressMath.NeedsText(needs, Donated, total, id => ItemName(id))}"));
        }

        var sb = new StringBuilder($"Museum: {donated}/{total} donated ({Donated("item_type_arch")} artifacts, {Donated("item_type_minerals")} minerals)");
        if (next.Count > 0)
            sb.Append(". Next rewards: ").Append(string.Join(" | ", next.OrderBy(n => n.missing).Take(4).Select(n => n.text)));
        if (waiting.Count > 0)
            sb.Append(". Earned, collect from Gunther: ").Append(string.Join(", ", waiting));
        return sb.Append('.').ToString();
    }
}
