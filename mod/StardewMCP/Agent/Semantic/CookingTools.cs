using System;
using System.Collections;
using System.Text.Json;
using StardewValley.Locations;

namespace StardewMCP.Agent;

// Semantic (intent-shaped) Cooking tools (phase 2 of docs/tool-decoupling/README.md): the dish list split from
// the actual cooking. list_dishes is a pure instant observation; cook_dish does one named job and requires a
// recipe plus a reason, so empty arguments can never trigger the write. The legacy "cook" tool in CookingTools.cs
// keeps its list/cook behavior and stays in the MCP catalog; these are the versions the built-in AI catalog
// prefers. The ToolDef registrations live in Agent/SemanticTools.cs; this file only holds the Run bodies.

public static partial class AgentTools
{
    /// <summary>list_dishes: the legacy cook list text as a pure instant read (fridge counts when home).</summary>
    internal static IEnumerator ListDishesSemantic(JsonElement a, CallContext c)
    {
        var home = Loc is FarmHouse here ? CookFridges(here) : HomeFridges();
        var elsewhere = Loc is FarmHouse ? "" : $" You are in {Loc.Name}: cook_dish works inside your farmhouse.";
        c.Result = RecipeList(true, FridgeItems(home)) +
                   (home.Count > 0 ? "\nCounts include the fridge." : "\nCounts include your pack only.") + elsewhere;
        yield break;
    }

    /// <summary>cook_dish: the legacy cook body, but a missing recipe refuses instead of falling back to listing.</summary>
    internal static IEnumerator CookDishSemantic(JsonElement a, CallContext c)
    {
        var want = S(a, "recipe").Trim();
        if (want.Length == 0 || want.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            c.Fail("cook_dish needs a recipe to cook. list_dishes shows the dishes you know; cook_dish(recipe, reason) cooks one.");
            yield break;
        }
        yield return Cook(a, c);
    }
}
