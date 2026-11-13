using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using StardewValley;
using StardewValley.GameData;
using StardewValley.Monsters;
using StardewValley.Tools;

namespace StardewMCP.Agent;

/// <summary>
/// monsters: which monsters are on this map and how to fight each kind. The live list uses the monsters the screen
/// actually holds (so modded and variant monsters show up); the codex reads Data/Monsters and adds the one-line tactic
/// from <see cref="MonsterRules"/>. A pure observation (Instant, no game time).
/// </summary>
public static class MonsterTools
{
    private const int MaxList = 25;

    public static List<ToolDef> All() => new()
    {
        new()
        {
            Name = "monsters",
            Description = "What is on this map and how to fight it. No arguments: every living monster on the map, nearest first, with current/max HP, " +
                          "damage, distance and a one-line tactic; your held weapon's damage is shown too. name=X: the codex for one monster or family " +
                          "(base HP, damage, defense, behaviour, what works and what to avoid). Use it on a new mine floor, before a crowded room or " +
                          "whenever a monster you do not recognise appears. Costs no game time.",
            Schema = AgentTools.Obj(new
            {
                name = AgentTools.Str("Monster name or family, e.g. Green Slime, Rock Crab, Serpent, Mummy, Bat"),
                radius = AgentTools.Int("Only list monsters within this many tiles (default: the whole map)")
            }),
            Instant = true, AppendStatus = false,
            Run = (a, c) => Report(c, () => Build(a))
        }
    };

    /// <summary>Runs the report; a data mismatch (a modded or changed game file) becomes a clear failure, not a crash.</summary>
    private static IEnumerator Report(CallContext c, Func<(bool ok, string text)> build)
    {
        (bool ok, string text) r;
        try { r = build(); }
        catch (Exception ex) { r = (false, $"monsters could not read the game data ({ex.GetType().Name}: {ex.Message}). Tell the player; the game data may differ from what the mod expects."); }
        if (r.ok) c.Result = r.text; else c.Fail(r.text);
        yield break;
    }

    private static (bool ok, string text) Build(JsonElement a)
    {
        if (Game1.player == null || Game1.currentLocation == null) return (false, "No save is loaded.");
        string name = AgentTools.S(a, "name").Trim();
        if (name.Length > 0) return Codex(name);
        return ListMap(AgentTools.I(a, "radius", 0));
    }

    // ------------------------------------------------------------------ live map list

    private static (bool ok, string text) ListMap(int radius)
    {
        var loc = Game1.currentLocation;
        var p = Game1.player;
        var all = loc.characters.OfType<Monster>().Where(m => m.Health > 0).ToList();
        if (radius > 0)
            all = all.Where(m => AgentTools.TileDistance(p.TilePoint, m.TilePoint) <= radius).ToList();
        var shown = all.OrderBy(m => AgentTools.TileDistance(p.TilePoint, m.TilePoint)).Take(MaxList).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Map {loc.DisplayName} · {all.Count} living monster(s){(all.Count > shown.Count ? $" (showing the nearest {shown.Count})" : "")}");
        sb.AppendLine(HeldLine(p));
        if (shown.Count == 0)
        {
            sb.Append("No living monsters here.");
            return (true, sb.ToString());
        }

        foreach (var mon in shown)
        {
            var t = MonsterRules.ForLive(mon.GetType().Name, mon.Name);
            float dist = AgentTools.TileDistance(p.TilePoint, mon.TilePoint);
            var tags = new List<string>();
            if (mon.IsInvisible) tags.Add("underground/untargetable");
            if (mon is Mummy { reviveTimer.Value: > 0 }) tags.Add("down, needs a bomb");
            if (mon is RockCrab { shellGone.Value: false }) tags.Add("shell intact");
            if (mon is Bug { isArmoredBug.Value: true }) tags.Add("armored, needs a bomb");
            if (mon is LavaLurk { currentState.Value: LavaLurk.State.Submerged }) tags.Add("submerged, invulnerable");
            if (t.Invulnerable) tags.Add("invulnerable");
            if (t.Pickaxe) tags.Add("pickaxe");
            if (t.NeedsBomb) tags.Add("bomb");
            string tagText = tags.Count > 0 ? $" [{string.Join(", ", tags)}]" : "";
            sb.AppendLine($"- {mon.Name} (HP {mon.Health}, damage {mon.DamageToFarmer}, defense {mon.resilience.Value}, {dist:0} tiles){tagText} — {t.Role}: {t.Advice}");
        }
        sb.Append("Use monsters(name) for the base stats of one kind; fight(seconds) does the swinging, monsters only tells you what to expect.");
        return (true, sb.ToString());
    }

    /// <summary>The held item, with the weapon's real damage range: the number that decides how long a fight will take.</summary>
    private static string HeldLine(Farmer p)
    {
        var held = p.CurrentTool;
        if (held is MeleeWeapon w)
            return $"Held: {w.DisplayName} (damage {w.minDamage.Value}-{w.maxDamage.Value})";
        if (held != null)
            return $"Held: {held.DisplayName} (not a weapon)";
        return p.CurrentItem == null ? "Held: nothing" : $"Held: {p.CurrentItem.DisplayName} (not a weapon)";
    }

    // ------------------------------------------------------------------ codex

    private sealed record Fact(string Key, string Name, int Health, int Damage, int Defense, bool Flying);

    private static (bool ok, string text) Codex(string query)
    {
        string q = MonsterRules.Normalize(query);
        var facts = LoadFacts();
        var exact = facts.Where(f => MonsterRules.Normalize(f.Name) == q || MonsterRules.Normalize(f.Key) == q).ToList();
        var matches = (exact.Count > 0
                ? exact
                : facts.Where(f => MonsterRules.Normalize(f.Name).Contains(q) || MonsterRules.Normalize(f.Key).Contains(q)).Take(8).ToList())
            // Truffle Crab, Lava Crab and Iridium Crab all carry the display name "Rock Crab" in Data/Monsters; one
            // codex line for the family is enough, and asking for the variant by its key still finds it above.
            .GroupBy(f => MonsterRules.Normalize(f.Name)).Select(g => g.First()).ToList();

        if (matches.Count == 0)
        {
            // A live variant (or a modded monster) may still be on the map even when Data/Monsters has no display-name match.
            var live = Game1.currentLocation?.characters.OfType<Monster>()
                .Where(m => MonsterRules.Normalize(m.Name) == q || MonsterRules.Normalize(m.GetType().Name) == q)
                .GroupBy(m => m.Name).Select(g => g.First()).Take(8).ToList() ?? new List<Monster>();
            if (live.Count == 0)
                return (false, $"No monster called \"{query}\" in the game data or on this map. Use monsters() to see what is here; try the English name, e.g. Green Slime, Rock Crab, Serpent, Mummy.");
            var sbLive = new StringBuilder();
            foreach (var mon in live)
            {
                var t = MonsterRules.ForLive(mon.GetType().Name, mon.Name);
                sbLive.AppendLine($"{mon.Name} — HP {mon.Health}/{mon.MaxHealth}, damage {mon.DamageToFarmer}, defense {mon.resilience.Value} · {t.Role}");
                sbLive.AppendLine($"  {t.Advice}");
            }
            return (true, sbLive.ToString().TrimEnd());
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Monster codex · {matches.Count} match(es)");
        foreach (var f in matches)
        {
            var t = MonsterRules.ForName(f.Name);
            if (t.Advice.StartsWith("Close in and swing;", StringComparison.Ordinal))
            {
                // A localised display name does not match the English rules table; the data key usually does.
                var byKey = MonsterRules.ForName(f.Key);
                if (!byKey.Advice.StartsWith("Close in and swing;", StringComparison.Ordinal)) t = byKey;
            }
            sb.AppendLine($"{f.Name} — base HP {f.Health}, damage {f.Damage}, defense {f.Defense}{(f.Flying || t.Flying ? ", flying" : "")} · {t.Role}");
            sb.AppendLine($"  {t.Advice}");
            if (t.Pickaxe) sb.AppendLine("  Weapon: Pickaxe on the shell first; a sword only clinks.");
            if (t.NeedsBomb) sb.AppendLine("  Finish: a bomb (or a Crusader weapon); a normal weapon only knocks it down.");
            if (t.Invulnerable) sb.AppendLine("  Do not fight it: it cannot be killed.");
        }
        sb.Append("Base stats come from the game's Data/Monsters; a monster on this map may have variant stats. monsters() shows the live HP and damage.");
        return (true, sb.ToString());
    }

    /// <summary>Data/Monsters, including modded entries: health/damage/defense are fields 0/1/7, display name field 14.</summary>
    private static List<Fact> LoadFacts()
    {
        var list = new List<Fact>();
        foreach (var kv in DataLoader.Monsters(Game1.content))
        {
            var f = (kv.Value ?? "").Split('/');
            if (f.Length < 8) continue;
            string name = f.Length > 14 && f[14].Trim().Length > 0 ? f[14].Trim() : kv.Key;
            list.Add(new Fact(kv.Key, name, Int(f[0]), Int(f[1]), Int(f[7]), Bool(f[4])));
        }
        return list;
    }

    private static int Int(string s) =>
        int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;

    private static bool Bool(string s) =>
        bool.TryParse(s.Trim(), out bool b) && b;
}
