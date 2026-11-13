using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Tools;
using Monster = StardewValley.Monsters.Monster;
using SObject = StardewValley.Object;

namespace StardewMCP.Agent;

/// <summary>
/// Combat. fight() plays the weapon like a player: equip the best one, walk to the nearest monster, face it and
/// swing with the game's own <c>Farmer.BeginUsingTool</c>, so damage, knockback, loot and combat XP are the game's.
/// The AI decides when to fight and when to stop. The weapon, monster and health rules are small internal helpers
/// so other tools (mine survival, interrupts) use the same ones.
/// </summary>
public static partial class AgentTools
{
    /// <summary>Monsters farther than this many tiles are not fought.</summary>
    internal const int FightSightTiles = 12;

    /// <summary>fight() never fights on at or below this health, however high the maximum is.</summary>
    internal const int FightStopHealthFloor = 15;

    /// <summary>fight() also stops at this percent of the maximum health (30% of 100 is 30, of 125 is 37).</summary>
    internal const int FightStopHealthPercent = 30;

    /// <summary>Within this many tiles of its target the farmer stops walking and swings.</summary>
    private const float FightMeleeTiles = 2f;
    private const int FightSwingTicks = 18;
    private const int FightReplanTicks = 20;
    private const int FightPathSteps = 5;
    private const int FightIgnoreTicks = 90;

    private static ToolDef CombatTool() => new()
    {
        Name = "fight",
        Description = "Fight monsters near you with type-specific spacing and line-of-sight checks: support casters are prioritised, chargers and ranged monsters use hit-and-run, flyers are engaged when they enter reach, and blocked attacks reposition instead of swinging through stone. " +
                      "Stops early when no monster is left in range, when your health is low (30% of your maximum, never less than 15) or when a menu opens. About 1.4 game minutes pass per second. " +
                      "Check Health in status before you start. With eat_below it eats the best health-restoring food from your inventory whenever your health falls below that percent, then keeps fighting. " +
                      "The result lists the monsters defeated, the damage you took, the food eaten and the loot.",
        Schema = Obj(new
        {
            seconds = Int("Optional: how long to fight, 3-25 (default 10)"),
            eat_below = Int("Optional health percent, 1-99: when your health is below it, eat the best health-restoring food you carry, then keep fighting"),
            reason = Str(ReasonDesc)
        }, "reason"),
        Run = Fight
    };

    // ------------------------------------------------------------------ helpers (shared with other combat tools)

    /// <summary>
    /// Health at or below which fight stops: <c>max(15, 30% of the maximum health)</c>. One place to change the rule.
    /// Integer maths, so 30% of 100 is exactly 30 (health is a whole number, so "at or below 37.5" is "at or below 37").
    /// </summary>
    internal static int FightStopHealth(Farmer who) => Math.Max(FightStopHealthFloor, who.maxHealth * FightStopHealthPercent / 100);

    internal static bool IsHealthLow(Farmer who) => who.health <= FightStopHealth(who);

    internal static float TileDistance(Point a, Point b) => Vector2.Distance(a.ToVector2(), b.ToVector2());

    /// <summary>
    /// Inventory slot of the weapon to fight with: the melee weapon with the highest max damage, a scythe only when
    /// there is nothing else. -1 when the farmer carries no weapon.
    /// </summary>
    internal static int BestWeaponSlot(Farmer who)
    {
        int Damage(int slot) => ((MeleeWeapon)who.Items[slot]).maxDamage.Value;

        int best = -1, bestScythe = -1;
        for (int i = 0; i < who.Items.Count; i++)
        {
            if (who.Items[i] is not MeleeWeapon w) continue;
            if (w.Name.Contains("Scythe", StringComparison.OrdinalIgnoreCase))
            {
                if (bestScythe < 0 || Damage(i) > Damage(bestScythe)) bestScythe = i;
            }
            else if (best < 0 || Damage(i) > Damage(best))
                best = i;
        }
        if (best >= 0) return best;
        if (bestScythe >= 0) return bestScythe;
        for (int i = 0; i < who.Items.Count; i++)
            if (who.Items[i] is Tool t && t.Name.Contains("Scythe", StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    /// <summary>
    /// Inventory slot of the edible item that restores the most health (edible by the eat tool's rule, Edibility above
    /// -300), or -1 when the farmer carries nothing that heals.
    /// </summary>
    internal static int BestHealingFoodSlot(Farmer who)
    {
        int best = -1, bestHealth = 0;
        for (int i = 0; i < who.Items.Count; i++)
        {
            if (who.Items[i] is not SObject food || food.Edibility <= -300) continue;
            int healing = food.healthRecoveredOnConsumption();
            if (healing > bestHealth) { best = i; bestHealth = healing; }
        }
        return best;
    }

    /// <summary>The living monster closest to <paramref name="from"/> within <paramref name="maxTiles"/> tiles, or null.</summary>
    internal static Monster? NearestMonster(GameLocation loc, Point from, float maxTiles, Func<Monster, bool>? skip = null)
    {
        Monster? best = null;
        float bestDist = maxTiles;
        foreach (var npc in loc.characters)
        {
            if (npc is not Monster mon || mon.Health <= 0 || (skip != null && skip(mon))) continue;
            float d = TileDistance(from, mon.TilePoint);
            if (d > maxTiles || (best != null && d >= bestDist)) continue;
            best = mon;
            bestDist = d;
        }
        return best;
    }

    internal static bool HasClearMeleePath(GameLocation loc, Point from, Point target) =>
        CombatRules.LineClear(from.X, from.Y, target.X, target.Y,
            (x, y) => Pathfinder.IsWalkable(loc, x, y, ignoreVillagers: true));

    private static MonsterRules.Tactic TacticFor(Monster mon) => MonsterRules.ForLive(mon.GetType().Name, mon.displayName);

    /// <summary>Nearest target, with support casters prioritised over ordinary closer monsters.</summary>
    private static Monster? FightTarget(GameLocation loc, Point from, Func<Monster, bool> skip)
    {
        Monster? best = null;
        float bestScore = float.MaxValue;
        foreach (var npc in loc.characters)
        {
            if (npc is not Monster mon || mon.Health <= 0 || skip(mon)) continue;
            float distance = TileDistance(from, mon.TilePoint);
            if (distance > FightSightTiles) continue;
            float score = CombatRules.TargetPriority(TacticFor(mon), distance);
            if (best != null && score >= bestScore) continue;
            best = mon;
            bestScore = score;
        }
        return best;
    }

    private static Stack<Point>? StepsTo(GameLocation loc, Point me, List<Vector2> route, int maxSteps)
    {
        var steps = new Stack<Point>();
        for (int i = Math.Min(route.Count, maxSteps) - 1; i >= 0; i--)
            steps.Push(new Point((int)route[i].X, (int)route[i].Y));
        return steps;
    }

    /// <summary>A short route to a walkable attack tile with a clear line to the monster, or null when none exists.</summary>
    private static Stack<Point>? ApproachSteps(GameLocation loc, Monster mon, CombatStyle style, int maxSteps)
    {
        var me = P.TilePoint;
        var goal = mon.TilePoint;
        float wanted = CombatRules.DesiredRange(style);
        var candidates = new List<Point>();
        int radius = (int)Math.Ceiling(wanted + 1);
        for (int x = goal.X - radius; x <= goal.X + radius; x++)
        for (int y = goal.Y - radius; y <= goal.Y + radius; y++)
        {
            var q = new Point(x, y);
            float distance = TileDistance(q, goal);
            if (distance < Math.Max(1f, wanted - 0.75f) || distance > wanted + 0.75f) continue;
            if (q != me && !Pathfinder.IsWalkable(loc, x, y)) continue;
            if (!HasClearMeleePath(loc, q, goal)) continue;
            candidates.Add(q);
        }

        List<Vector2>? best = null;
        float bestScore = float.MaxValue;
        foreach (var q in candidates)
        {
            if (q == me) return new Stack<Point>();
            var path = new Pathfinder().FindPath(loc, me.ToVector2(), q.ToVector2());
            if (path == null) continue;
            float score = path.Count + Math.Abs(TileDistance(q, goal) - wanted) * 2;
            if (best == null || score < bestScore) { best = path; bestScore = score; }
        }
        return best == null ? null : StepsTo(loc, me, best, maxSteps);
    }

    /// <summary>After a hit-and-run swing, step away instead of trading the next blow. Ranged styles prefer cover.</summary>
    private static Stack<Point>? RetreatSteps(GameLocation loc, Monster mon, CombatStyle style, int maxSteps)
    {
        var me = P.TilePoint;
        var goal = mon.TilePoint;
        List<Vector2>? best = null;
        float bestScore = float.MaxValue;
        foreach (var d in Dirs)
        for (int n = 3; n <= 5; n++)
        {
            var q = new Point(me.X + d.X * n, me.Y + d.Y * n);
            if (!Pathfinder.IsWalkable(loc, q.X, q.Y) || TileDistance(q, goal) < 2.5f) continue;
            var path = new Pathfinder().FindPath(loc, me.ToVector2(), q.ToVector2());
            if (path == null) continue;
            bool exposed = HasClearMeleePath(loc, q, goal);
            float score = path.Count - TileDistance(q, goal) + (style == CombatStyle.Ranged && exposed ? 3 : 0);
            if (best == null || score < bestScore) { best = path; bestScore = score; }
        }
        return best == null ? null : StepsTo(loc, me, best, maxSteps);
    }

    // The two checks below are ported from Pichaya31847/stardew-ai-agent (MIT, commit 464e777; their days 58-101: whole
    // fights spent swinging at rock crab shells and armored bugs).

    /// <summary>A Rock Crab (or its Lava and Iridium kinds) still in its shell: a weapon only clinks on it, the Pickaxe cracks it (the game's RockCrab.hitWithTool).</summary>
    private static bool InShell(Monster mon) => mon is StardewValley.Monsters.RockCrab { shellGone.Value: false, isStickBug.Value: false };

    /// <summary>
    /// Nothing a weapon can do right now: an Armored Bug, a downed Mummy (it needs a bomb or a Crusader weapon to finish
    /// it, but a standing one can be knocked down first), an invulnerable Spiker, a Duggy under the ground. The family
    /// table decides the invulnerable cases the same way the codex does.
    /// </summary>
    private static bool CannotBeHit(Monster mon) =>
        mon.IsInvisible ||
        mon is StardewValley.Monsters.Bug { isArmoredBug.Value: true } ||
        mon is StardewValley.Monsters.Mummy { reviveTimer.Value: > 0 } ||
        mon is StardewValley.Monsters.LavaLurk { currentState.Value: StardewValley.Monsters.LavaLurk.State.Submerged } ||
        MonsterRules.ForClass(mon.GetType().Name) is { Invulnerable: true };

    /// <summary>
    /// For <see cref="SelfDefense"/>: this monster can be left alone, because no held item would change it — an armored
    /// bug, a downed mummy, a Duggy underground. A rock crab in its shell is *not* one of these: the pickaxe cracks it,
    /// and <see cref="DefenseSlot"/> picks the right item for it.
    /// </summary>
    internal static bool WeaponCannotHurt(Monster mon) => CannotBeHit(mon);

    /// <summary>
    /// The slot to swing at <paramref name="mon"/> with: the pickaxe for a rock crab still in its shell (the game's
    /// <c>RockCrab.hitWithTool</c> cracks it and a weapon only clinks), otherwise the best melee weapon. -1 when the
    /// farmer carries nothing that helps.
    /// </summary>
    internal static int DefenseSlot(Farmer who, Monster mon)
    {
        if (InShell(mon))
        {
            int pickaxe = who.Items.ToList().FindIndex(i => i is Pickaxe);
            return pickaxe; // -1: no pickaxe, so nothing to crack the shell with
        }
        return BestWeaponSlot(who);
    }

    /// <summary>
    /// How close <paramref name="mon"/> has to be for the item <see cref="DefenseSlot"/> picks to reach it: a pickaxe
    /// swings at the tile in front (1.5 tiles, as <c>fight</c> uses for rock crab shells), a weapon reaches 2. Using
    /// the weapon range for the pickaxe made every defensive swing at a shelled rock crab miss.
    /// </summary>
    internal static float DefenseReach(Monster mon) => DefenseRules.ReachFor(InShell(mon));

    /// <summary>Swings at one monster with no change in its health before the fight leaves it alone.</summary>
    private const int FightNoEffectSwings = 8;

    // ------------------------------------------------------------------ fight

    private static IEnumerator Fight(JsonElement a, CallContext c)
    {
        int seconds = Math.Clamp(I(a, "seconds", 10), 3, 25);
        int eatBelow = Math.Clamp(I(a, "eat_below", 0), 0, 99); // 0 = never eat
        int budget = seconds * 60;
        var loc = Loc;

        yield return WaitIdle();
        int slot = BestWeaponSlot(P);
        if (slot < 0) { c.Fail("You have no weapon in your inventory (sword, club, dagger or scythe)."); yield break; }
        P.CurrentToolIndex = slot;
        var weapon = P.Items[slot];
        int pickaxe = P.Items.ToList().FindIndex(i => i is Pickaxe);
        var hits = new Dictionary<Monster, (int health, int swings)>(ReferenceEqualityComparer.Instance);
        var noEffect = new HashSet<Monster>(ReferenceEqualityComparer.Instance);
        var obstructed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int shellHits = 0;
        int retreatUntil = 0;

        var before = Snapshot();
        int healthStart = P.health;
        var engaged = new Dictionary<Monster, string>(ReferenceEqualityComparer.Instance);
        var ignoreUntil = new Dictionary<Monster, int>(ReferenceEqualityComparer.Instance);
        int swings = 0, nextSwing = 0, nextPlan = 0;
        string stop = "";
        bool allClear = false;
        var eaten = new List<string>();
        bool noFood = false;

        int t = 0;
        for (; t < budget; t++)
        {
            if (Game1.activeClickableMenu != null || Game1.eventUp) { stop = "Stopped: a menu, dialogue or cutscene opened."; break; }
            if (Game1.currentLocation != loc || Game1.isWarping) { stop = "Stopped: you left the map."; break; }
            if (eatBelow > 0 && P.health * 100 < P.maxHealth * eatBelow && !P.UsingTool && !P.isEating)
            {
                // Before the low-health stop: food that heals is better than quitting. The game's own eating path, like the eat tool.
                int food = BestHealingFoodSlot(P);
                if (food < 0)
                    noFood = true;
                else
                {
                    var item = P.Items[food];
                    var foodName = item.DisplayName;
                    int stackBefore = item.Stack;
                    P.controller = null;
                    P.Halt();
                    yield return WaitIdle(60);
                    yield return EatSlot(food);
                    if (P.Items[food] == item && item.Stack >= stackBefore)
                    {
                        stop = $"Stopped: eating {foodName} did not work.";
                        break;
                    }
                    eaten.Add(foodName);
                    continue;
                }
            }
            if (IsHealthLow(P))
            {
                stop = $"Stopped: health {P.health}/{P.maxHealth} is at or below {FightStopHealth(P)}. Eat food or call leave_mine before fighting on.";
                break;
            }

            var me = P.TilePoint;
            // A monster with no walkable route is skipped for a while, unless it has come within reach by itself.
            var mon = FightTarget(loc, me,
                m => CannotBeHit(m) || noEffect.Contains(m) || ignoreUntil.TryGetValue(m, out var until) && t < until && TileDistance(me, m.TilePoint) > FightMeleeTiles);
            if (mon == null)
            {
                if (NearestMonster(loc, me, FightSightTiles, m => CannotBeHit(m) || noEffect.Contains(m)) == null)
                {
                    stop = NearestMonster(loc, me, FightSightTiles) == null
                        ? $"All clear: no monsters within {FightSightTiles} tiles."
                        : $"Nothing left within {FightSightTiles} tiles that your weapon can hurt.";
                    allClear = true;
                    break;
                }
                yield return null; // only monsters with no route in view: give them time to come closer
                continue;
            }
            engaged.TryAdd(mon, mon.Name);
            var style = CombatRules.StyleFor(TacticFor(mon));

            if (t < retreatUntil)
            {
                if (P.controller == null && !P.UsingTool && t >= nextPlan)
                {
                    var away = RetreatSteps(loc, mon, style, FightPathSteps);
                    if (away != null && away.Count > 0)
                    {
                        P.controller = new WalkController(away, P, loc);
                        P.setRunning(true, true);
                    }
                    else retreatUntil = t; // nowhere to step back: re-evaluate the attack angle instead of freezing
                    nextPlan = t + FightReplanTicks;
                }
                yield return null;
                continue;
            }

            bool crack = pickaxe >= 0 && InShell(mon);
            float reach = crack ? 1.5f : FightMeleeTiles;
            bool clear = HasClearMeleePath(loc, me, mon.TilePoint);
            if (TileDistance(me, mon.TilePoint) <= reach && clear)
            {
                if (P.controller != null)
                {
                    P.controller = null;
                    P.Halt();
                }
                if (!P.UsingTool && P.CanMove && t >= nextSwing)
                {
                    if (crack) shellHits++;
                    else
                    {
                        // Its health has not moved in FightNoEffectSwings swings: stop hitting it and say so.
                        hits.TryGetValue(mon, out var seen);
                        seen = seen.swings == 0 || seen.health != mon.Health ? (mon.Health, 1) : (seen.health, seen.swings + 1);
                        hits[mon] = seen;
                        if (seen.swings > FightNoEffectSwings) { noEffect.Add(mon); yield return null; continue; }
                    }
                    P.CurrentToolIndex = crack ? pickaxe : slot;
                    Face(mon.TilePoint);
                    P.lastClick = new Vector2(mon.StandingPixel.X, mon.StandingPixel.Y);
                    P.BeginUsingTool();
                    swings++;
                    nextSwing = t + FightSwingTicks;
                    if (CombatRules.RetreatAfterSwing(style)) retreatUntil = t + 20;
                }
            }
            else if (!P.UsingTool && t >= nextPlan)
            {
                // Re-plan around obstacles and to the type's attack range; never swing through a blocked line.
                var steps = ApproachSteps(loc, mon, style, FightPathSteps);
                if (steps == null)
                {
                    ignoreUntil[mon] = t + FightIgnoreTicks;
                    if (!clear) obstructed.Add(mon.Name);
                }
                else if (steps.Count > 0)
                {
                    P.controller = new WalkController(steps, P, loc);
                    P.setRunning(true, true);
                }
                nextPlan = t + FightReplanTicks;
            }
            yield return null;
        }

        if (P.controller != null)
        {
            P.controller = null;
            if (!P.UsingTool) P.Halt();
        }
        yield return WaitIdle(90); // let the last swing finish
        if (stop.Length == 0 || allClear)
            yield return Ticks(30); // loot flies to the farmer

        if (stop.Length == 0) stop = TimeUpText(loc);
        var defeated = engaged
            .Where(kv => kv.Key.Health <= 0 || !loc.characters.Any(n => ReferenceEquals(n, kv.Key)))
            .GroupBy(kv => kv.Value)
            .Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key)
            .ToList();

        string weaponDamage = weapon is StardewValley.Tools.MeleeWeapon mw ? $", damage {mw.minDamage.Value}-{mw.maxDamage.Value}" : "";
        var sb = new StringBuilder($"Fought {t / 60}s with {weapon.DisplayName}{weaponDamage} ({swings} swings). {stop}");
        sb.Append($"\nDefeated: {(defeated.Count > 0 ? string.Join(", ", defeated) : "none")}.");
        if (shellHits > 0) sb.Append($"\nPickaxe on rock crab shells: {shellHits} hit(s). A weapon only clinks on a shell.");
        if (noEffect.Count > 0) sb.Append($"\nNo effect after {FightNoEffectSwings} swings, left alone: {string.Join(", ", noEffect.Select(m => m.Name).Distinct())}.");
        if (obstructed.Count > 0) sb.Append($"\nLine of sight blocked: {string.Join(", ", obstructed)}. Repositioned instead of swinging through obstacles.");
        if (eaten.Count > 0) sb.Append($"\nAte: {string.Join(", ", eaten)}.");
        if (noFood) sb.Append("\neat_below: you carry no (more) food that restores health.");
        sb.Append($"\nHealth {healthStart} → {P.health}/{P.maxHealth}.");
        var diff = Diff(before);
        if (diff.Length > 0) sb.Append('\n').Append(diff);
        c.Result = sb.ToString();
    }

    /// <summary>What to tell the AI when the time ran out: whether anything is left to fight.</summary>
    private static string TimeUpText(GameLocation loc)
    {
        if (Game1.currentLocation != loc) return "Time is up.";
        var me = P.TilePoint;
        var near = NearestMonster(loc, me, FightSightTiles);
        if (near == null) return $"Time is up. All clear: no monsters within {FightSightTiles} tiles.";
        return $"Time is up. {near.Name} (HP {near.Health}) is still {TileDistance(me, near.TilePoint):0} tiles away at ({near.TilePoint.X},{near.TilePoint.Y}); call fight again to keep going.";
    }
}
