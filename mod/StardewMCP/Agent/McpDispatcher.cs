using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace StardewMCP.Agent;

/// <summary>One AI seat as the MCP layer sees it. <c>McpSeat</c> adapts a split-screen seat; the unit tests use a fake.</summary>
public interface IMcpSeat
{
    string Id { get; }

    /// <summary>The seat's screen exists and is in the game loop.</summary>
    bool Active { get; }

    /// <summary>Any MCP traffic means an AI is attached to this seat: from now on its thinking freezes the world.</summary>
    void MarkContact();

    /// <summary>Run a tool for this seat; instant observations may answer during the seat's own action.</summary>
    Task<(bool ok, string text)> CallTool(ToolDef tool, JsonElement args);

    /// <summary>Run a tool and format the MCP result object (including structuredContent when supported).</summary>
    async Task<object> CallToolReply(ToolDef tool, JsonElement args)
    {
        var (ok, text) = await CallTool(tool, args);
        return new { content = new[] { new { type = "text", text } }, isError = !ok };
    }

    /// <summary>Format an MCP tool error when a call cannot reach a tool.</summary>
    object ErrorReply(string reason, string text) =>
        new { content = new[] { new { type = "text", text } }, isError = true };
}

/// <summary>What to answer one POST with: HTTP status, JSON body (null = empty body) and the Mcp-Session-Id header (null = none).</summary>
public readonly record struct McpReply(int Status, object? Body, string? SessionId);

/// <summary>
/// JSON-RPC handling of the MCP endpoint (initialize, ping, tools/list, tools/call), moved out of
/// AgentHttp so it can run without HTTP, the game or an <c>AgentRuntime</c>.
/// </summary>
public class McpDispatcher
{
    public const string ProtocolFallback = "2025-06-18";

    private readonly IReadOnlyDictionary<string, ToolDef> _tools;
    private readonly Func<bool> _splitScreen;
    private readonly string _serverVersion;
    private readonly Func<bool> _guideOn;
    private readonly Func<bool> _selfDefenseOn;

    /// <summary>Appended to the initialize instructions when the strategy guide is on.</summary>
    public const string GuideInstructions =
        " Call guide() for the strategy roadmap and guide(topic) before big decisions; crops, item_info, fish_info, machines, bundles and progress give exact game data.";

    /// <summary>Appended when <see cref="ModConfig.SelfDefense"/> is on, so an external AI expects the swings it did not ask for.</summary>
    public const string SelfDefenseInstructions =
        " Your farmer also defends itself between actions: with no action running, a monster that comes within melee reach is hit with your best weapon, and you eat health food when hurt. It never chases and starts no action (only at 25% health with no food does it step a few tiles away); a tool result explains it under 'While no action was running (you were thinking)'.";

    /// <summary>Appended always: the monsters tool is always registered.</summary>
    public const string MonsterInstructions =
        " Use monsters() on a new mine floor or before a crowded fight: it lists every living monster on the map with HP, damage, distance and a one-line tactic, and shows the held weapon's damage.";

    /// <summary>Appended always: routine selling does not need a shop trip.</summary>
    public const string SellInstructions =
        " Sell routine produce and fish with ship at the farm (same base price, paid overnight); walk to a shop to sell only when you need the gold today or the buyer pays more. If the shop is closed, do not wait in town: ship and buy tomorrow.";

    /// <summary>Appended always: the farmhouse TV is a free daily information source.</summary>
    public const string TvInstructions =
        " Watch the farmhouse TV in the morning with watch_tv: weather is tomorrow's forecast, fortune is today's luck, Livin' Off The Land (Mon/Thu) gives tips, Queen of Sauce (Sun new, Wed rerun) teaches recipes, and Fishing (after Pam's letter) lists this season's fish.";

    /// <summary>Appended always: keeping the pack light is a tool call, not a walk to a shop.</summary>
    public const string ChestInstructions =
        " When the pack fills, use chest(action=stash) at home or on the farm: it finds or crafts a Chest (50 Wood) and stores everything except tools, seeds and cooked dishes; give item/items to store only those.";

    public McpDispatcher(IReadOnlyDictionary<string, ToolDef> tools, bool splitScreen) : this(tools, () => splitScreen) { }

    /// <summary>
    /// <paramref name="splitScreen"/> is read on each initialize, so a config change applies to the next session.
    /// <paramref name="serverVersion"/> is the mod's version (manifest.json), reported as serverInfo.version.
    /// <paramref name="guideOn"/> (read on each initialize) adds the strategy guide sentence to the instructions.
    /// <paramref name="selfDefenseOn"/> adds the self-defense sentence.
    /// </summary>
    public McpDispatcher(IReadOnlyDictionary<string, ToolDef> tools, Func<bool> splitScreen, string serverVersion = "0.0.0",
        Func<bool>? guideOn = null, Func<bool>? selfDefenseOn = null)
    {
        _tools = tools;
        _splitScreen = splitScreen;
        _serverVersion = serverVersion;
        _guideOn = guideOn ?? (() => false);
        _selfDefenseOn = selfDefenseOn ?? (() => false);
    }

    /// <summary>
    /// One POST body: a single message or a batch (array). Messages without an id are notifications
    /// and get no answer; when nothing is left to answer the reply is 202 with no body.
    /// </summary>
    public async Task<McpReply> Handle(JsonElement root, IMcpSeat seat)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            var outs = new List<object>();
            foreach (var m in root.EnumerateArray())
            {
                var o = await Dispatch(m, seat);
                if (o != null) outs.Add(o);
            }
            return outs.Count == 0 ? new McpReply(202, null, null) : new McpReply(200, outs, null);
        }

        var reply = await Dispatch(root, seat);
        return reply == null ? new McpReply(202, null, null) : new McpReply(200, reply, "stardew-" + seat.Id);
    }

    /// <summary>One JSON-RPC message; null for a notification (no id).</summary>
    public async Task<object?> Dispatch(JsonElement msg, IMcpSeat seat)
    {
        // Any MCP traffic means an AI is attached to this seat: from now on its thinking freezes the world.
        seat.MarkContact();
        var method = msg.TryGetProperty("method", out var m) ? m.GetString() ?? "" : "";
        if (!msg.TryGetProperty("id", out var idEl)) return null; // notification
        object id = idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt64() : (object)(idEl.GetString() ?? "");
        var p = msg.TryGetProperty("params", out var pe) ? pe : default;

        switch (method)
        {
            case "initialize":
                var ver = p.ValueKind == JsonValueKind.Object && p.TryGetProperty("protocolVersion", out var pv) ? pv.GetString() : ProtocolFallback;
                return Ok(id, new
                {
                    protocolVersion = ver ?? ProtocolFallback,
                    capabilities = new { tools = new { listChanged = false } },
                    serverInfo = new { name = "stardew", version = _serverVersion },
                    instructions = (_splitScreen()
                        ? "You live on a shared Stardew Valley farm with other newcomers. The whole world is frozen while you or another player is thinking, and only moves while actions run."
                        : "You are playing Stardew Valley. Game time is frozen while you think and only moves while an action runs.")
                        + MonsterInstructions
                        + SellInstructions
                        + TvInstructions
                        + ChestInstructions
                        + (_selfDefenseOn() ? SelfDefenseInstructions : "")
                        + (_guideOn() ? GuideInstructions : "")
                });
            case "ping":
                return Ok(id, new { });
            case "tools/list":
                return Ok(id, new
                {
                    tools = _tools.Values.Select(t => new { name = t.Name, description = t.Description, inputSchema = t.Schema })
                });
            case "tools/call":
                if (p.ValueKind != JsonValueKind.Object)
                    return Ok(id, seat.ErrorReply(Reasons.InvalidArgs, "tools/call needs a params object with a string \"name\" and an optional \"arguments\" object."));
                if (!p.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String)
                    return Ok(id, seat.ErrorReply(Reasons.InvalidArgs, "tools/call needs a string \"name\" in params."));
                var name = nameEl.GetString() ?? "";
                if (p.TryGetProperty("arguments", out var a) && a.ValueKind != JsonValueKind.Object)
                    return Ok(id, seat.ErrorReply(Reasons.InvalidArgs, $"tools/call arguments for {name} must be an object."));
                var args = p.TryGetProperty("arguments", out a) ? a.Clone() : JsonDocument.Parse("{}").RootElement;
                if (!_tools.TryGetValue(name, out var tool))
                    return Ok(id, seat.ErrorReply("not_found", $"Unknown tool {name}"));
                if (!seat.Active)
                    return Ok(id, seat.ErrorReply("not_ready", "Your farmer is not in the game yet (title screen, loading or still joining). Wait and try again."));
                return Ok(id, await seat.CallToolReply(tool, args));
            default:
                return new { jsonrpc = "2.0", id, error = new { code = -32601, message = $"Method not found: {method}" } };
        }
    }

    private static object Ok(object id, object result) => new { jsonrpc = "2.0", id, result };
}
