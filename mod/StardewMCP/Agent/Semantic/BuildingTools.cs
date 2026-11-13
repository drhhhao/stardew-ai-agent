using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewValley;

namespace StardewMCP.Agent;

/// <summary>
/// Semantic implementations of the building tools (registered by SemanticToolDefs in Agent/SemanticTools.cs,
/// docs/tool-decoupling/README.md Phase 2): list_buildings and suggest_building_site are pure instant observations,
/// construct_building and upgrade_building are single-intent actions with a required reason. Both actions decide their
/// lane statically (never Instant, no KnownUpgradeNames cache): construct requires x,y and refuses upgrade names,
/// upgrade refuses new-building names, takes optional x,y, and reuses the legacy Robin gate, cost checks and the
/// game-free UpgradeRules safety choice. The bodies delegate to the shared helpers in Agent/BuildingTools.cs so the
/// legacy build tool cannot drift apart.
/// </summary>
public static partial class AgentTools
{
    /// <summary>list_buildings: Robin's catalog — new buildings and upgrades with costs, affordability and takers. Read-only.</summary>
    private static IEnumerator ListBuildingsSemantic(JsonElement a, CallContext c)
    {
        var catalog = RobinCatalog();
        if (catalog.Count == 0) { c.Fail("Could not read Robin's building list from the game data (Game1.buildingData has no Robin buildings)."); yield break; }
        c.Result = BuildList(catalog, BuildHintsSemantic);
    }

    /// <summary>suggest_building_site: free Farm spots for a NEW building only; refuses upgrades and orders nothing.</summary>
    private static IEnumerator SuggestBuildingSiteSemantic(JsonElement a, CallContext c)
    {
        var want = S(a, "building").Trim();
        if (want.Length == 0) { c.Fail("suggest_building_site needs a building name; list_buildings shows what Robin builds (pass its exact name)."); yield break; }
        var spec = FindRobinSpec(want, c, out var catalog);
        if (spec == null) yield break;
        if (spec.IsUpgrade)
        {
            var from = ShownName(catalog)(spec.UpgradeFrom!);
            c.Fail($"A {spec.Name} is not a new building: it is Robin's upgrade of an existing {from} and keeps that building's place, so there is no site to pick. Upgrades: upgrade_building; new buildings: list_buildings.");
            yield break;
        }
        c.Result = BuildSpots(spec);
    }

    /// <summary>construct_building: pays for and starts a NEW building at the required top-left tile; refuses upgrade names.</summary>
    private static IEnumerator ConstructBuildingSemantic(JsonElement a, CallContext c)
    {
        var want = S(a, "building").Trim();
        if (want.Length == 0) { c.Fail("construct_building needs the building to construct; list_buildings shows what Robin builds (pass its exact name). Nothing was ordered."); yield break; }
        if (!(Has(a, "x") && Has(a, "y")))
        {
            c.Fail("construct_building needs x and y: the top-left tile of the footprint on the Farm. Nothing was ordered. suggest_building_site(building=...) lists free spots.");
            yield break;
        }
        var spec = FindRobinSpec(want, c, out var catalog);
        if (spec == null) yield break;
        if (spec.IsUpgrade)
        {
            var from = ShownName(catalog)(spec.UpgradeFrom!);
            c.Fail($"A {spec.Name} is an upgrade of an existing {from}, not a new building. Nothing was ordered. Use upgrade_building(building=\"{spec.Name}\"); new buildings: list_buildings.");
            yield break;
        }
        yield return ConstructBuildingAt(spec, I(a, "x", -1), I(a, "y", -1), c,
            $"Nothing was charged. suggest_building_site(building=\"{spec.Name}\") suggests free spots.");
    }

    /// <summary>upgrade_building: orders Robin's upgrade of an existing building; refuses new-building names; x,y optional.</summary>
    private static IEnumerator UpgradeBuildingSemantic(JsonElement a, CallContext c)
    {
        var want = S(a, "building").Trim();
        if (want.Length == 0) { c.Fail("upgrade_building needs the upgrade's exact name as list_buildings shows it (Big Coop, Deluxe Barn...). Nothing was ordered."); yield break; }
        var spec = FindRobinSpec(want, c, out var catalog);
        if (spec == null) yield break;
        var shown = ShownName(catalog);
        if (!spec.IsUpgrade)
        {
            c.Fail($"A {spec.Name} is a new building, not an upgrade of one you have. Nothing was ordered. Build a new one with construct_building(building=\"{spec.Name}\", x, y) (suggest_building_site finds free spots), or pick an upgrade from list_buildings.");
            yield break;
        }

        var farm = Game1.getFarm();
        var choice = PickUpgradeTarget(spec, a, catalog, farm);
        var cost = $"A {spec.Name} costs {CostText(spec.Cost, spec.Materials)} and takes {Math.Max(spec.Days, 1)} day(s).";
        if (choice.Decision == UpgradeDecision.Refuse) { c.Fail(choice.Message); yield break; }
        if (choice.Decision == UpgradeDecision.NeedTile) { c.Result = $"{cost}\n{choice.Message}"; yield break; }
        var target = farm.getBuildingAt(new Vector2(choice.Target!.X, choice.Target.Y));
        if (target == null) { c.Fail("The building to upgrade was not found on the Farm. Nothing was ordered."); yield break; }
        yield return UpgradeBuildingOrder(farm, target, spec, c, shown);
    }
}
