using System;
using System.Collections;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewValley;

namespace StardewMCP.Agent;

// Semantic (intent-shaped) travel (phase 4B of docs/tool-decoupling/README.md). The legacy travel_to in
// Agent/AgentTools.cs resolves a destination in three steps — a map name, then a shop keeper (Pierre =
// SeedShop), then any villager (where they are now) — which makes "where is this person right now" a
// side effect of a travel call. The two replacements split that intent: travel_to_location takes a place
// name only (a real map or a late-game area alias; a keeper or villager name is refused) and
// travel_to_shop takes a shop's name or its keeper's (an arbitrary villager is refused). Both reuse the
// legacy route/walk core (TravelToResolved → TravelTo), so doors, opening hours, paid rides and the
// on-the-way findings cannot drift; travel_to_shop additionally stops at the shop's counter tile.

public static partial class AgentTools
{
    /// <summary>
    /// travel_to_location: walk to another map, named by a place only. Accepts a real map name and the
    /// late-game area aliases (Desert, Ginger Island, Casino...); a shop keeper's or villager's name is
    /// refused — a shop goes with travel_to_shop, a person with talk_to_villager / give_gift.
    /// </summary>
    internal static IEnumerator TravelToLocationSemantic(JsonElement a, CallContext c)
    {
        var asked = S(a, "location").Trim();
        if (IsSkullCavernName(asked)) { yield return TravelToSkullCavern(c); yield break; }
        var (dest, note) = NpcInfo.ResolveStaticPlace(asked); // maps and area aliases only, never a person
        var target = dest == null ? null : Game1.getLocationFromName(dest);
        if (target == null)
        {
            c.Fail(Reasons.NotFound, $"Unknown location \"{asked}\". travel_to_location takes a map or area name only (Farm, Town, Beach, Mountain, Mine, Ginger Island...). " +
                                     "A shop or its owner goes with travel_to_shop; a villager is reached with talk_to_villager or give_gift, not by travel.");
            yield break;
        }
        yield return TravelToResolved(target, note, c);
    }

    /// <summary>
    /// travel_to_shop: walk to a shop, named by the shop or its keeper, and stop at the counter. Accepts a
    /// known shop map or a keeper alias from NpcInfo (Pierre, Clint, Robin, Marnie, Willy, Gus, Sandy); an
    /// arbitrary villager (or a non-shop map) is refused.
    /// </summary>
    internal static IEnumerator TravelToShopSemantic(JsonElement a, CallContext c)
    {
        var asked = S(a, "shop").Trim();
        var (dest, note) = NpcInfo.ResolveShopPlace(asked); // shop maps and keeper aliases only, never a villager
        var target = dest == null ? null : Game1.getLocationFromName(dest);
        if (target == null)
        {
            c.Fail(Reasons.NotFound, $"Unknown shop \"{asked}\". travel_to_shop takes a shop's name or its keeper's (Pierre, Clint, Robin, Marnie, Willy, Gus, Sandy), " +
                                     "or a shop map (SeedShop, Blacksmith, AnimalShop, FishShop, Saloon...). A villager's name is not a shop: " +
                                     "to talk or give a gift use talk_to_villager / give_gift.");
            yield break;
        }
        yield return TravelToResolved(target, note, c);
        if (!c.Ok) yield break;

        // Stop at the counter so the next interact opens the shop: the nearest tile whose action opens a shop.
        var counters = FindActionTiles(Loc, IsShopCounterAction);
        if (counters.Count > 0)
        {
            var me = P.TilePoint;
            var counter = counters.OrderBy(t => Math.Abs(t.X - me.X) + Math.Abs(t.Y - me.Y)).First();
            var walk = new CallContext();
            yield return WalkAdjacent(counter, walk);
            if (walk.Ok) c.Result += " You are at the counter.";
            else c.Result += $" The counter is at ({counter.X},{counter.Y}), but it could not be reached: {walk.Result}";
        }
    }

    /// <summary>A tile action that opens a shop menu: the generic 1.6 "OpenShop id ..." or a named counter action.</summary>
    private static bool IsShopCounterAction(string action)
    {
        var name = action.Split(' ')[0];
        return name.Equals("OpenShop", StringComparison.OrdinalIgnoreCase) || name is
            "SeedShop" or "Saloon" or "Carpenter" or "AnimalShop" or "Blacksmith" or "JojaShop" or
            "HospitalShop" or "AdventureShop" or "ClubShop" or "ClubSeller" or "SandyShop" or "BuyTokens";
    }
}
