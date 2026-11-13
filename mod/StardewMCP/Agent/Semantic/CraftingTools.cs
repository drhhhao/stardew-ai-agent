using System.Collections;
using System.Text.Json;
using StardewValley;

namespace StardewMCP.Agent;

// Semantic (intent-shaped) crafting tools (phase 2 of docs/tool-decoupling/README.md): the read is list_recipes,
// the write is craft_item, and neither has a mode-switch argument. The legacy "craft" tool in CraftingTools.cs keeps
// its list/craft modes and stays in the MCP catalog; these are the versions the built-in AI catalog prefers.
// The ToolDef registrations live in Agent/SemanticTools.cs; this file only holds the Run bodies, reusing the
// legacy implementations (RecipeList / CraftItems) so behavior cannot drift between the two catalogs.

public static partial class AgentTools
{
    /// <summary>list_recipes: the pure instant read of the legacy craft list mode — the crafting recipes you know,
    /// their ingredients and how many you can craft right now. Cooking recipes are not included: list_dishes reads those.</summary>
    internal static IEnumerator ListRecipesSemantic(JsonElement a, CallContext c)
    {
        CraftingRecipe.InitShared();
        c.Result = RecipeList();
        yield break;
    }

    /// <summary>craft_item: the legacy craft(recipe, quantity) action with an explicit recipe target. An empty recipe
    /// is a refusal, never a silent listing or write; list_recipes shows what you can craft.</summary>
    internal static IEnumerator CraftItemSemantic(JsonElement a, CallContext c)
    {
        var want = S(a, "recipe").Trim();
        if (want.Length == 0)
        {
            c.Fail(Reasons.InvalidArgs, "recipe is required: the name of a recipe list_recipes shows. Call list_recipes first.");
            yield break;
        }
        yield return CraftItems(a, c, want);
    }
}
