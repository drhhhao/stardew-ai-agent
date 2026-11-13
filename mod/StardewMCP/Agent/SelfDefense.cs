using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;

namespace StardewMCP.Agent;

/// <summary>
/// Keeps an AI's farmer alive while it is between actions, without pausing anything.
/// <para>
/// The freeze is about time, not safety: even with <see cref="ModConfig.FreezeTimeWhileThinking"/> on,
/// a single-player run only holds the clock, so monsters keep moving and keep hitting a farmer that
/// stands still. A slow model therefore paid for thinking in health — day 5 of the first real run:
/// 97/100 HP when a <c>fight</c> ended, 34/100 when the AI's next call began.
/// </para>
/// <para>
/// So the farmer does what a player keeps doing while deciding what to do next: with no action running,
/// a monster that reaches it is hit with the best item for it, and when health is low it eats, and with
/// no food left it backs away. It never starts a fight of its own and never chases, and it pauses
/// nothing, so it keeps working on a farm that other players share — which a freeze could not.
/// </para>
/// </summary>
public static class SelfDefense
{
    private sealed class State
    {
        /// <summary>Ticks until the next swing (fight's own rhythm).</summary>
        public int Cooldown;
        /// <summary>Ticks until the next attempt to find a way away from a monster (pathfinding is not free).</summary>
        public int FleeCooldown;
        /// <summary>Ticks until another bite after one the game refused (a fullness buff would otherwise retry every tick).</summary>
        public int EatCooldown;
        /// <summary>The farm day the counts below belong to.</summary>
        public int Day;
        /// <summary>Slot the AI held before a defensive action, given back when that action is over (-1 = nothing held).</summary>
        public int HeldIndex = -1;
        /// <summary>The walk this module started, so only that one is ever stopped here.</summary>
        public WalkController? Retreat;
        /// <summary>The bite in progress: name, slot, stack and health before it, so its result can be reported after the game's animation.</summary>
        public string? Eating;
        public int EatingSlot = -1;
        public int EatingStack;
        public int EatingHealth;
        /// <summary>The monster that made the farmer eat, and its distance then (for the line the AI reads).</summary>
        public string EatingThreat = "";
        public float EatingThreatTiles;
        public int Swings;
        public int Eaten;
        public int Retreats;
        /// <summary>Health at the previous tick, to tell the AI why it dropped while nobody was driving the farmer.</summary>
        public int LastHealth;
        public DateTime LastNote = DateTime.MinValue;
        /// <summary>Last of the lines that must not be missed (low health, nothing to eat), throttled on their own.</summary>
        public DateTime LastImportant = DateTime.MinValue;
        /// <summary>Lines waiting for the AI's next tool result (nobody was there to answer while it thought).</summary>
        public readonly List<string> Pending = new();
    }

    private static readonly State[] States = { new(), new(), new(), new() };

    /// <summary>Forget the day's counts (a new save). Registered with <see cref="GameSession.OnEnd"/>.</summary>
    public static void Reset()
    {
        foreach (var s in States)
        {
            s.Cooldown = 0;
            s.FleeCooldown = 0;
            s.EatCooldown = 0;
            s.Day = 0;
            s.HeldIndex = -1;
            s.Retreat = null;
            s.Eating = null;
            s.EatingSlot = -1;
            s.EatingStack = 0;
            s.EatingHealth = 0;
            s.EatingThreat = "";
            s.EatingThreatTiles = 0;
            s.Swings = 0;
            s.Eaten = 0;
            s.Retreats = 0;
            s.LastHealth = 0;
            s.LastNote = DateTime.MinValue;
            s.LastImportant = DateTime.MinValue;
            s.Pending.Clear();
        }
    }

    /// <summary>"3 swings, ate 1, backed away once" for the day's stats; "" when it never had to.</summary>
    public static string Summary(PlayerSlot slot)
    {
        var s = States[Math.Clamp(slot.Screen, 0, States.Length - 1)];
        return DefenseRules.Summary(s.Swings, s.Eaten, s.Retreats);
    }

    /// <summary>
    /// Called every tick on the seat's own screen, right after its runtime ticked. Game thread.
    /// Only an AI seat that somebody plays, between actions, in a running world, does anything.
    /// </summary>
    public static void Tick(PlayerSlot seat, ModConfig config)
    {
        if (!config.SelfDefense || seat.Role != SeatRole.Ai || !seat.Connected) return;
        var state = States[Math.Clamp(seat.Screen, 0, States.Length - 1)];
        if (state.Cooldown > 0) state.Cooldown--;
        if (state.FleeCooldown > 0) state.FleeCooldown--;
        if (state.EatCooldown > 0) state.EatCooldown--;

        var p = Game1.player;
        var loc = Game1.currentLocation;
        if (p == null || loc == null) return;

        // The game restores health when the eating animation ends, not when eatHeldObject starts: report the bite now
        // that there is a real number to report.
        FinishEating(seat, state, p);
        int healthBefore = state.LastHealth;
        state.LastHealth = p.health;

        if (Context.IsWorldReady && state.Day != AgentLog.FarmDay)
        {
            // One line a day in the log, so a viewer can see the farmer is not being beaten up while its AI thinks.
            if (state.Day > 0 && Summary(seat).Length > 0)
                seat.Runtime.Log.Add("system", text: $"Self-defense on day {state.Day}: {Summary(seat)} (no action was running)");
            state.Day = AgentLog.FarmDay;
            state.Swings = 0;
            state.Eaten = 0;
            state.Retreats = 0;
            state.Pending.Clear(); // a new day's tool result must not open with yesterday's fight notes
        }

        bool defend = DefenseRules.ShouldDefend(
            enabled: true, isAiSeat: true, connected: true,
            worldReady: Context.IsWorldReady && !Game1.fadeToBlack && !Game1.isWarping && !Game1.eventUp,
            actionRunning: seat.Runtime.ActionRunning, wakingUp: seat.Runtime.WakingUp, released: seat.Released,
            menuOpen: Game1.activeClickableMenu != null, paused: WorldClock.WorldPaused, holding: RunControl.Holding,
            newDay: Game1.newDay, inBed: p.isInBed.Value, dead: p.health <= 0);
        if (!defend)
        {
            StopRetreat(state);
            ReleaseHeldItem(state);
            return;
        }

        // A hit that landed before the farmer could answer: say where the health went. The AI only sees the sum when
        // it comes back, and "97 → 34 while you were thinking" with no monster named reads like a bug.
        if (healthBefore > 0 && p.health < healthBefore && !p.isEating)
        {
            var attacker = AgentTools.NearestMonster(loc, p.TilePoint, DefenseRules.ThreatTiles);
            if (attacker != null)
                Note(seat, state,
                    $"{attacker.Name} hit you while no action was running: health {healthBefore} → {p.health}/{p.maxHealth}. You hit back when it comes within reach; eat or leave_mine before it corners you.");
        }

        // Mid-swing or mid-bite: let the game finish it, then the held item goes back on a later tick.
        if (p.UsingTool || p.isEating || !p.CanMove) return;

        // Our own retreat is walking: let it finish. Replacing it every tick would mean a new route each tick and a
        // farmer that never actually moves — and backing away at low health is the point, not a reason to interrupt it.
        if (state.Retreat != null)
        {
            if (ReferenceEquals(p.controller, state.Retreat)) return;
            state.Retreat = null; // the walk ended by itself: pick the next step below
        }

        // No action is running, so any controller left on the farmer is stale (an action that was interrupted before
        // its cleanup): stop it, or the farmer keeps walking while it swings or eats.
        if (p.controller != null)
        {
            p.controller = null;
            p.Halt();
        }

        var me = p.TilePoint;
        var threat = AgentTools.NearestMonster(loc, me, DefenseRules.ThreatTiles);
        if (threat == null)
        {
            ReleaseHeldItem(state); // nothing around: the AI's own held item goes back
            return; // stand still, exactly as before
        }

        if (DefenseRules.ShouldEat(p.health, p.maxHealth, config.SelfDefenseEatPercent))
        {
            int food = AgentTools.BestHealingFoodSlot(p);
            if (food >= 0)
            {
                if (state.EatCooldown <= 0)
                {
                    Eat(seat, state, p, food, threat, me);
                    return;
                }
                // A refused bite is cooling down: swing while it waits, do not retry every tick.
            }
            // Nothing to eat and nearly out of health: the fight it is in cannot be won by swinging.
            else if (DefenseRules.ShouldFlee(p.health, p.maxHealth))
            {
                if (state.FleeCooldown <= 0)
                {
                    bool ok = StartRetreat(state, p, loc, threat);
                    if (ok)
                    {
                        Note(seat, state,
                            $"Health {p.health}/{p.maxHealth} with {threat.Name} {AgentTools.TileDistance(me, threat.TilePoint):0} tiles away and no healing food left, so you backed away from it. In the Mines, leave with leave_mine before it corners you.",
                            important: true);
                        return;
                    }

                    // Nowhere to go: do not pathfind again every tick, say it once, and keep swinging at anything in
                    // reach. Standing still at 20 HP is the one thing that surely ends in the mine entrance.
                    state.FleeCooldown = DefenseRules.SwingTicks;
                    Note(seat, state,
                        $"Health {p.health}/{p.maxHealth}, {threat.Name} {AgentTools.TileDistance(me, threat.TilePoint):0} tiles away and no healing food left, and no way to back away from it here. You keep fighting instead.",
                        important: true);
                }
            }
            else
                Note(seat, state, $"Health {p.health}/{p.maxHealth} with {threat.Name} {AgentTools.TileDistance(me, threat.TilePoint):0} tiles away and no food that restores health.", important: true);
        }

        // A shelled rock crab is cracked with the pickaxe, which only reaches the tile in front: do not swing a
        // pickaxe at it from the sword's two tiles (that swing misses, as the first in-game test showed).
        var target = AgentTools.NearestMonster(loc, me, DefenseRules.ReachTiles,
            m => AgentTools.WeaponCannotHurt(m) || AgentTools.TileDistance(me, m.TilePoint) > AgentTools.DefenseReach(m)
                 || !AgentTools.HasClearMeleePath(loc, me, m.TilePoint));
        if (target == null || state.Cooldown > 0) return;

        int slot = AgentTools.DefenseSlot(p, target);
        if (slot < 0)
        {
            Note(seat, state, $"{target.Name} is in reach and nothing in your inventory can hurt it" +
                              (target is StardewValley.Monsters.RockCrab ? " (its shell needs a Pickaxe)." : " (no weapon)."));
            return;
        }

        if (state.HeldIndex < 0 && p.CurrentToolIndex != slot) state.HeldIndex = p.CurrentToolIndex;
        p.CurrentToolIndex = slot;
        Face(p, target.TilePoint);
        p.lastClick = new Vector2(target.StandingPixel.X, target.StandingPixel.Y);
        p.BeginUsingTool();
        state.Cooldown = DefenseRules.SwingTicks;
        state.Swings++;
        string weaponName = p.Items[slot]?.DisplayName ?? "weapon";
        Note(seat, state, $"{target.Name} (HP {target.Health}) came within reach while no action was running, so you swung your {weaponName} at it. This was not a fight call: nothing was paused and you did not move.");
    }

    /// <summary>
    /// Eat the best healing food in <paramref name="slot"/> the way the game does it (<c>Farmer.eatHeldObject</c>,
    /// exactly as the <c>eat</c> tool and <c>fight</c>'s <c>eat_below</c>).
    /// </summary>
    private static void Eat(PlayerSlot seat, State state, Farmer p, int slot, Monster threat, Point me)
    {
        var item = p.Items[slot];
        var name = item.DisplayName;
        int stack = item.Stack;
        // The AI's own held item comes back once the bite is over (the food takes the slot meanwhile).
        if (state.HeldIndex < 0 && p.CurrentToolIndex != slot) state.HeldIndex = p.CurrentToolIndex;
        state.Eating = name;
        state.EatingSlot = slot;
        state.EatingStack = stack;
        state.EatingHealth = p.health;
        state.EatingThreat = threat.Name;
        state.EatingThreatTiles = AgentTools.TileDistance(me, threat.TilePoint);
        p.controller = null;
        p.Halt();
        p.CurrentToolIndex = slot;
        p.mostRecentlyGrabbedItem = item; // eatHeldObject eats what was grabbed last
        p.eatHeldObject();
        if (p.Items[slot] == item && item.Stack >= stack)
        {
            // The game refused the bite (a fullness buff, a fade, a menu): nothing will happen later either. Wait
            // long enough that an unexpired fullness buff does not turn this into a line every tick.
            ClearEating(state);
            state.EatCooldown = DefenseRules.NoteSeconds * 60;
            Note(seat, state, $"Health {p.health}/{p.maxHealth} and {threat.Name} nearby, but eating {name} did not work.", always: true);
        }
    }

    /// <summary>
    /// The game adds the health in <c>Farmer.doneEating</c>, when the eating animation ends. Report the bite then, with
    /// the real before/after numbers: a line written the moment the food was grabbed said "health 35 → 35", which reads
    /// to the AI like eating failed.
    /// </summary>
    private static void FinishEating(PlayerSlot seat, State state, Farmer p)
    {
        if (state.Eating == null || p.isEating) return;
        string name = state.Eating;
        int before = state.EatingHealth;
        int slot = state.EatingSlot;
        int stack = state.EatingStack;
        string threat = state.EatingThreat;
        float threatTiles = state.EatingThreatTiles;
        bool itemGone = slot < 0 || slot >= p.Items.Count || p.Items[slot] == null || p.Items[slot].Stack < stack;
        ClearEating(state);
        if (!itemGone)
        {
            state.EatCooldown = DefenseRules.NoteSeconds * 60;
            Note(seat, state, $"Health {before}/{p.maxHealth} and {threat} {threatTiles:0} tiles away, but eating {name} did not work.", always: true);
            return;
        }
        state.Eaten++;
        // The item was eaten. Health can still be at or below the old number when a monster landed a hit during the
        // bite; report the real number so the AI can decide to eat again or leave.
        Note(seat, state, $"You ate {name}: health {before} → {p.health}/{p.maxHealth}, {threat} {threatTiles:0} tiles away and no action was running.", always: true);
    }

    private static void ClearEating(State state)
    {
        state.Eating = null;
        state.EatingSlot = -1;
        state.EatingStack = 0;
        state.EatingHealth = 0;
        state.EatingThreat = "";
        state.EatingThreatTiles = 0;
    }

    /// <summary>
    /// Walk a few tiles away from <paramref name="threat"/>: the one thing a farmer can still do at
    /// <see cref="DefenseRules.FleePercent"/> health with nothing to eat. This is what keeps the AI's next
    /// call from opening on a passed-out farmer. False when there is nowhere to go.
    /// </summary>
    private static bool StartRetreat(State state, Farmer p, GameLocation loc, Monster threat)
    {
        var me = p.TilePoint;

        // Any walkable tile within a few steps that is farther from the monster than where we stand.
        var here = AgentTools.TileDistance(me, threat.TilePoint);
        var candidates = new List<(Point tile, float dist)>();
        for (int dy = -DefenseRules.FleeTiles; dy <= DefenseRules.FleeTiles; dy++)
            for (int dx = -DefenseRules.FleeTiles; dx <= DefenseRules.FleeTiles; dx++)
            {
                var t = new Point(me.X + dx, me.Y + dy);
                if (t == me || !Pathfinder.IsWalkable(loc, t.X, t.Y)) continue;
                float d = AgentTools.TileDistance(t, threat.TilePoint);
                if (d > here) candidates.Add((t, d));
            }
        if (candidates.Count == 0) return false;

        // Try a dozen of the farthest tiles: the single farthest is often behind a rock, and a narrow corridor only
        // opens in one direction.
        foreach (var (tile, _) in candidates.OrderByDescending(c => c.dist).Take(12))
        {
            var path = new Pathfinder().FindPath(loc, me.ToVector2(), tile.ToVector2());
            if (path == null || path.Count == 0) continue;

            var steps = new Stack<Point>();
            for (int i = Math.Min(path.Count, DefenseRules.FleeTiles) - 1; i >= 0; i--)
                steps.Push(new Point((int)path[i].X, (int)path[i].Y));
            if (steps.Count == 0) continue;

            var walk = new WalkController(steps, p, loc);
            p.controller = walk;
            p.setRunning(true, true);
            state.Retreat = walk;
            state.Retreats++;
            return true;
        }
        return false;
    }

    /// <summary>Stop a retreat this module started (never a walk the AI's own action is running).</summary>
    private static void StopRetreat(State state)
    {
        if (state.Retreat == null) return;
        var p = Game1.player;
        if (p != null && ReferenceEquals(p.controller, state.Retreat))
        {
            p.controller = null;
            p.Halt();
        }
        state.Retreat = null;
    }

    private static void ReleaseHeldItem(State state)
    {
        if (state.HeldIndex < 0) return;
        var p = Game1.player;
        if (p == null || p.UsingTool || p.isEating) return;
        p.CurrentToolIndex = state.HeldIndex;
        state.HeldIndex = -1;
    }

    /// <summary>
    /// Log what happened (at most one line per <see cref="DefenseRules.NoteSeconds"/>) and keep it for the AI's next
    /// tool result. Deliberately not a <see cref="Perception"/> event: those stop a running action, and "you swung at
    /// a slime" is no reason to stop a walk that the AI has only just started.
    /// <para><paramref name="important"/> is for the lines the AI has to read (low health, nothing to eat): they skip
    /// the log's rhythm, because the one that matters is exactly the one that would otherwise be dropped.
    /// <paramref name="always"/> is for one-off results such as a bite: eating happens once, so its line must not be
    /// swallowed by the damage line that happened to be written a second before.</para>
    /// </summary>
    private static void Note(PlayerSlot seat, State state, string text, bool important = false, bool always = false)
    {
        // The same line again from the next swing says nothing new; one copy is what the AI needs.
        if (state.Pending.Count > 0 && state.Pending[^1].EndsWith(text, StringComparison.Ordinal)) return;

        // One line per NoteSeconds, counted separately: an important line (low health, nothing to eat) must not be
        // crowded out by the swing line that came a moment before, and it must not spam the log either.
        var last = important ? state.LastImportant : state.LastNote;
        if (!always && (DateTime.UtcNow - last).TotalSeconds < DefenseRules.NoteSeconds) return;

        state.LastNote = DateTime.UtcNow;
        if (important) state.LastImportant = state.LastNote;
        seat.Runtime.Log.Add("selfdefense", text: text);
        state.Pending.Add($"[{WorldText.FormatTime(Game1.timeOfDay)}] {text}");
        while (state.Pending.Count > DefenseRules.MaxNotes) state.Pending.RemoveAt(0);
    }

    /// <summary>The lines for this seat's next tool result ("" when it had to defend nothing since the last one).</summary>
    public static string TakeNotes(PlayerSlot seat)
    {
        var state = States[Math.Clamp(seat.Screen, 0, States.Length - 1)];
        if (state.Pending.Count == 0) return "";
        var text = "While no action was running (you were thinking):\n" + string.Join("\n", state.Pending.Select(l => "  - " + l));
        state.Pending.Clear();
        return text;
    }

    /// <summary>Face a tile as <c>fight</c> does (its Face helper is private to the combat tools).</summary>
    private static void Face(Farmer p, Point target)
    {
        var me = p.TilePoint;
        int dx = target.X - me.X, dy = target.Y - me.Y;
        int dir = Math.Abs(dx) > Math.Abs(dy) ? (dx > 0 ? 1 : 3) : (dy > 0 ? 2 : 0);
        p.faceDirection(dir);
        p.FarmerSprite.StopAnimation();
    }
}
