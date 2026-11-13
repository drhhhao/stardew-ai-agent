using System.Collections;
using System.Text.Json;

namespace StardewMCP.Agent;

/// <summary>
/// Semantic geode tools (docs/tool-decoupling/README.md Phase 2): list_geodes is a pure instant observation of the
/// geodes you carry; crack_geodes does the one named job of having Clint open them, with an explicit item, and reuses
/// the shared Clint-cracking loop in Agent/GeodeTools.cs so behavior cannot drift.
/// </summary>
public static partial class AgentTools
{
    /// <summary>list_geodes: the geodes in your pack, without spending anything or moving. Read-only; time stays frozen.</summary>
    private static IEnumerator ListGeodesSemantic(JsonElement a, CallContext c)
    {
        var carried = CarriedGeodes();
        c.Result = carried.Count == 0
            ? "You carry no geodes. Mine them (stones in the mines), fish or dig them up, then bring them to Clint at the Blacksmith."
            : $"Geodes you carry: {Stacks(carried)}. Clint cracks one for {GeodeFee}g at the Blacksmith (crack_geodes with the name).";
        yield break;
    }

    /// <summary>crack_geodes: Clint opens the named geodes at the Blacksmith. item is required; no empty call can spend money.</summary>
    private static IEnumerator CrackGeodesSemantic(JsonElement a, CallContext c)
    {
        var want = S(a, "item").Trim();
        if (want.Length == 0)
        {
            c.Fail("crack_geodes needs item: the geode to open (list_geodes shows what you carry). An empty call never spends money.");
            yield break;
        }
        yield return CrackGeodesAtClint(want, I(a, "quantity", 1), c);
    }
}
