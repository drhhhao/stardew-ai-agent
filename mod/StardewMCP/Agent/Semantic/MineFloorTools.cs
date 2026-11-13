using System;
using System.Collections;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using SObject = StardewValley.Object;

namespace StardewMCP.Agent;

/// <summary>Resource-first mine progression: gather, fight only when needed, then take the down ladder to the next elevator.</summary>
public static partial class AgentTools
{
    internal static IEnumerator MineFloorSemantic(JsonElement a, CallContext c)
    {
        if (Loc is not MineShaft start)
        {
            c.Fail(Reasons.NotHere, "mine_floor works inside the Mines. travel_to_location Mine first.");
            yield break;
        }
        int startFloor = start.mineLevel;
        int target = MineRules.NextElevatorFloor(startFloor);
        if (target == startFloor)
        {
            c.NoChange("You are already at the bottom of the Mines (floor 120). Gather or fight with use_tool / fight instead.");
            yield break;
        }

        var before = Snapshot();
        string stop = "";
        while (Loc is MineShaft current && current.mineLevel < target)
        {
            if (Game1.activeClickableMenu != null) { stop = "a menu opened"; break; }
            if (IsHealthLow(P)) { stop = $"health {P.health}/{P.maxHealth} is low"; break; }

            yield return GatherMineLoot(c);
            if (IsHealthLow(P)) { stop = $"health {P.health}/{P.maxHealth} is low"; break; }

            if (NearestMonster(Loc, P.TilePoint, 6) != null)
                yield return Fight(ArgsOf(new { seconds = 8, eat_below = 50, reason = "Fight only monsters threatening the mining route" }), c);
            if (IsHealthLow(P)) { stop = $"health {P.health}/{P.maxHealth} is low"; break; }

            var ladder = FindDownLadder(Loc);
            if (ladder == null)
            {
                yield return MinePlainStonesForLadder(c);
                ladder = FindDownLadder(Loc);
            }
            if (ladder == null && Loc is MineShaft infested && infested.mustKillAllMonstersToAdvance())
            {
                yield return Fight(ArgsOf(new { seconds = 15, eat_below = 50, reason = "Clear the infested floor to reveal the ladder" }), c);
                ladder = FindDownLadder(Loc);
            }
            if (ladder == null)
            {
                stop = "no down ladder was revealed yet; gather more stones or clear the remaining monsters first";
                break;
            }

            int beforeFloor = ((MineShaft)Loc).mineLevel;
            yield return UseDownLadder(ladder.Value, c);
            if (!c.Ok) { stop = c.Result; break; }
            if (Loc is not MineShaft after || after.mineLevel <= beforeFloor)
            {
                stop = "the down ladder did not change the floor";
                break;
            }
        }

        int reached = Loc is MineShaft end ? end.mineLevel : startFloor;
        c.Partial = reached < target;
        c.Changed = reached > startFloor;
        var text = $"Mine goal: floor {startFloor} → {reached} (next elevator: {target}).";
        if (stop.Length > 0) text += $" Stopped: {stop}.";
        text += "\n" + Diff(before);
        if (reached < target && stop.Length > 0)
            c.Reason ??= stop.Contains("health") ? Reasons.Interrupted : Reasons.NoPath;
        c.Result = text;
    }

    private static IEnumerator GatherMineLoot(CallContext c)
    {
        var loose = Loc.Objects.Pairs
            .Where(kv => WorldText.IsLoose(kv.Value))
            .Select(kv => kv.Key.ToPoint())
            .OrderBy(t => Math.Abs(t.X - P.TilePoint.X) + Math.Abs(t.Y - P.TilePoint.Y))
            .Take(20).ToList();
        if (loose.Count > 0)
            yield return Harvest(ArgsOf(new { tiles = loose, reason = "Collect loose mine resources first" }), c);

        var nodes = Loc.Objects.Pairs
            .Where(kv => kv.Value.IsBreakableStone() && MineRules.IsValuableNode(WorldText.StoneLabel(kv.Value.ItemId)))
            .Select(kv => kv.Key.ToPoint())
            .OrderBy(t => Math.Abs(t.X - P.TilePoint.X) + Math.Abs(t.Y - P.TilePoint.Y))
            .Take(16).ToList();
        if (nodes.Count > 0)
            yield return UseTool(ArgsOf(new { tool = "Pickaxe", tiles = nodes, max_hits = 12, reason = "Mine valuable ore and gem nodes before plain stone" }), c);
    }

    private static IEnumerator MinePlainStonesForLadder(CallContext c)
    {
        var stones = Loc.Objects.Pairs
            .Where(kv => kv.Value.IsBreakableStone() && !MineRules.IsValuableNode(WorldText.StoneLabel(kv.Value.ItemId)))
            .Select(kv => kv.Key.ToPoint())
            .OrderBy(t => Math.Abs(t.X - P.TilePoint.X) + Math.Abs(t.Y - P.TilePoint.Y))
            .Take(24).ToList();
        if (stones.Count > 0)
            yield return UseTool(ArgsOf(new { tool = "Pickaxe", tiles = stones, max_hits = 8, reason = "Mine plain stones to reveal the down ladder" }), c);
    }

    /// <summary>Nearest down-ladder tile (index 173), or null when the floor has not revealed one.</summary>
    internal static Point? FindDownLadder(GameLocation loc)
    {
        int w = loc.Map.Layers[0].LayerWidth, h = loc.Map.Layers[0].LayerHeight;
        var me = P.TilePoint;
        Point? best = null;
        int bestDist = int.MaxValue;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (loc.getTileIndexAt(new xTile.Dimensions.Location(x, y), "Buildings", "mine") != 173) continue;
            int d = Math.Abs(x - me.X) + Math.Abs(y - me.Y);
            if (d < bestDist) { bestDist = d; best = new Point(x, y); }
        }
        return best;
    }

    private static IEnumerator UseDownLadder(Point ladder, CallContext c)
    {
        var floor = Loc;
        yield return WalkAdjacent(ladder, c);
        if (!c.Ok) yield break;
        PressAction(ladder);
        for (int i = 0; i < 300 && ReferenceEquals(Loc, floor); i++) yield return null;
        yield return WaitIdle();
        if (ReferenceEquals(Loc, floor)) c.Fail(Reasons.NoPath, "The down ladder did not take you to the next floor.");
    }
}
