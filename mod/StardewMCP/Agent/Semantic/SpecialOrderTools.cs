using System.Collections;
using System.Text.Json;

namespace StardewMCP.Agent;

/// <summary>
/// Semantic implementations of the Special Orders tools (registered by SemanticToolDefs in
/// Agent/SemanticTools.cs). Each does one named job and delegates to the shared board logic in SpecialOrderTools.cs:
/// list_special_orders and accept_special_order walk to the board (action lane, reason required),
/// active_special_orders is a pure instant observation.
/// </summary>
public static partial class AgentTools
{
    /// <summary>list_special_orders: walks to the board and reports the orders posted on it.</summary>
    private static IEnumerator ListSpecialOrdersSemantic(JsonElement a, CallContext c)
    {
        yield return SoListBoard(c);
    }

    /// <summary>active_special_orders: the accepted orders, objectives, progress, reward and days left. Read-only.</summary>
    private static IEnumerator ActiveSpecialOrdersSemantic(JsonElement a, CallContext c)
    {
        c.Result = ActiveSpecialOrdersText();
        yield break;
    }

    /// <summary>accept_special_order: takes the order at the given index from the board.</summary>
    private static IEnumerator AcceptSpecialOrderSemantic(JsonElement a, CallContext c)
    {
        int index = I(a, "index", -1);
        if (index < 0) { c.Fail("accept_special_order needs the order's index (0 = left, 1 = right); see list_special_orders."); yield break; }
        yield return SoAcceptBoard(index, c);
    }
}
