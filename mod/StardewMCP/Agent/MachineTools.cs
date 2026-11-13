using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData;
using StardewValley.Extensions;
using StardewValley.GameData.Machines;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.Objects.Trinkets;
using StardewValley.TokenizableStrings;
using SObject = StardewValley.Object;

namespace StardewMCP.Agent;

/// <summary>
/// machines: what each machine makes from an item (item=), one machine's rules (machine=) and the state of every placed
/// machine on the farm (no args). Read from Data/Machines with the game's own MachineDataUtility, so outputs, prices and
/// times are the game's (modded machines too). Instant, costs no game time, not omniscient.
/// The arithmetic and wording live in <see cref="MachineMath"/>.
/// </summary>
public static class MachineTools
{
    private const int MaxCandidates = 12;
    /// <summary>RANDOM queries are left to <see cref="MachineMath.RandomChance"/>, so a lookup is not a dice roll.</summary>
    private static readonly HashSet<string> IgnoreRandom = new(StringComparer.OrdinalIgnoreCase) { "RANDOM", "SYNCED_RANDOM" };

    public static List<ToolDef> All() => new()
    {
        new()
        {
            Name = "machines",
            Description = "Machines (Keg, Preserves Jar, Furnace, Cask...), read from the game's own data. item=X: every machine that takes X, with the output, " +
                          "its sell price (with your professions), the time, the gain per item and per machine-day, and the best pick. machine=X: what that machine takes, " +
                          "makes and needs as fuel. No arguments: every machine placed on the farm, in its buildings and in the farmhouse, grouped READY / Working (with the " +
                          "ready time) / Empty, with tiles. Costs no game time.",
            Schema = AgentTools.Obj(new
            {
                item = AgentTools.Str("Item to process, e.g. Blueberry or Copper Ore"),
                machine = AgentTools.Str("Machine to explain, e.g. Keg or Preserves Jar")
            }),
            Instant = true, AppendStatus = false,
            Run = RunMachines
        }
    };

    private static IEnumerator RunMachines(JsonElement a, CallContext c)
    {
        List<Placed>? placed = null;
        Exception? error = null;
        yield return ScanHere((list, ex) => { placed = list; error = ex; });
        yield return Report(c, () => Machines(a, placed ?? new List<Placed>(), error));
    }

    /// <summary>The best machine outputs for an item, one short line each ("Keg -> Blueberry Wine 150g, 6.25 days, +100g per item, 16g per machine-day"); empty when none.</summary>
    public static IReadOnlyList<string> BestOutputs(string qualifiedItemId, int count)
    {
        try
        {
            if (count <= 0 || ItemRegistry.Create(qualifiedItemId, 1, 0, allowNull: true) is not { } input) return Array.Empty<string>();
            return Options(input)
                .Where(o => o.Priced && o.RuleChance >= 1 && o.Gain > 0)
                .OrderByDescending(o => o.GainPerItem ?? 0).ThenByDescending(o => o.PerDay)
                .Take(count)
                .Select(o => $"{o.Machine}{CountText(o)} -> {o.Output} {o.ValueText}, {MachineMath.Duration(o.Minutes, o.WholeDays)}, {GainText(o)}, {Num(o.PerDay)}g per machine-day")
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// One line for farm_summary ("Machines: 4 ready, 6 working, 2 empty (machines() for detail)."), handed to
    /// <paramref name="done"/>; empty when no machine is placed. Empty machines that fill by themselves (lightning rods,
    /// bee houses in winter) are not counted: they are no chore. A coroutine, since a farmhand screen asks the host screen.
    /// </summary>
    public static IEnumerator SummaryLine(Action<string> done)
    {
        List<Placed>? placed = null;
        yield return ScanHere((list, _) => placed = list);
        try
        {
            var spots = (placed ?? new List<Placed>()).Select(p => p.Spot).ToList();
            done(MachineMath.CountsLine(spots.Count(s => s.State == MachineState.Ready), spots.Count(s => s.State == MachineState.Working),
                spots.Count(s => s.State == MachineState.Empty && !s.SelfFilling)));
        }
        catch
        {
            done("");
        }
    }

    /// <summary>Runs a report; a data mismatch (a modded or changed game file) becomes a clear failure, not a crash.</summary>
    private static IEnumerator Report(CallContext c, Func<(bool ok, string text)> build)
    {
        (bool ok, string text) r;
        try { r = build(); }
        catch (Exception ex) { r = (false, $"machines could not read the game data ({ex.GetType().Name}: {ex.Message}). Tell the player; the game data may differ from what the mod expects."); }
        if (r.ok) c.Result = r.text; else c.Fail(r.text);
        yield break;
    }

    /// <summary>The report asked for; a failed scan of the placed machines only fails the farm overview (item= and machine= just lose the placed counts).</summary>
    private static (bool ok, string text) Machines(JsonElement a, List<Placed> placed, Exception? scanError)
    {
        var item = AgentTools.S(a, "item").Trim();
        var machine = AgentTools.S(a, "machine").Trim();
        if (item.Length > 0) return ItemReport(item, placed);
        if (machine.Length > 0) return MachineReport(machine, placed);
        if (scanError != null) throw scanError;
        return (true, FarmReport(placed));
    }

    private static string Num(double v, string format = "0.#") => v.ToString(format, CultureInfo.InvariantCulture);

    private static Dictionary<string, MachineData> MachineData() => DataLoader.Machines(Game1.content);

    private static string ItemName(string? id)
    {
        if (string.IsNullOrEmpty(id)) return "?";
        try { return ItemRegistry.GetData(id)?.DisplayName ?? id; }
        catch { return id; }
    }

    /// <summary>The shown name of an item, or null when the game has no such item (for an id context tag, which is lowercased).</summary>
    private static string? KnownItemName(string qualifiedId)
    {
        try { return ItemRegistry.GetData(qualifiedId)?.DisplayName; }
        catch { return null; }
    }

    private static int SellPrice(Item? item) => item is SObject o ? o.sellToStorePrice() : 0;

    // ------------------------------------------------------------------ item=

    /// <summary>One way to process an item: a machine rule that takes it.</summary>
    private sealed class Option
    {
        public string MachineId = "", Machine = "", Output = "", ValueText = "", Note = "", Fuel = "";
        public int Count = 1, Minutes;
        public bool WholeDays, Repeats, Priced;
        public double Value, InputValue, FuelCost, RuleChance = 1;
        public double Gain => Value - (Repeats ? 0 : InputValue * Count) - FuelCost;
        public double? GainPerItem => Repeats || !Priced ? null : Gain / Count;
        public double PerDay => Priced ? MachineMath.PerMachineDay(Gain, Minutes) : 0;
    }

    /// <summary>Every machine rule that takes the item, in Data/Machines order (the first matching rule of a machine wins).</summary>
    private static List<Option> Options(Item input)
    {
        var list = new List<Option>();
        var who = Game1.player;
        var loc = Game1.getFarm();
        foreach (var (id, data) in MachineData())
        {
            if (data?.OutputRules == null || data.OutputRules.Count == 0) continue;
            SObject? machine;
            try { machine = ItemRegistry.Create<SObject>(id, 1, 0, allowNull: true); }
            catch { machine = null; }
            if (machine == null) continue;
            machine.Location = loc;
            foreach (var rule in data.OutputRules)
            {
                var trigger = MatchTrigger(rule, input, who, loc);
                if (trigger == null) continue;
                double chance = ConditionChance(trigger.Condition, loc, who, null, input);
                var option = BuildOption(id, data, machine, rule, trigger, input, who, loc);
                if (option != null)
                {
                    option.RuleChance = chance;
                    list.Add(option);
                }
                if (chance >= 1) break;
            }
        }
        return list;
    }

    /// <summary>
    /// The chance a condition passes: 0 when its other queries fail now, else the chance of its RANDOM parts. An ANY query
    /// passes when one of its branches does, each with its own roll (the Loom's "ITEM_QUALITY Input 2 2, RANDOM 0.5" branches).
    /// </summary>
    private static double ConditionChance(string? condition, GameLocation loc, Farmer who, Item? target, Item input)
    {
        if (string.IsNullOrWhiteSpace(condition)) return 1;
        double chance = 1;
        foreach (var query in MachineMath.SplitQueries(condition))
        {
            if (query.StartsWith("ANY ", StringComparison.OrdinalIgnoreCase))
            {
                double none = 1;
                foreach (var branch in MachineMath.SplitQuoted(query.Substring(4)))
                    none *= 1 - (GameStateQuery.CheckConditions(branch, loc, who, target, input, null, IgnoreRandom) ? MachineMath.RandomChance(branch) : 0);
                chance *= 1 - none;
            }
            else chance *= GameStateQuery.CheckConditions(query, loc, who, target, input, null, IgnoreRandom) ? MachineMath.RandomChance(query) : 0;
        }
        return chance;
    }

    /// <summary>MachineDataUtility.CanApplyOutput for an item dropped in, without the stack-size check and with RANDOM conditions counted as passing.</summary>
    private static MachineOutputTriggerRule? MatchTrigger(MachineOutputRule rule, Item input, Farmer who, GameLocation loc)
    {
        if (rule.Triggers == null) return null;
        foreach (var t in rule.Triggers)
        {
            if (t == null || !t.Trigger.HasFlag(MachineOutputTrigger.ItemPlacedInMachine)) continue;
            if (t.Condition != null && !GameStateQuery.CheckConditions(t.Condition, loc, who, null, input, null, IgnoreRandom)) continue;
            if (t.RequiredItemId != null && !ItemRegistry.HasItemId(input, t.RequiredItemId)) continue;
            if (t.RequiredTags is { Count: > 0 } && !ItemContextTagManager.DoAllTagsMatch(t.RequiredTags, input.GetContextTags())) continue;
            return t;
        }
        return null;
    }

    private static Option? BuildOption(string machineId, MachineData data, SObject machine, MachineOutputRule rule, MachineOutputTriggerRule trigger,
        Item input, Farmer who, GameLocation loc)
    {
        var outputs = rule.OutputItem;
        if (outputs == null || outputs.Count == 0) return null;
        var opt = new Option
        {
            MachineId = machineId,
            Machine = machine.DisplayName,
            Count = Math.Max(1, trigger.RequiredCount),
            Repeats = rule.Triggers.Any(t => t != null && t.Trigger.HasFlag(MachineOutputTrigger.OutputCollected)),
            InputValue = SellPrice(input)
        };
        if (data.AdditionalConsumedItems is { Count: > 0 } fuel)
        {
            opt.Fuel = string.Join(" + ", fuel.Select(f => $"{f.RequiredCount} {ItemName(f.ItemId)}"));
            opt.FuelCost = fuel.Sum(f => SellPrice(ItemRegistry.Create(f.ItemId, 1, 0, allowNull: true)) * (double)f.RequiredCount);
        }

        var pass = outputs.Select(o => o == null ? 0 : ConditionChance(o.Condition, loc, who, null, input)).ToList();
        var chances = MachineMath.OutputChances(pass, rule.UseFirstValidOutput);

        bool specific = trigger.RequiredItemId != null || trigger.RequiredTags is { Count: > 0 };
        var parts = new List<(string name, double chance, double each, double stack)>();
        Item? first = null;
        int? overrideMinutes = null;
        for (int i = 0; i < outputs.Count; i++)
        {
            if (chances[i] <= 0) continue;
            var (item, note, stack, minutes) = Produce(machine, outputs[i], input, who, specific);
            overrideMinutes ??= minutes;
            if (item == null)
            {
                if (note.Length > 0 && !opt.Note.Contains(note)) opt.Note = note;
                continue;
            }
            first ??= item;
            double each = SellPrice(item);
            parts.Add((item.DisplayName + (note.Length > 0 ? $" ({note})" : ""), chances[i], each, stack));
            opt.Value += chances[i] * each * stack;
        }
        if (parts.Count == 0 && opt.Note.Length == 0) return null;
        opt.Priced = parts.Count > 0;

        if (parts.Count == 0) opt.Output = opt.Note;
        else if (parts.Count == 1 && parts[0].chance >= 0.999)
        {
            var p = parts[0];
            opt.Output = p.name;
            opt.ValueText = Math.Abs(p.stack - 1) < 0.001 ? $"{Num(p.each, "0")}g" : $"x{Num(p.stack)} ({Num(p.each, "0")}g each)";
        }
        else
        {
            double total = parts.Sum(p => p.chance);
            opt.Output = string.Join(", ", parts.Select(p => $"{MachineMath.Percent(p.chance)} {p.name}{(Math.Abs(p.stack - 1) < 0.001 ? "" : $" x{Num(p.stack)}")} {Num(p.each, "0")}g"))
                         + (total < 0.999 ? $", else nothing ({MachineMath.Percent(1 - total)})" : "");
            opt.ValueText = $"(avg {Num(opt.Value, "0")}g)";
        }

        // Object.OutputMachine: an output method may set the time; days count to 6:00; then Data/Machines ReadyTimeModifiers
        int baseMinutes;
        if (overrideMinutes is >= 0 and < 999999) baseMinutes = overrideMinutes.Value;
        else if (rule.DaysUntilReady >= 0) { baseMinutes = rule.DaysUntilReady * MachineMath.MinutesPerDay; opt.WholeDays = true; }
        else baseMinutes = Math.Max(0, rule.MinutesUntilReady);
        if (overrideMinutes is >= 0 and < 999999 && outputs.Any(o => o?.OutputMethod?.EndsWith("OutputCask", StringComparison.Ordinal) == true)) opt.WholeDays = true;
        opt.Minutes = (int)Utility.ApplyQuantityModifiers(baseMinutes, data.ReadyTimeModifiers, data.ReadyTimeModifierMode, loc, who, first, input);
        if (data.OnlyCompleteOvernight)
        {
            // Object.minutesElapsed: such a machine (incubators) is only ready at the 6:00 after its time runs out
            opt.WholeDays = true;
            opt.Minutes = MachineMath.WholeDays(opt.Minutes) * MachineMath.MinutesPerDay;
        }
        return opt;
    }

    /// <summary>
    /// The item one output entry makes from the input (MachineDataUtility.GetOutputItem with probe), its average stack and a
    /// time set by the output method. Output methods that would roll a hidden result (geode contents) or need a placed
    /// machine are described instead of run. Random parts are averaged, not rolled: the stack and its RANDOM StackModifiers,
    /// the Seed Maker's rare Mixed / Ancient Seeds.
    /// </summary>
    private static (Item? item, string note, double stack, int? minutes) Produce(SObject machine, MachineItemOutput o, Item input, Farmer who, bool specific)
    {
        if (machine.QualifiedItemId == "(BC)156")
        {
            // Object.CheckForActionOnMachine "(BC)156": the egg hatches into a slime next to the incubator; nothing to sell
            string slime = MachineMath.SlimeFromEgg(input.QualifiedItemId);
            return (null, slime.Length > 0 ? $"hatches a {slime} slime (keep slimes in a Slime Hutch)" : "hatches a slime (keep slimes in a Slime Hutch)", 1, null);
        }
        string method = "";
        if (o.OutputMethod != null)
        {
            int colon = o.OutputMethod.LastIndexOf(':');
            method = (colon >= 0 ? o.OutputMethod.Substring(colon + 1) : o.OutputMethod).Trim();
        }
        if (method == "OutputCask")
        {
            // Cask.OutputCask: the same item, aged one quality step at a time; the cask must stand in the Cellar
            float rate = 1f;
            if (o.CustomData != null && o.CustomData.TryGetValue("AgingMultiplier", out var raw)
                && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0) rate = parsed;
            if (input.Quality >= 4) return (null, "already Iridium quality", 1, null);
            var aged = input.getOne();
            aged.Quality = 4;
            int days = (int)Math.Ceiling(new Cask().GetDaysForQuality(input.Quality) / rate);
            return (aged, "Iridium quality, Cellar only", 1, days * MachineMath.MinutesPerDay);
        }
        switch (method)
        {
            case "":
            case "OutputDeconstructor":
                break; // run with probe: no side effects, nothing hidden
            case "OutputSeedMaker":
            {
                // Object.OutputSeedMaker: the seed of the crop that grows the input, 1-3 (avg 2); a roll by tile and time
                // gives Ancient Seeds (0.5%) or Mixed Seeds (2%) instead, which a probe could land on
                if (!input.HasTypeObject()) return (null, "", 1, null);
                string? seedId = Game1.cropData.FirstOrDefault(kv => kv.Value != null && ItemRegistry.HasItemId(input, kv.Value.HarvestItemId)).Key;
                var seeds = seedId == null ? null : ItemRegistry.Create("(O)" + seedId, 1, 0, allowNull: true);
                return seeds == null ? (null, "", 1, null) : (seeds, "1-3, sometimes Mixed or Ancient Seeds", 2.0, null);
            }
            case "OutputGeodeCrusher":
                // not run: it would reveal what the next geode holds
                return (null, Utility.IsGeode(input, disallow_special_geodes: true) ? MachineMath.MethodText(o.OutputMethod) : "", 1, null);
            case "OutputIncubator":
                var animal = FarmAnimal.GetAnimalDataFromEgg(input, machine.Location);
                if (animal == null) return (null, "", 1, null);
                return (null, $"hatches a {TokenParser.ParseText(animal.DisplayName)} (in a {animal.House})", 1, animal.IncubationTime > 0 ? animal.IncubationTime : 9000);
            case "OutputAnvil":
                return (null, input is Trinket trinket && trinket.GetTrinketData()?.CanBeReforged == true ? MachineMath.MethodText(o.OutputMethod) : "", 1, null);
            default:
                // a modded output method is not run (it may change the world); described only when the rule names its input, else every item would match
                return (null, specific ? MachineMath.MethodText(o.OutputMethod) : "", 1, null);
        }

        // the stack modifiers are left out of the probe (they may roll, like the Bone Mill's "RANDOM .1" double) and averaged below
        var stackMods = o.StackModifiers;
        Item? item;
        int? minutes;
        try
        {
            if (stackMods is { Count: > 0 }) o.StackModifiers = null;
            item = MachineDataUtility.GetOutputItem(machine, o, input, who, true, out minutes);
        }
        finally { o.StackModifiers = stackMods; }
        if (item == null) return (null, "", 1, null);
        double stack = o.MinStack > 0 && o.MaxStack > o.MinStack ? (o.MinStack + o.MaxStack) / 2.0 : item.Stack;
        if (stackMods is { Count: > 0 })
        {
            var loc = machine.Location;
            var mods = stackMods.Where(m => m != null).Select(m => (
                m.Modification.ToString(),
                (double)(m.RandomAmount is { Count: > 0 } amounts ? amounts.Average() : m.Amount),
                ConditionChance(m.Condition, loc, who, item, input))).ToList();
            stack = MachineMath.ExpectedQuantity(stack, mods, o.StackModifierMode.ToString());
        }
        return (item, "", stack, minutes);
    }

    private static string CountText(Option o) => o.Count > 1 ? $" ({o.Count} at once)" : "";

    private static string GainText(Option o) =>
        o.Repeats ? "input not used up, repeats" : o.GainPerItem is double g ? $"{(g >= 0 ? "+" : "")}{Num(g)}g per item" : "";

    private static (bool ok, string text) ItemReport(string query, List<Placed> placedList)
    {
        var (input, held, failure, note) = ResolveInput(query);
        if (input == null) return (false, failure);
        string name = input.DisplayName;
        var options = Options(input);
        var placed = placedList.GroupBy(p => p.MachineId).ToDictionary(g => g.Key, g => g.Count());
        string quality = input.Quality switch { 1 => "silver", 2 => "gold", 4 => "iridium", _ => "" };

        var sb = new StringBuilder();
        string about = string.Join(", ", new[] { quality.Length > 0 ? quality + " quality" : "", held ? "as you hold it" : "", $"you get {SellPrice(input)}g each when sold" }.Where(x => x.Length > 0));
        sb.AppendLine($"{name} ({about}) in machines. " +
                      $"Sell prices include professions ({ArtisanText()}).");
        if (note.Length > 0) sb.AppendLine(note);
        if (options.Count == 0)
        {
            sb.Append($"No machine takes {name}.");
            return (true, sb.ToString());
        }
        foreach (var o in options.OrderByDescending(o => o.Priced).ThenByDescending(o => o.PerDay))
        {
            var line = new StringBuilder($"- {o.Machine}{CountText(o)}");
            if (o.RuleChance < 1) line.Append($" ({MachineMath.Percent(o.RuleChance)} of loads)");
            if (placed.TryGetValue(o.MachineId, out int n)) line.Append($" [you have {n} placed]");
            line.Append($" -> {o.Output}{(o.ValueText.Length > 0 ? " " + o.ValueText : "")} | {MachineMath.Duration(o.Minutes, o.WholeDays)}");
            if (o.Priced) line.Append($" | {GainText(o)} | {Num(o.PerDay)}g per machine-day");
            if (o.Fuel.Length > 0) line.Append($" | fuel: {o.Fuel} per load");
            if (o.Priced && o.Note.Length > 0) line.Append($" | {o.Note}");
            sb.AppendLine(line.ToString());
        }
        var compared = options.Where(o => o.Priced && o.RuleChance >= 1)
            .Select(o => new MachineOption(o.Machine, o.GainPerItem, o.PerDay)).ToList();
        if (compared.Count > 0) sb.AppendLine(MachineMath.Best(compared, name));
        sb.Append("Gain = output minus the input's and the fuel's sell price; a machine-day is 1600 machine minutes (6:00 to 2:00 plus the night), jobs under a day count as one load per day." +
                  (held ? "" : " Normal-quality input."));
        return (true, sb.ToString());
    }

    /// <summary>
    /// Whether the sell prices shown include Artisan (Object.getPriceAfterMultipliers): with shared money the best multiplier
    /// of every active farmer counts, with separate wallets only this player's.
    /// </summary>
    private static string ArtisanText()
    {
        var me = Game1.player;
        if (me.professions.Contains(Farmer.artisan)) return "Artisan, +40% on artisan goods: yes";
        if (!me.useSeparateWallets && Game1.getAllFarmers().FirstOrDefault(f => f != null && f.isActive() && f.professions.Contains(Farmer.artisan)) is { } other)
            return $"Artisan, +40% on artisan goods: yes, through {other.Name} (shared money)";
        return "Artisan, +40% on artisan goods: no";
    }

    /// <summary>
    /// The item a query means: an item you hold by its shown name (flavoured, with its quality), a qualified id, an item by
    /// its data name, a flavoured item by its full name ("Ancient Fruit Wine"), else the one item whose name contains the query.
    /// A generic flavoured item (plain "Wine") is replaced by the one kind you hold, or said to be priced generically.
    /// </summary>
    private static (Item? input, bool held, string failure, string note) ResolveInput(string query)
    {
        var inventory = Game1.player.Items.Where(i => i != null).ToList();
        if (inventory.FirstOrDefault(i => i.DisplayName.Equals(query, StringComparison.OrdinalIgnoreCase)) is { } mine)
            return (mine.getOne(), true, "", "");

        var (matches, exact) = FindItems(query);
        if (exact)
        {
            var (qid, name) = matches[0];
            var item = ItemRegistry.Create(qid, 1, 0, allowNull: true);
            if (item == null) return (null, false, $"The game cannot create \"{name}\".", "");
            if (GenericFlavored(qid) is not { } type) return (item, false, "", "");
            var kinds = inventory.Where(i => i.QualifiedItemId == qid && i is SObject so && so.preserve.Value != null)
                .GroupBy(i => i.DisplayName).Select(g => g.First()).ToList();
            if (kinds.Count == 1) return (kinds[0].getOne(), true, "", "");
            string yours = kinds.Count > 1 ? $" You hold {string.Join(", ", kinds.Select(k => k.DisplayName))}: ask for one of them by name." : "";
            return (item, false, "", $"This is plain {name} at its data price; a real one is worth {MachineMath.FlavoredPrice(type.ToString())}, so ask by its full name (as the game shows it).{yours}");
        }
        if (Flavored(query) is { } flavored) return (flavored, false, "", "");

        string q = query.ToLowerInvariant();
        var heldNames = inventory.Where(i => i.DisplayName.ToLowerInvariant().Contains(q)).GroupBy(i => i.DisplayName).Select(g => g.First()).ToList();
        var names = heldNames.Select(i => i.DisplayName).Concat(matches.Select(m => m.name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0)
            return (null, false, $"No item called \"{query}\". Check the spelling (item_info finds items by name).", "");
        if (names.Count > 1)
            return (null, false, $"Several items match \"{query}\": {string.Join(", ", names.OrderBy(n => n).Take(MaxCandidates))}{(names.Count > MaxCandidates ? ", ..." : "")}. Ask again with the full name.", "");
        if (heldNames.Count == 1) return (heldNames[0].getOne(), true, "", "");
        var only = ItemRegistry.Create(matches[0].qid, 1, 0, allowNull: true);
        return only == null ? (null, false, $"The game cannot create \"{matches[0].name}\".", "") : (only, false, "", "");
    }

    /// <summary>The preserve type whose generic item this is (ObjectDataDefinition.GetBaseItemIdForFlavoredItem: (O)348 Wine...), or null.</summary>
    private static SObject.PreserveType? GenericFlavored(string qualifiedId)
    {
        var def = ItemRegistry.GetObjectTypeDefinition();
        foreach (SObject.PreserveType type in Enum.GetValues(typeof(SObject.PreserveType)))
            if (def.GetBaseItemIdForFlavoredItem(type, "") == qualifiedId) return type;
        return null;
    }

    /// <summary>A flavoured item by its full shown name ("Blueberry Wine", "Pickled Beet"), made as the game makes it, or null.</summary>
    private static SObject? Flavored(string query)
    {
        string q = query.ToLowerInvariant();
        var def = ItemRegistry.GetObjectTypeDefinition();
        var ingredients = Game1.objectData
            .Where(kv => kv.Value != null)
            .Select(kv => (qid: "(O)" + kv.Key, name: ItemName("(O)" + kv.Key).ToLowerInvariant()))
            .Where(x => x.name.Length >= 3 && x.name != q && q.Contains(x.name))
            .OrderByDescending(x => x.name.Length)
            .ToList();
        foreach (var (qid, _) in ingredients)
        {
            if (ItemRegistry.Create(qid, 1, 0, allowNull: true) is not SObject ingredient) continue;
            foreach (SObject.PreserveType type in Enum.GetValues(typeof(SObject.PreserveType)))
            {
                try
                {
                    if (def.CreateFlavoredItem(type, ingredient) is { } made && made.DisplayName.Equals(query, StringComparison.OrdinalIgnoreCase)) return made;
                }
                catch
                {
                    // an ingredient this kind of item cannot be made from
                }
            }
        }
        return null;
    }

    /// <summary>Objects whose shown name, English name or id equals the query, else those whose names contain it; a qualified id ("(O)24") is taken as it is.</summary>
    private static (List<(string qid, string name)> matches, bool exact) FindItems(string query)
    {
        if (query.StartsWith("(") && ItemRegistry.GetData(query) is { } direct)
            return (new List<(string, string)> { (direct.QualifiedItemId, direct.DisplayName) }, true);
        string q = query.ToLowerInvariant();
        var exact = new List<(string, string)>();
        var containing = new List<(string, string)>();
        foreach (var kv in Game1.objectData)
        {
            if (kv.Value == null) continue;
            string qid = "(O)" + kv.Key, internalName = kv.Value.Name ?? "", display = ItemName(qid);
            string d = display.ToLowerInvariant(), n = internalName.ToLowerInvariant();
            if (d == q || n == q || kv.Key.ToLowerInvariant() == q) exact.Add((qid, display));
            else if (d.Contains(q) || n.Contains(q)) containing.Add((qid, display));
        }
        return exact.Count > 0 ? (exact, true) : (containing, false);
    }

    // ------------------------------------------------------------------ machine=

    private static (bool ok, string text) MachineReport(string query, List<Placed> placedList)
    {
        var machines = MachineData();
        string q = query.ToLowerInvariant();
        var exact = new List<string>();
        var containing = new List<string>();
        foreach (var id in machines.Keys)
        {
            var d = ItemRegistry.GetData(id);
            string display = (d?.DisplayName ?? id).ToLowerInvariant(), internalName = (d?.InternalName ?? "").ToLowerInvariant();
            if (display == q || internalName == q || id.ToLowerInvariant() == q) exact.Add(id);
            else if (display.Contains(q) || internalName.Contains(q)) containing.Add(id);
        }
        var found = exact.Count > 0 ? exact : containing;
        if (found.Count == 0)
            return (false, $"No machine called \"{query}\". Machines: {string.Join(", ", machines.Keys.Select(ItemName).Distinct().OrderBy(n => n))}.");
        if (found.Count > 1)
            return (false, $"Several machines match \"{query}\": {string.Join(", ", found.Select(ItemName).Distinct().OrderBy(n => n))}. Ask again with the full name.");

        string mid = found[0];
        var data = machines[mid];
        var sb = new StringBuilder();
        int placed = placedList.Count(p => p.MachineId == mid);
        sb.AppendLine($"{ItemName(mid)}{(placed > 0 ? $" (you have {placed} placed; machines() shows their state)" : "")}.");
        if (data.AdditionalConsumedItems is { Count: > 0 } fuel)
            sb.AppendLine($"Fuel: {string.Join(" + ", fuel.Select(f => $"{f.RequiredCount} {ItemName(f.ItemId)}"))} per load, taken from your inventory.");
        if (data.OutputRules == null || data.OutputRules.Count == 0)
        {
            sb.Append("It has no rules in the machine data: the game fills it by itself (a tapper from its tree, a lightning rod from storms).");
            return (true, sb.ToString().TrimEnd());
        }
        sb.AppendLine("Rules (the first that fits the item is used):");
        foreach (var rule in data.OutputRules)
        {
            if (rule == null) continue;
            sb.AppendLine($"- {InputText(rule)} -> {(mid == "(BC)156" ? "hatches the egg's slime (green, blue, red, purple or tiger; keep slimes in a Slime Hutch)" : OutputText(rule))} | {RuleTime(rule, data.OnlyCompleteOvernight)}");
        }
        if (data.ReadyTimeModifiers is { Count: > 0 } mods)
            sb.AppendLine("Time changes: " + string.Join("; ", mods.Select(ModifierText)) + ".");
        if (data.OnlyCompleteOvernight) sb.AppendLine("Finishes only overnight: ready at 6:00 on the morning after its time runs out.");
        sb.Append("machines(item=X) gives the exact output, price and value for one item.");
        return (true, sb.ToString().TrimEnd());
    }

    private static string InputText(MachineOutputRule rule)
    {
        var triggers = (rule.Triggers ?? new List<MachineOutputTriggerRule>()).Where(t => t != null).ToList();
        bool takesInput = triggers.Any(t => t.Trigger.HasFlag(MachineOutputTrigger.ItemPlacedInMachine));
        var inputs = new List<string>();
        var auto = new List<string>();
        foreach (var t in triggers)
        {
            var cond = MachineMath.ConditionText(t.Condition);
            if (t.Trigger.HasFlag(MachineOutputTrigger.ItemPlacedInMachine))
            {
                var words = new List<string>();
                if (t.RequiredItemId != null) words.Add(ItemName(t.RequiredItemId));
                var tags = MachineMath.TagText(t.RequiredTags, KnownItemName);
                if (tags.Length > 0) words.Add(tags);
                if (words.Count == 0) words.Add("any item");
                var text = string.Join(" ", words) + (t.RequiredCount > 1 ? $" x{t.RequiredCount}" : "");
                inputs.Add(cond.Length > 0 ? $"{text} ({cond})" : text);
                continue;
            }
            string when = t.Trigger.HasFlag(MachineOutputTrigger.DayUpdate) ? "by itself each day"
                : t.Trigger.HasFlag(MachineOutputTrigger.MachinePutDown) ? "when placed"
                : t.Trigger.HasFlag(MachineOutputTrigger.OutputCollected) ? (takesInput ? "repeats after each collection" : "again after each collection")
                : "";
            if (when.Length > 0) auto.Add(cond.Length > 0 ? $"{when} ({cond})" : when);
        }
        auto = auto.Distinct().ToList();
        if (inputs.Count == 0 && auto.Count == 0) return "never";
        return string.Join(" or ", inputs) + (inputs.Count > 0 && auto.Count > 0 ? "; " : "") + string.Join(", ", auto);
    }

    private static string OutputText(MachineOutputRule rule)
    {
        var outputs = rule.OutputItem;
        if (outputs == null || outputs.Count == 0) return "nothing";
        var chances = MachineMath.OutputChances(outputs.Select(o => o == null ? 0 : MachineMath.RandomChance(o.Condition)).ToList(), rule.UseFirstValidOutput);
        var parts = new List<string>();
        for (int i = 0; i < outputs.Count; i++)
        {
            var o = outputs[i];
            if (o == null || chances[i] <= 0) continue;
            string text = OutputEntryText(o);
            var other = MachineMath.SplitQueries(o.Condition).Where(x => !x.TrimStart('!').StartsWith("RANDOM", StringComparison.OrdinalIgnoreCase) && !x.TrimStart('!').StartsWith("SYNCED_RANDOM", StringComparison.OrdinalIgnoreCase)).ToList();
            if (other.Count > 0) text += $" (if {MachineMath.ConditionText(string.Join(", ", other))})";
            parts.Add(chances[i] < 0.999 ? $"{MachineMath.Percent(chances[i])} {text}" : text);
        }
        return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
    }

    private static string OutputEntryText(MachineItemOutput o)
    {
        if (o.OutputMethod != null) return MachineMath.MethodText(o.OutputMethod);
        var id = o.ItemId ?? "";
        string stack = o.MinStack > 1 ? (o.MaxStack > o.MinStack ? $" x{o.MinStack}-{o.MaxStack}" : $" x{o.MinStack}") : "";
        if (id == "DROP_IN") return "a copy of the input" + stack;
        var words = id.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2 && words[0] == "FLAVORED_ITEM")
        {
            string type = words[1] == "Honey" && words.Contains("NEARBY_FLOWER_ID") ? "Honey (flavoured by a flower within 5 tiles)" : SpacedWords(words[1]) + " of the input";
            return $"{type}{stack}, {MachineMath.FlavoredPrice(words[1])}";
        }
        if (words.Length == 1 && ItemRegistry.Create(id, 1, 0, allowNull: true) is { } item)
            return $"{item.DisplayName}{stack} {SellPrice(item)}g";
        return id + stack;
    }

    private static string SpacedWords(string s)
    {
        var sb = new StringBuilder();
        foreach (char ch in s)
        {
            if (char.IsUpper(ch) && sb.Length > 0) sb.Append(' ');
            sb.Append(sb.Length == 0 ? ch : char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    private static string RuleTime(MachineOutputRule rule, bool overnight)
    {
        if (rule.DaysUntilReady >= 0) return MachineMath.Duration(rule.DaysUntilReady * MachineMath.MinutesPerDay, true);
        if (rule.MinutesUntilReady > 0)
            return overnight ? MachineMath.Duration(MachineMath.WholeDays(rule.MinutesUntilReady) * MachineMath.MinutesPerDay, true) : MachineMath.Duration(rule.MinutesUntilReady);
        if (rule.OutputItem?.Any(o => o?.OutputMethod != null) == true) return "time varies";
        return "instant";
    }

    private static string ModifierText(QuantityModifier m)
    {
        string who = "", cond = m.Condition ?? "";
        var words = cond.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 3 && words[0] == "ITEM_ID") who = ItemName(words[2]);
        else if (words.Length == 3 && words[0] == "PLAYER_HAS_PROFESSION" && int.TryParse(words[2], out int prof)) who = "with " + SafeProfession(prof);
        else who = MachineMath.ConditionText(cond);
        string change = m.Modification switch
        {
            QuantityModifier.ModificationType.Set => MachineMath.Duration((int)m.Amount),
            QuantityModifier.ModificationType.Multiply => $"time x{Num(m.Amount, "0.##")}",
            QuantityModifier.ModificationType.Divide => $"time / {Num(m.Amount, "0.##")}",
            QuantityModifier.ModificationType.Add => $"+{MachineMath.Duration((int)m.Amount)}",
            _ => $"-{MachineMath.Duration((int)m.Amount)}"
        };
        return who.Length > 0 ? $"{who}: {change}" : change;
    }

    private static string SafeProfession(int id)
    {
        try { return Professions.Title(id); }
        catch { return $"profession {id}"; }
    }

    // ------------------------------------------------------------------ placed machines

    private sealed record Placed(string MachineId, MachineSpot Spot, string Travel);

    /// <summary>Maps that belong to the farm: the Farm, its building interiors, the farmhouse and cabins, cellars, the Greenhouse, the Farm Cave and Ginger Island's farm.</summary>
    private static bool IsFarmPlace(GameLocation loc) =>
        loc.IsFarm || loc.IsGreenhouse || loc is FarmHouse || loc is Cellar || loc is FarmCave || loc is IslandWest || loc.ParentBuilding != null;

    /// <summary>
    /// The placed machines, handed to <paramref name="done"/> (or the error). A farmhand's split-screen screen keeps only the
    /// map its farmer stands on and the always-active maps (Farm, farmhouse, Greenhouse) up to date (Multiplayer.isActiveLocation),
    /// so its Cellar or Ginger Island casks would show an old state: there the scan runs on the host screen's next tick.
    /// </summary>
    private static IEnumerator ScanHere(Action<List<Placed>?, Exception?> done)
    {
        if (!Context.IsMainPlayer && Players.SplitScreen)
        {
            var host = GameThread.Host.Run(Scan, TimeSpan.FromSeconds(3));
            while (!host.IsCompleted) yield return null;
            if (host.Status == TaskStatus.RanToCompletion)
            {
                done(host.Result, null);
                yield break;
            }
        }
        List<Placed>? list = null;
        Exception? error = null;
        try { list = Scan(); }
        catch (Exception ex) { error = ex; }
        done(list, error);
    }

    private static List<Placed> Scan()
    {
        var machines = MachineData();
        var list = new List<Placed>();
        int time = Game1.timeOfDay, day = Game1.dayOfMonth, season = CropMath.SeasonIndex(Game1.currentSeason);
        Utility.ForEachLocation(loc =>
        {
            if (!IsFarmPlace(loc)) return true;
            string place = loc.Name, travel = "";
            if (loc.ParentBuilding is { } b && !string.IsNullOrEmpty(loc.uniqueName.Value))
            {
                place = $"{b.buildingType.Value} at ({b.tileX.Value},{b.tileY.Value})";
                travel = loc.NameOrUniqueName;
            }
            foreach (var (tile, obj) in loc.objects.Pairs)
            {
                if (obj == null || !machines.TryGetValue(obj.QualifiedItemId, out var data) || data == null) continue;
                var held = obj.heldObject.Value;
                var state = held == null ? MachineState.Empty : obj.readyForHarvest.Value ? MachineState.Ready : MachineState.Working;
                string when = state == MachineState.Working ? WorkingText(obj, held!, data.OnlyCompleteOvernight, time, day, season) : "";
                bool selfFilling = data.OutputRules?.Any(r => r?.Triggers?.Any(t => t != null && t.Trigger.HasFlag(MachineOutputTrigger.ItemPlacedInMachine)) == true) != true;
                list.Add(new Placed(obj.QualifiedItemId, new MachineSpot(obj.DisplayName, held?.DisplayName ?? "", place, (int)tile.X, (int)tile.Y, state, when, selfFilling), travel));
            }
            return true;
        }, includeInteriors: true, includeGenerated: false);
        return list;
    }

    private static string WorkingText(SObject obj, SObject held, bool overnight, int time, int day, int season)
    {
        if (obj is Cask cask)
        {
            string quality = held.Quality switch { 1 => "Silver", 2 => "Gold", 4 => "Iridium", _ => "normal" };
            if (cask.agingRate.Value <= 0) return $"aging, {quality} now";
            int days = (int)Math.Ceiling(cask.daysToMature.Value / cask.agingRate.Value);
            return $"aging, {quality} now, Iridium in {days} {(days == 1 ? "day" : "days")} (hit the cask with a tool to take it out early)";
        }
        int left = obj.MinutesUntilReady;
        if (left >= 999999) return "not timed";
        return "ready " + MachineMath.ReadyText(time, left, day, season, overnight);
    }

    private static string FarmReport(List<Placed> placed)
    {
        if (placed.Count == 0)
            return "No machines placed on the farm, in its buildings or in the farmhouse yet. Craft one (craft) and put it down (place); machines(item=X) says which machine suits an item.";
        var spots = placed.Select(p => p.Spot).ToList();
        var sb = new StringBuilder();
        int idle = spots.Count(s => s.State == MachineState.Empty && s.SelfFilling);
        sb.AppendLine($"Machines on the farm: {spots.Count} ({spots.Count(s => s.State == MachineState.Ready)} ready, {spots.Count(s => s.State == MachineState.Working)} working, " +
                      $"{spots.Count(s => s.State == MachineState.Empty) - idle} empty{(idle > 0 ? $", {idle} waiting to fill by themselves" : "")}). Now: {Game1.currentSeason} {Game1.dayOfMonth}, {MachineMath.Clock(Game1.timeOfDay)}.");
        foreach (var line in MachineMath.StateLines(spots)) sb.AppendLine(line);
        var buildings = placed.Where(p => p.Travel.Length > 0).Select(p => (p.Spot.Place, p.Travel)).Distinct().ToList();
        if (buildings.Count > 0)
            sb.AppendLine("Enter a building with travel_to: " + string.Join("; ", buildings.Select(b => $"{b.Place} = \"{b.Travel}\"")) + ".");
        return sb.ToString().TrimEnd();
    }
}
