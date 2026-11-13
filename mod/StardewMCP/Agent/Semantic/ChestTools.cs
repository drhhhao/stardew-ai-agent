using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace StardewMCP.Agent;

// Semantic (intent-shaped) container tools (phase 3 of docs/tool-decoupling/README.md): one observation or one
// action per tool, no mode-switch "action" argument. The legacy "chest" tool in Agent/ChestTools.cs keeps its
// list/store/take/stash/create/all modes and stays in the MCP catalog; these are the versions the built-in AI
// catalog prefers. The ToolDef registrations live in Agent/SemanticTools.cs; this file only holds the Run bodies,
// reusing the shared implementations (ChestListText / ChestStoreTake / ChestLedger.Report) so behavior cannot
// drift between the two catalogs. The reads are pure instant observations: they never walk and never open or
// close a container. The transfers are serialized actions: with x,y they open the container the way a player
// would (and close it again); without x,y they use the chest menu that is already open.

public static partial class AgentTools
{
    /// <summary>
    /// list_chest: read the chest or fridge whose menu is open right now, without walking, opening or closing
    /// anything. item/items/quality narrow the listing. The menu stays open; dismiss_menu when you are done with it.
    /// </summary>
    internal static IEnumerator ListChestSemantic(JsonElement a, CallContext c)
    {
        if (Has(a, "x") || Has(a, "y"))
        {
            c.Fail("list_chest only reads the chest that is already open: leave out x,y (open it with open_container first). store_items and take_items open the chest at x,y themselves.");
            yield break;
        }
        if (Game1.activeClickableMenu is not ItemGrabMenu menu || menu.context is not Chest)
        {
            c.Fail("No chest or fridge menu is open. Use open_container first, or use list_containers to read every chest and fridge on the farm without walking.");
            yield break;
        }
        var problems = new List<string>();
        var names = StrList(a, "items");
        var single = S(a, "item").Trim();
        if (single.Length > 0) names.Add(single);
        names.RemoveAll(IsAll); // "everything" is the default view, not a filter
        int? quality = null;
        var qText = S(a, "quality").Trim();
        if (qText.Length > 0)
        {
            quality = QualityValue(qText);
            if (quality == null)
            {
                c.Fail($"Unknown quality \"{qText}\". Use normal, silver, gold or iridium.");
                yield break;
            }
        }
        c.Result = ChestListText(menu, names, quality, "", problems);
    }

    /// <summary>list_containers: every chest and fridge on the farm (and in its buildings) with owner and contents, plus the
    /// recent changes, as a pure instant read (the legacy chest action=all text). No walking; game time stays frozen.</summary>
    internal static IEnumerator ListContainersSemantic(JsonElement a, CallContext c)
    {
        c.Result = ChestLedger.Report(Rt.Log.Player);
        yield break;
    }

    /// <summary>
    /// store_items: move items from your pack into a chest or the fridge. What moves is named explicitly: item/items,
    /// or "everything" (which keeps tools, seeds and cooked dishes in your pack, like the old stash). quality moves only
    /// that quality; quantity caps how many of each named item (default all). With x,y you walk to the chest, it opens,
    /// the items move and it closes again; with no x,y the chest menu already open is used. What did not fit is listed,
    /// never silently dropped.
    /// </summary>
    internal static IEnumerator StoreItemsSemantic(JsonElement a, CallContext c)
    {
        if (!TryParseChestMoves(a, c, stashDefaultsToAll: false, requireTargets: true, out var mv))
            yield break;
        yield return ChestStoreTake(a, c, "store", mv);
    }

    /// <summary>
    /// take_items: move items from a chest or the fridge into your pack. What moves is named explicitly: item/items,
    /// or "everything". quality takes only that quality; quantity caps how many of each named item (default all). With x,y
    /// you walk to the chest, it opens, the items move and it closes again; with no x,y the chest menu already open is
    /// used. What did not fit in your pack is listed, never silently dropped.
    /// </summary>
    internal static IEnumerator TakeItemsSemantic(JsonElement a, CallContext c)
    {
        if (!TryParseChestMoves(a, c, stashDefaultsToAll: false, requireTargets: true, out var mv))
            yield break;
        yield return ChestStoreTake(a, c, "take", mv);
    }
}
