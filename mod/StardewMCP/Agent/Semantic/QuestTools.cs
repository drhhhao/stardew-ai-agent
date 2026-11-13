using System.Collections;
using System.Text.Json;

namespace StardewMCP.Agent;

// Semantic (intent-shaped) Quest tools (phase 1 of docs/tool-decoupling/README.md): one observation and
// one action per tool, no mode-switch "action" argument. The legacy "quests" tool in QuestTools.cs keeps
// its list/claim_reward actions and stays in the MCP catalog; these are the versions the built-in AI
// catalog prefers. The ToolDef registrations live in Agent/SemanticTools.cs; this file only holds the
// Run bodies, reusing the legacy implementations (QuestsText / ClaimQuestReward) so behavior cannot
// drift between the two catalogs.

public static partial class AgentTools
{
    /// <summary>list_quests: everything quests tracks as a pure instant read (the legacy quests list text).</summary>
    internal static IEnumerator ListQuestsSemantic(JsonElement a, CallContext c)
    {
        c.Result = QuestsText(a);
        yield break;
    }

    /// <summary>claim_quest_reward: the legacy claim_reward action, unchanged.</summary>
    internal static IEnumerator ClaimQuestRewardSemantic(JsonElement a, CallContext c)
    {
        yield return ClaimQuestReward(a, c);
    }
}
