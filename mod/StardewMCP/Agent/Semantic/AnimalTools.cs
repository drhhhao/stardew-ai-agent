using System;
using System.Collections;
using System.Text.Json;

namespace StardewMCP.Agent;

// Semantic (intent-shaped) animal tools (phase 2 of docs/tool-decoupling/README.md): one observation or one
// action per tool, no mode-switch "action"/"type=list" argument. The legacy "buy_animal" and "animals" tools in
// Agent/AnimalTools.cs keep their old modes and stay in the MCP catalog; these are the versions the built-in AI
// catalog prefers. The ToolDef registrations live in Agent/SemanticTools.cs; this file only holds the Run bodies,
// reusing the shared implementations (AnimalShopList / PurchaseAnimalBody / AnimalOverview / PetAnimals /
// CollectAnimals / FeedAnimals) so behavior cannot drift between the two catalogs. Reads are pure observations;
// care actions only ever affect the animals in the place the farmer is standing in, like the legacy modes.

public static partial class AgentTools
{
    /// <summary>list_animals_for_sale: Marnie's catalogue as a pure instant read (the legacy buy_animal list text).</summary>
    internal static IEnumerator ListAnimalsForSaleSemantic(JsonElement a, CallContext c)
    {
        c.Result = AnimalShopList(AnimalCatalog());
        yield break;
    }

    /// <summary>
    /// purchase_animal: buy exactly one animal of the required type. Unlike the legacy buy_animal there is no
    /// type=list mode: an empty or "list" type is an argument error, never a silent switch to browsing (and never
    /// a write).
    /// </summary>
    internal static IEnumerator PurchaseAnimalSemantic(JsonElement a, CallContext c)
    {
        var want = S(a, "type").Trim();
        if (want.Length == 0 || want.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            c.Fail(Reasons.InvalidArgs, "type is required: which animal to buy, as list_animals_for_sale shows it (e.g. Chicken, Cow, Duck, Goat). To browse instead, call list_animals_for_sale.");
            yield break;
        }
        yield return PurchaseAnimalBody(a, c, want);
    }

    /// <summary>list_animals: every farm animal and the silo hay as a pure instant read (the legacy animals list text).</summary>
    internal static IEnumerator ListAnimalsSemantic(JsonElement a, CallContext c)
    {
        c.Result = AnimalOverview();
        yield break;
    }

    /// <summary>pet_animals: the legacy pet action (all un-petted animals here, or only the one named), unchanged.</summary>
    internal static IEnumerator PetAnimalsSemantic(JsonElement a, CallContext c)
    {
        yield return PetAnimals(S(a, "name").Trim(), c);
    }

    /// <summary>collect_animal_products: the legacy collect action (floor products plus milk/wool with the right tool), unchanged.</summary>
    internal static IEnumerator CollectAnimalProductsSemantic(JsonElement a, CallContext c)
    {
        yield return CollectAnimals(S(a, "name").Trim(), c);
    }

    /// <summary>feed_animals: the legacy feed action (fill this coop or barn's troughs from the silo), unchanged.</summary>
    internal static IEnumerator FeedAnimalsSemantic(JsonElement a, CallContext c)
    {
        yield return FeedAnimals(c);
    }
}
