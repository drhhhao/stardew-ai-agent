using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace StardewMCP.Agent;

/// <summary>One way to process an item, as compared by <see cref="MachineMath.Best"/>.</summary>
/// <param name="Machine">Machine name.</param>
/// <param name="GainPerItem">Gold gained per input item over selling it raw; null when the input is not used up (crystalarium).</param>
/// <param name="PerMachineDay">Gold gained per machine-day.</param>
public sealed record MachineOption(string Machine, double? GainPerItem, double PerMachineDay);

/// <summary>A placed machine for the machines() farm overview. <see cref="MachineMath.StateLines"/> groups them.</summary>
/// <param name="Machine">Machine name.</param>
/// <param name="Held">What it holds (or makes), empty when empty.</param>
/// <param name="Place">Where it stands ("Farm", "Shed at (40,12)").</param>
/// <param name="State">Ready, Working or Empty.</param>
/// <param name="When">For a working machine: when it is ready ("ready today 18:40").</param>
/// <param name="SelfFilling">The machine takes no input (tapper, lightning rod, bee house): empty is not a chore.</param>
public sealed record MachineSpot(string Machine, string Held, string Place, int X, int Y, MachineState State, string When = "", bool SelfFilling = false);

public enum MachineState { Ready, Working, Empty }

/// <summary>
/// Game-free arithmetic and wording for the machines tool (<see cref="MachineTools"/>): machine time, value per
/// machine-day, output chances, ready times and the grouped farm overview. Formulas follow the decompiled game:
/// Utility.CalculateMinutesUntilMorning, MachineDataUtility.GetOutputData, ObjectDataDefinition.CreateFlavored*.
/// </summary>
public static class MachineMath
{
    /// <summary>
    /// Machine minutes in one day: the clock runs 1200 minutes from 6:00 to 2:00 and the night adds 400 more
    /// (Utility.CalculateMinutesUntilMorning: 2600 - now + 400), whenever the farmer goes to bed.
    /// </summary>
    public const int MinutesPerDay = 1600;
    private const int DayClockMinutes = 1200;
    private static readonly string[] Weekdays = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    private static string Num(double v, string format = "0.#") => v.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>A 26-hour clock time (1430, 2510) as minutes since midnight (870, 1510).</summary>
    public static int ToMinutes(int time) => time / 100 * 60 + time % 100;

    /// <summary>Machine-days for a job of this many machine minutes.</summary>
    public static double Days(int minutes) => minutes / (double)MinutesPerDay;

    /// <summary>
    /// Gold per machine-day for a job: the gain divided by its days, at least one day per load (the farmer seldom
    /// reloads a short job more than once a day).
    /// </summary>
    public static double PerMachineDay(double gain, int minutes) => gain / Math.Max(1.0, Days(minutes));

    /// <summary>Whole days for a job that is only done at 6:00 (counted in days, or a machine that finishes only overnight): its days rounded up.</summary>
    public static int WholeDays(int minutes) => Math.Max(1, (int)Math.Ceiling(Days(minutes) - 1e-9));

    /// <summary>"4h", "1h 40m", "50 min", "2.5 days" or, for a job counted in days, "1 day, ready at 6:00".</summary>
    public static string Duration(int minutes, bool wholeDays = false)
    {
        if (wholeDays)
        {
            int days = WholeDays(minutes);
            return days == 1 ? "1 day, ready at 6:00" : $"{days} days, ready at 6:00";
        }
        if (minutes <= 0) return "instant";
        if (minutes < 60) return $"{minutes} min";
        if (minutes < MinutesPerDay)
        {
            int h = minutes / 60, m = minutes % 60;
            return m == 0 ? $"{h}h" : $"{h}h {m}m";
        }
        double d = Days(minutes);
        return Math.Abs(d - 1) < 0.005 ? "1 day" : $"{Num(d, "0.##")} days";
    }

    /// <summary>A 26-hour clock time as the game shows it: 600 -> "6:00", 1840 -> "18:40", 2530 -> "1:30".</summary>
    public static string Clock(int time) => $"{(time / 100) % 24}:{time % 100:00}";

    /// <summary>Short weekday of a day of the season (day 1 is a Monday).</summary>
    public static string Weekday(int dayOfMonth) => Weekdays[((Math.Max(1, dayOfMonth) - 1) % 7)];

    /// <summary>
    /// When a job with <paramref name="minutesLeft"/> machine minutes left is done, seen at <paramref name="timeOfDay"/>:
    /// days from today and the clock time. A job that would end in the night (after 2:00) is ready at 6:00 the next morning.
    /// A machine that finishes only overnight (Data/Machines OnlyCompleteOvernight: incubators) is ready at the 6:00 after its
    /// time runs out, also when it already ran out today (Object.minutesElapsed).
    /// </summary>
    public static (int dayOffset, int time) ReadyAt(int timeOfDay, int minutesLeft, bool overnight = false)
    {
        int now = Math.Clamp(ToMinutes(timeOfDay) - 360, 0, DayClockMinutes);
        int end = now + Math.Max(0, minutesLeft);
        int day = end / MinutesPerDay, rest = end % MinutesPerDay;
        if (rest == 0 && day > 0) return (day, 600);
        if (rest <= DayClockMinutes && !overnight) return (day, 600 + rest / 60 * 100 + rest % 60);
        return (day + 1, 600);
    }

    /// <summary>"today 18:40", "tomorrow 6:00", "Fri 14 6:00" or "Mon summer 2 6:00" (season given as 0-3).</summary>
    public static string ReadyText(int timeOfDay, int minutesLeft, int dayOfMonth, int season, bool overnight = false)
    {
        var (offset, time) = ReadyAt(timeOfDay, minutesLeft, overnight);
        if (offset == 0) return $"today {Clock(time)}";
        if (offset == 1) return $"tomorrow {Clock(time)}";
        int day = dayOfMonth + offset, s = season;
        while (day > CropMath.DaysPerSeason) { day -= CropMath.DaysPerSeason; s++; }
        string seasonText = s == season || season < 0 ? "" : CropMath.SeasonName(s) + " ";
        return $"{Weekday(day)} {seasonText}{day} {Clock(time)}";
    }

    // ------------------------------------------------------------------ conditions and chances

    /// <summary>Game state queries in a condition, split on commas outside quotes (GameStateQuery.Parse).</summary>
    public static List<string> SplitQueries(string? condition)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(condition)) return list;
        var sb = new StringBuilder();
        bool quoted = false;
        foreach (char ch in condition)
        {
            if (ch == '"') quoted = !quoted;
            if (ch == ',' && !quoted)
            {
                if (sb.ToString().Trim().Length > 0) list.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else sb.Append(ch);
        }
        if (sb.ToString().Trim().Length > 0) list.Add(sb.ToString().Trim());
        return list;
    }

    /// <summary>The arguments of a query split on spaces outside quotes, quotes removed (ArgUtility.SplitBySpaceQuoteAware): the branches of ANY.</summary>
    public static List<string> SplitQuoted(string? text)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        var sb = new StringBuilder();
        bool quoted = false;
        foreach (char ch in text)
        {
            if (ch == '"') { quoted = !quoted; continue; }
            if (ch == ' ' && !quoted)
            {
                if (sb.ToString().Trim().Length > 0) list.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else sb.Append(ch);
        }
        if (sb.ToString().Trim().Length > 0) list.Add(sb.ToString().Trim());
        return list;
    }

    /// <summary>
    /// The chance part of a condition: the product of its RANDOM queries ("RANDOM 0.3" -> 0.3, "!RANDOM 0.3" -> 0.7),
    /// 1 when it has none. The other queries are checked by the game.
    /// </summary>
    public static double RandomChance(string? condition)
    {
        double chance = 1.0;
        foreach (var q in SplitQueries(condition))
        {
            var parts = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            bool negated = parts[0].StartsWith("!");
            string key = parts[0].TrimStart('!');
            if (!key.Equals("RANDOM", StringComparison.OrdinalIgnoreCase) && !key.Equals("SYNCED_RANDOM", StringComparison.OrdinalIgnoreCase)) continue;
            // SYNCED_RANDOM <interval> <key> <chance>
            string raw = key.Equals("RANDOM", StringComparison.OrdinalIgnoreCase) ? parts[1] : parts.Length >= 4 ? parts[3] : "";
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var p)) continue;
            p = Math.Clamp(p, 0, 1);
            chance *= negated ? 1 - p : p;
        }
        return chance;
    }

    /// <summary>
    /// The chance each output entry is produced, given the chance each one's condition passes
    /// (MachineDataUtility.GetOutputData): the first that passes when <paramref name="useFirstValid"/>, else one of
    /// those that pass at random. The sum is below 1 when sometimes none passes.
    /// </summary>
    public static double[] OutputChances(IReadOnlyList<double> passChance, bool useFirstValid)
    {
        int n = passChance.Count;
        var result = new double[n];
        if (n == 0) return result;
        if (useFirstValid)
        {
            double none = 1.0;
            for (int i = 0; i < n; i++)
            {
                result[i] = none * passChance[i];
                none *= 1 - passChance[i];
            }
            return result;
        }
        if (n > 12)
        {
            double total = passChance.Sum();
            for (int i = 0; i < n; i++) result[i] = total > 0 ? passChance[i] / total : 0;
            return result;
        }
        for (int mask = 1; mask < (1 << n); mask++)
        {
            double p = 1.0;
            int size = 0;
            for (int i = 0; i < n; i++)
            {
                bool inSet = (mask & (1 << i)) != 0;
                p *= inSet ? passChance[i] : 1 - passChance[i];
                if (inSet) size++;
            }
            if (p <= 0) continue;
            for (int i = 0; i < n; i++)
                if ((mask & (1 << i)) != 0) result[i] += p / size;
        }
        return result;
    }

    /// <summary>
    /// The average result of quantity modifiers (Utility.ApplyQuantityModifiers) on <paramref name="value"/>, each applied
    /// with its chance: in order on the running value (mode "Stack"), or the lowest / highest single result of those that
    /// apply ("Minimum" / "Maximum"). Modification is QuantityModifier.ModificationType by name; a random amount is its average.
    /// </summary>
    public static double ExpectedQuantity(double value, IReadOnlyList<(string modification, double amount, double chance)>? modifiers, string mode = "Stack")
    {
        if (modifiers == null || modifiers.Count == 0) return value;
        int n = Math.Min(modifiers.Count, 12);
        double expected = 0;
        for (int mask = 0; mask < (1 << n); mask++)
        {
            double p = 1;
            for (int i = 0; i < n && p > 0; i++)
            {
                double c = Math.Clamp(modifiers[i].chance, 0, 1);
                p *= (mask & (1 << i)) != 0 ? c : 1 - c;
            }
            if (p <= 0) continue;
            double? result = null;
            for (int i = 0; i < n; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                var (mod, amount, _) = modifiers[i];
                if (mode.Equals("Minimum", StringComparison.OrdinalIgnoreCase)) { double r = ApplyModifier(value, mod, amount); result = result == null ? r : Math.Min(result.Value, r); }
                else if (mode.Equals("Maximum", StringComparison.OrdinalIgnoreCase)) { double r = ApplyModifier(value, mod, amount); result = result == null ? r : Math.Max(result.Value, r); }
                else result = ApplyModifier(result ?? value, mod, amount);
            }
            expected += p * (result ?? value);
        }
        return expected;
    }

    /// <summary>QuantityModifier.Apply.</summary>
    private static double ApplyModifier(double value, string modification, double amount) => modification.ToLowerInvariant() switch
    {
        "add" => value + amount,
        "subtract" => value - amount,
        "multiply" => value * amount,
        "divide" => amount == 0 ? value : value / amount,
        "set" => amount,
        _ => value
    };

    /// <summary>The slime a Slime Incubator hatches from an egg (Object.CheckForActionOnMachine, "(BC)156"); empty for other items.</summary>
    public static string SlimeFromEgg(string qualifiedItemId) => qualifiedItemId switch
    {
        "(O)680" => "green",
        "(O)413" => "blue",
        "(O)437" => "red",
        "(O)439" => "purple",
        "(O)857" => "tiger",
        _ => ""
    };

    /// <summary>"30%" / "2.5%" for a chance.</summary>
    public static string Percent(double chance) => Num(chance * 100, chance * 100 >= 10 ? "0" : "0.#") + "%";

    // ------------------------------------------------------------------ wording of machine rules

    private static readonly Dictionary<string, string> Tags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["category_fruits"] = "any fruit",
        ["category_vegetable"] = "any vegetable",
        ["category_greens"] = "forage greens",
        ["category_fish"] = "any fish",
        ["category_gem"] = "any gem",
        ["category_minerals"] = "any mineral",
        ["category_flowers"] = "any flower",
        ["egg_item"] = "eggs",
        ["large_egg_item"] = "large",
        ["bone_item"] = "bone items",
        ["slime_egg_item"] = "slime eggs",
        ["edible_mushroom"] = "edible mushrooms",
        ["keg_wine"] = "items marked for wine",
        ["keg_juice"] = "items marked for juice",
        ["preserves_jelly"] = "items marked for jelly",
        ["preserves_pickle"] = "items marked for pickles",
        ["!crystalarium_banned"] = "",
        ["!seedmaker_banned"] = "any crop harvest",
        ["preserve_sheet_index_698"] = "from Sturgeon"
    };

    /// <summary>
    /// Plain words for the context tags of a machine input rule ("any fruit"); empty for tags that only exclude a few items.
    /// <paramref name="itemName"/> names the item of an id tag ("(O)881" -> "Bone Fragment"), null when unknown.
    /// </summary>
    public static string TagText(IEnumerable<string>? tags, Func<string, string?>? itemName = null)
    {
        if (tags == null) return "";
        var words = new List<string>();
        foreach (var raw in tags)
        {
            var tag = raw.Trim();
            if (tag.Length == 0) continue;
            if (Tags.TryGetValue(tag, out var w)) { if (w.Length > 0) words.Add(w); }
            else if (tag.StartsWith("!id_", StringComparison.OrdinalIgnoreCase)) words.Add("except " + IdTagName(tag.Substring(1), itemName));
            else if (tag.StartsWith("!")) words.Add("not " + tag.Substring(1).Replace('_', ' '));
            else if (tag.StartsWith("id_", StringComparison.OrdinalIgnoreCase)) words.Add(IdTagName(tag, itemName));
            else words.Add("items tagged " + tag);
        }
        return string.Join(" ", words);
    }

    /// <summary>
    /// The qualified item id of an id context tag ("id_o_881" -> "(O)881"; the game lowercases tags, so a modded id may
    /// differ in case), or null when the tag is not one.
    /// </summary>
    public static string? IdTagItem(string tag)
    {
        if (!tag.StartsWith("id_", StringComparison.OrdinalIgnoreCase)) return null;
        var rest = tag.Substring(3);
        int cut = rest.IndexOf('_');
        if (cut <= 0 || cut == rest.Length - 1) return null;
        return $"({rest.Substring(0, cut).ToUpperInvariant()}){rest.Substring(cut + 1)}";
    }

    private static string IdTagName(string tag, Func<string, string?>? itemName)
    {
        var id = IdTagItem(tag);
        var name = id != null && itemName != null ? itemName(id) : null;
        return string.IsNullOrEmpty(name) ? "item " + (id ?? tag.Substring(3)) : name!;
    }

    /// <summary>Plain words for a machine rule condition; RANDOM parts become a chance. Unknown queries are kept as they are.</summary>
    public static string ConditionText(string? condition)
    {
        var parts = new List<string>();
        foreach (var q in SplitQueries(condition))
        {
            var words = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) continue;
            bool not = words[0].StartsWith("!");
            string key = words[0].TrimStart('!').ToUpperInvariant();
            string arg = words.Length > 2 ? string.Join(" ", words.Skip(2)) : "";
            switch (key)
            {
                case "RANDOM":
                case "SYNCED_RANDOM":
                    var p = RandomChance(q);
                    parts.Add($"{Percent(p)} chance");
                    break;
                case "ITEM_CONTEXT_TAG":
                    parts.Add((not ? "not " : "") + TagText(arg.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
                    break;
                case "ITEM_EDIBILITY":
                    parts.Add(not ? "inedible" : "edible");
                    break;
                case "LOCATION_SEASON":
                case "SEASON":
                    parts.Add((not ? "not in " : "in ") + (key == "SEASON" ? string.Join(" ", words.Skip(1)) : arg).ToLowerInvariant());
                    break;
                case "PLAYER_HAS_PROFESSION":
                    parts.Add((not ? "without" : "with") + " profession " + arg);
                    break;
                default:
                    parts.Add(q);
                    break;
            }
        }
        return string.Join(", ", parts.Where(p => p.Length > 0));
    }

    /// <summary>How a FLAVORED_ITEM output is priced (ObjectDataDefinition.CreateFlavored*), by preserve type.</summary>
    public static string FlavoredPrice(string preserveType) => preserveType.ToLowerInvariant() switch
    {
        "wine" => "3x the input's base price",
        "juice" => "2.25x the input's base price",
        "jelly" or "pickle" => "2x the input's base price + 50",
        "driedfruit" or "driedmushroom" => "7.5x the input's base price + 25",
        "smokedfish" => "2x the fish's base price",
        "agedroe" => "2x the roe's price",
        "roe" => "30 + half the fish's base price",
        "honey" => "100 + 2x the flower's base price",
        "bait" => "10% of the fish's base price each",
        _ => "depends on the input"
    };

    /// <summary>Plain words for the custom output methods of vanilla machines (Object.Output*, Cask.OutputCask).</summary>
    public static string MethodText(string? outputMethod)
    {
        var name = outputMethod ?? "";
        int colon = name.LastIndexOf(':');
        if (colon >= 0) name = name.Substring(colon + 1);
        return name.Trim() switch
        {
            "OutputCask" => "ages it to better quality (Cellar only)",
            "OutputSeedMaker" => "1-3 seeds of the crop (sometimes Mixed Seeds or Ancient Seeds)",
            "OutputGeodeCrusher" => "what the geode holds (random)",
            "OutputDeconstructor" => "the most valuable ingredient of its crafting recipe",
            "OutputAnvil" => "the same trinket with new random stats",
            "OutputIncubator" => "hatches the animal (in a Coop or Barn)",
            "OutputSolarPanel" => "a Battery Pack every 7 sunny days",
            "OutputMushroomLog" => "mushrooms, more with trees nearby",
            "OutputStatueOfEndlessFortune" => "a gift: today's birthday villager's favourite, else a random valuable",
            _ => "a special output (" + name.Trim() + ")"
        };
    }

    // ------------------------------------------------------------------ comparing machines

    /// <summary>
    /// The best machine for an item, two ways: when machines are the limit (gold per machine-day) and when the item is
    /// the limit (gold gained per item). Ties within 1g are named.
    /// </summary>
    public static string Best(IReadOnlyList<MachineOption> options, string itemName)
    {
        var useful = options.Where(o => o.PerMachineDay > 0 || o.GainPerItem > 0).ToList();
        if (useful.Count == 0) return "Best: none of them adds value; sell it as it is.";

        var byDay = useful.OrderByDescending(o => o.PerMachineDay).ToList();
        var topDay = byDay[0];
        var dayTies = byDay.Skip(1).Where(o => topDay.PerMachineDay - o.PerMachineDay < 1).Select(o => o.Machine).Distinct().Where(m => m != topDay.Machine).ToList();

        var perItem = useful.Where(o => o.GainPerItem is > 0).OrderByDescending(o => o.GainPerItem).ToList();
        var sb = new StringBuilder("Best: ");
        if (perItem.Count == 0)
        {
            sb.Append($"{topDay.Machine} ({Num(topDay.PerMachineDay)}g per machine-day){TieText(dayTies)}.");
            return sb.ToString();
        }
        var topItem = perItem[0];
        var itemTies = perItem.Skip(1).Where(o => topItem.GainPerItem!.Value - o.GainPerItem!.Value < 1).Select(o => o.Machine).Distinct().Where(m => m != topItem.Machine).ToList();
        if (topItem.Machine == topDay.Machine)
            return $"Best: {topDay.Machine} both per machine-day ({Num(topDay.PerMachineDay)}g){TieText(dayTies)} and per {itemName} (+{Num(topItem.GainPerItem!.Value)}g){TieText(itemTies)}.";
        return $"Best: {topDay.Machine} when machines are the limit ({Num(topDay.PerMachineDay)}g per machine-day){TieText(dayTies)}; " +
               $"{topItem.Machine} when {itemName} is the limit (+{Num(topItem.GainPerItem!.Value)}g per item){TieText(itemTies)}.";
    }

    private static string TieText(List<string> ties) => ties.Count == 0 ? "" : $", tied with {string.Join(", ", ties)}";

    // ------------------------------------------------------------------ farm overview

    /// <summary>"Machines: 4 ready, 6 working, 2 empty (machines() for detail)" for farm_summary; empty when there are none.</summary>
    public static string CountsLine(int ready, int working, int empty) =>
        ready + working + empty == 0 ? "" : $"Machines: {ready} ready, {working} working, {empty} empty (machines() for detail).";

    /// <summary>
    /// The placed machines grouped by state, then by machine, held item and place, with their tiles:
    /// "READY (4):" / "- Keg (Blueberry Wine) x3, Shed at (40,12): (5,4) (6,4) (7,4)". Working tiles carry their ready time.
    /// </summary>
    public static List<string> StateLines(IEnumerable<MachineSpot> spots, int maxTiles = 12)
    {
        var all = spots.ToList();
        var lines = new List<string>();
        foreach (var state in new[] { MachineState.Ready, MachineState.Working, MachineState.Empty })
        {
            var inState = all.Where(s => s.State == state).ToList();
            if (inState.Count == 0) continue;
            string title = state switch
            {
                MachineState.Ready => $"READY ({inState.Count}), collect with interact on the tile:",
                MachineState.Working => $"Working ({inState.Count}):",
                _ => $"Empty ({inState.Count}):"
            };
            lines.Add(title);
            var groups = inState
                .GroupBy(s => (s.Machine, s.Held, s.Place, s.SelfFilling))
                .OrderBy(g => g.Key.Place == "Farm" ? 0 : 1).ThenBy(g => g.Key.Place).ThenBy(g => g.Key.Machine).ThenBy(g => g.Key.Held);
            foreach (var g in groups)
            {
                var list = g.OrderBy(s => s.Y).ThenBy(s => s.X).ToList();
                string what = g.Key.Machine + (g.Key.Held.Length > 0 ? $" ({g.Key.Held})" : "") + (list.Count > 1 ? $" x{list.Count}" : "");
                string tiles;
                if (state == MachineState.Working)
                {
                    var whens = list.Select(s => s.When).Distinct().ToList();
                    tiles = whens.Count == 1
                        ? TileList(list, maxTiles) + (whens[0].Length > 0 ? $", {whens[0]}" : "")
                        : string.Join("; ", list.Take(maxTiles).Select(s => $"({s.X},{s.Y}) {s.When}")) + (list.Count > maxTiles ? $"; and {list.Count - maxTiles} more" : "");
                }
                else tiles = TileList(list, maxTiles);
                string note = state == MachineState.Empty && g.Key.SelfFilling ? " (fills by itself)" : "";
                lines.Add($"- {what}, {g.Key.Place}: {tiles}{note}");
            }
        }
        return lines;
    }

    private static string TileList(List<MachineSpot> list, int max) =>
        string.Join(" ", list.Take(max).Select(s => $"({s.X},{s.Y})")) + (list.Count > max ? $" and {list.Count - max} more" : "");
}
