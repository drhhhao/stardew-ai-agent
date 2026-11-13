using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StardewMCP.Agent;

/// <summary>
/// The arithmetic and wording of <c>progress</c> that needs no game types (compiled by the unit tests):
/// skill XP thresholds, the profession tree, the mine elevator, slayer goals and museum reward needs.
/// </summary>
public static class ProgressMath
{
    /// <summary>XP needed for levels 1-10 (Farmer.getBaseExperienceForLevel).</summary>
    private static readonly int[] LevelXp = { 0, 100, 380, 770, 1300, 2150, 3300, 4800, 6900, 10000, 15000 };

    public const int MaxLevel = 10;

    /// <summary>Total XP for a level, or -1 outside 1-10 (as the game).</summary>
    public static int XpForLevel(int level) => level >= 1 && level <= MaxLevel ? LevelXp[level] : -1;

    public static string N(int v) => v.ToString("#,0", CultureInfo.InvariantCulture);

    /// <summary>"Farming 4 (1,120/1,300 XP, 180 to go; 5: profession)", "Farming 10 (max, 16,200 XP)".
    /// <paramref name="buff"/> is the food/ring bonus on top of the real level.</summary>
    public static string SkillText(string name, int level, int xp, int buff = 0)
    {
        var shown = buff > 0 ? $"{level}+{buff}" : buff < 0 ? $"{level}{buff}" : $"{level}";
        if (level >= MaxLevel) return $"{name} {shown} (max, {N(xp)} XP)";
        int next = XpForLevel(level + 1);
        var choice = level + 1 is 5 or 10 ? $"; {level + 1}: profession" : "";
        return $"{name} {shown} ({N(xp)}/{N(next)} XP, {N(Math.Max(0, next - xp))} to go{choice})";
    }

    /// <summary>The level-10 professions that follow a level-5 one: ids skill*6 + 2,3 after +0, and + 4,5 after +1.</summary>
    public static (int, int) Level10Of(int level5Id)
    {
        int b = level5Id - level5Id % 6;
        return level5Id % 6 == 0 ? (b + 2, b + 3) : (b + 4, b + 5);
    }

    /// <summary>"have 2 pack + 4 chest", or "have 3" when nothing is stored.</summary>
    public static string Have(int pack, int chest) => chest > 0 ? $"have {N(pack)} pack + {N(chest)} chest" : $"have {N(pack)}";

    /// <summary>The deepest elevator stop: every 5th floor reached, up to 120 (MineElevatorMenu).</summary>
    public static int ElevatorFloor(int lowestLevelReached) => Math.Clamp(lowestLevelReached, 0, 120) / 5 * 5;

    /// <summary>"Mines: deepest floor 47, elevator to 45 | Skull Cavern: not entered yet". The game counts Skull
    /// Cavern floors on from 121 in the same level number.</summary>
    public static string MinesText(int deepestLevel, int lowestLevelReached)
    {
        int mines = Math.Clamp(deepestLevel, 0, 120);
        var bottom = mines >= 120 ? " (bottom reached)" : "";
        var skull = deepestLevel > 120 ? $"Skull Cavern deepest floor {deepestLevel - 120}" : "Skull Cavern: not entered yet";
        return $"Mines: deepest floor {mines}{bottom}, elevator to {ElevatorFloor(lowestLevelReached)} | {skull}";
    }

    /// <summary>One Adventurer's Guild goal: kills of its monsters so far against the target.</summary>
    public sealed record SlayerGoal(string Name, int Killed, int Count, string Reward, bool Collected);

    /// <summary>
    /// "Slayer goals: Slimes 312/1000 (Slime Charmer Ring) | Bats 41/200 (Vampire Ring). Reward waiting at Gil: Duggies (Hard Hat).
    /// Not started: Mummies 0/100, ... Done: Crabs." Goals with kills come first, nearest to done first.
    /// </summary>
    public static string SlayerText(IEnumerable<SlayerGoal> goals)
    {
        var all = goals.ToList();
        if (all.Count == 0) return "Slayer goals: none in the game data.";
        string Reward(SlayerGoal g) => g.Reward.Length > 0 ? $" ({g.Reward})" : "";
        var going = all.Where(g => g.Killed > 0 && g.Killed < g.Count)
            .OrderByDescending(g => (double)g.Killed / Math.Max(1, g.Count))
            .Select(g => $"{g.Name} {N(g.Killed)}/{N(g.Count)}{Reward(g)}").ToList();
        var waiting = all.Where(g => g.Killed >= g.Count && !g.Collected).Select(g => $"{g.Name}{Reward(g)}").ToList();
        var fresh = all.Where(g => g.Killed <= 0 && g.Count > 0).Select(g => $"{g.Name} 0/{N(g.Count)}{Reward(g)}").ToList();
        var done = all.Where(g => g.Killed >= g.Count && g.Collected).Select(g => g.Name).ToList();

        var parts = new List<string>();
        parts.Add("Slayer goals (Adventurer's Guild): " + (going.Count > 0 ? string.Join(" | ", going) : "none started"));
        if (waiting.Count > 0) parts.Add("Done, reward waiting at Gil: " + string.Join(", ", waiting));
        if (fresh.Count > 0) parts.Add("Not started: " + string.Join(", ", fresh));
        if (done.Count > 0) parts.Add("Done: " + string.Join(", ", done));
        return string.Join(". ", parts) + ".";
    }

    /// <summary>
    /// Whether Gil has given a goal's reward. "Gil_&lt;id&gt;" (<paramref name="gilFlag"/>) is set only when a reward item is
    /// taken; a goal without an item counts once any of its reward flags is set, or at once when it has none.
    /// </summary>
    public static bool SlayerRewardCollected(bool gilFlag, bool hasRewardItem, IEnumerable<string?> rewardFlags, Func<string, bool> flagSet)
    {
        if (gilFlag) return true;
        if (hasRewardItem) return false;
        var flags = rewardFlags.Where(f => !string.IsNullOrEmpty(f)).Select(f => f!).ToList();
        return flags.Count == 0 || flags.Any(flagSet);
    }

    /// <summary>One museum reward condition (Data/MuseumRewards TargetContextTags): Tag "" counts every donation,
    /// and Count -1 with Tag "" means every donatable item.</summary>
    public sealed record MuseumNeed(string Tag, int Count);

    /// <summary>The qualified item id behind an "id_o_96" context tag, or null for other tags.</summary>
    public static string? TagItemId(string tag)
    {
        if (!tag.StartsWith("id_", StringComparison.Ordinal)) return null;
        int sep = tag.IndexOf('_', 3);
        if (sep < 0 || sep == tag.Length - 1) return null;
        return $"({tag.Substring(3, sep - 3).ToUpperInvariant()}){tag.Substring(sep + 1)}";
    }

    /// <summary>How many more donations a reward needs in total (0 = earned).</summary>
    public static int Missing(IEnumerable<MuseumNeed> needs, Func<string, int> donated, int totalDonatable)
    {
        int missing = 0;
        foreach (var n in needs)
        {
            int want = n.Tag.Length == 0 && n.Count == -1 ? totalDonatable : n.Count;
            missing += Math.Max(0, want - donated(n.Tag));
        }
        return missing;
    }

    /// <summary>What a reward still needs: "3 more artifacts, Dwarf Scroll IV". <paramref name="itemName"/> names a qualified item id.</summary>
    public static string NeedsText(IEnumerable<MuseumNeed> needs, Func<string, int> donated, int totalDonatable, Func<string, string> itemName)
    {
        var parts = new List<string>();
        foreach (var n in needs)
        {
            int want = n.Tag.Length == 0 && n.Count == -1 ? totalDonatable : n.Count;
            int more = want - donated(n.Tag);
            if (more <= 0) continue;
            var id = TagItemId(n.Tag);
            parts.Add(n.Tag switch
            {
                "" => $"{more} more donation{(more == 1 ? "" : "s")}",
                "item_type_arch" => $"{more} more artifact{(more == 1 ? "" : "s")}",
                "item_type_minerals" => $"{more} more mineral{(more == 1 ? "" : "s")}",
                _ when id != null => itemName(id),
                _ => $"{more} more {n.Tag}"
            });
        }
        return string.Join(", ", parts);
    }
}
