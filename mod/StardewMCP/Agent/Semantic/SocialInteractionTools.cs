using System.Collections;
using System.Text.Json;

namespace StardewMCP.Agent;

// Semantic social tools (phase 3 of docs/tool-decoupling/README.md): the legacy mode-switch talk_to
// (talk, or talk + gift via gift_item) becomes two single-intent tools. talk_to_villager talks and can
// never carry a gift; give_gift requires the item and always reports the villager's reaction. Both reuse
// NpcTools.TalkToVillagerCore — the same find-NPC / approach / game-native gift path as the legacy tool —
// with the gift passed as a plain string, so no call has to synthesize a gift_item argument object and the
// two intents cannot drift apart. The ToolDef registrations live in Agent/SemanticTools.cs; this file only
// holds the Run bodies.

public static partial class AgentTools
{
    /// <summary>talk_to_villager: the legacy walk-up-and-talk with the gift lane closed. The gift argument is
    /// always null here, so a talk never looks up, offers or reports a gift — talking to the villager is the
    /// whole intent. go stays available (travel to them when they are on another map).</summary>
    internal static IEnumerator TalkToVillagerSemantic(JsonElement a, CallContext c) =>
        TalkToVillagerCore(a, c, gift: null);

    /// <summary>give_gift: the same walk-up and the game's own gift action, with a required item. An empty
    /// item is a refusal, never a silent talk; the item is verified in the pack before leaving, and the core
    /// reports whether the gift was accepted and the villager's taste reaction.</summary>
    internal static IEnumerator GiveGiftSemantic(JsonElement a, CallContext c)
    {
        var item = S(a, "item").Trim();
        if (item.Length == 0)
        {
            c.Fail(Reasons.InvalidArgs, "item is required: the name of the gift you carry, e.g. Parsnip. villagers(name) shows their tastes and what they would make of it.");
            yield break;
        }
        yield return TalkToVillagerCore(a, c, item);
    }
}
