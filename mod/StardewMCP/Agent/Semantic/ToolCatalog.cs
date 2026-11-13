using System.Collections.Generic;
using System.Linq;

namespace StardewMCP.Agent;

/// <summary>
/// Catalog-level rules for the tool decoupling (docs/tool-decoupling/README.md): which tools the built-in AI
/// sees. Game-free so the unit tests (mod/StardewMCP.Tests) can exercise it without the game assemblies.
/// </summary>
public static class ToolCatalog
{
    /// <summary>
    /// The mode-switch tools superseded by the semantic tools in Agent/SemanticTools.cs. Phase 1: quests, museum,
    /// special_orders. Phase 2: craft, cook, crack_geode, buy_animal, animals, build. Phase 3: chest, talk_to.
    /// Phase 4A: plant (implicit whole-map scope), close_menu (accepts a letter's quest and claims a grab menu's
    /// items as a side effect of closing). Phase 4B: go_to_bed (sleep), travel_to (travel_to_location for maps,
    /// travel_to_shop for shop keepers), wear (unified into equip).
    /// AgentTools.All() applies the mark centrally as a safety net; a tool definition can also set
    /// <see cref="ToolDef.Legacy"/> directly. menu and interact stay unmarked: they remain the preferred catalog's
    /// fallback for unknown menus and unmatched tiles. Unrelated tools in the same files remain unchanged.
    /// </summary>
    public static readonly string[] SupersededBySemantic =
    {
        "quests", "museum", "special_orders",
        "craft", "cook", "crack_geode", "buy_animal", "animals", "build",
        "chest", "talk_to",
        "plant", "close_menu",
        "go_to_bed", "travel_to", "wear"
    };

    /// <summary>The built-in AI's catalog: everything <see cref="AgentTools.All()"/> serves except Legacy tools.</summary>
    public static List<ToolDef> Preferred(IEnumerable<ToolDef> tools) => tools.Where(t => !t.Legacy).ToList();

    /// <summary>
    /// Which lane a queued call runs in: true is the observation lane (answers even during the seat's own action,
    /// the world stays frozen). Only zero-time, non-serialized, non-debug calls observe: <paramref name="instant"/>
    /// is the queued call's zero-time flag (<see cref="ToolDef.Instant"/>/InstantFor), and a zero-time tool marked
    /// <see cref="ToolDef.Serialized"/> (pay, give, name) still takes the action lane so it stays ordered with the
    /// seat's actions. Game-free so the unit tests can exercise it without the game assemblies.
    /// </summary>
    public static bool UseObservationLane(ToolDef tool, bool instant, bool debug) =>
        !debug && instant && !tool.Serialized;

    /// <summary>
    /// Both names of the end-of-day sleep tool: the legacy go_to_bed and its semantic successor sleep. Runtime rules
    /// that treat a sleeper specially (the longer timeout, the early answer, wake-up, sleep statistics, session
    /// breaks, the stalled-night check) test this instead of comparing against one tool name.
    /// </summary>
    public static bool IsSleep(string? name) => name is "go_to_bed" or "sleep";
}
