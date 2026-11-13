using System;
using System.Collections;
using System.Text.Json;

namespace StardewMCP.Agent;

// Moved here unchanged from AgentTools.cs and AgentRuntime.cs: they use no game types, so the unit
// tests (mod/StardewMCP.Tests) compile this file to build fake tools for the MCP contract tests.

/// <summary>State shared by one running tool call. Reason, Changed, Partial and Data are in ToolResult.cs.</summary>
public partial class CallContext
{
    public string Result = "";
    public bool Ok = true;
    /// <summary>The call takes zero game time (<see cref="ToolDef.Instant"/> / <see cref="ToolDef.InstantFor"/>, decided when it was
    /// queued). Which lane it runs in is a separate decision (<see cref="ToolCatalog.UseObservationLane"/>).</summary>
    public bool Instant;

    public void Fail(string message)
    {
        Ok = false;
        Result = message;
    }
}

public delegate IEnumerator ToolRunner(JsonElement args, CallContext ctx);

public class ToolDef
{
    public string Name = null!;
    public string Description = null!;
    public object Schema = null!;
    public ToolRunner Run = null!;
    /// <summary>Zero game time: the call does not unfreeze the world while it runs. Says nothing about read-only or
    /// about which lane it runs in — that is <see cref="Mutates"/>, <see cref="Serialized"/> and
    /// <see cref="ToolCatalog.UseObservationLane"/>.</summary>
    public bool Instant;
    /// <summary>Instant for these arguments only, e.g. menu() reads while menu(action) acts.</summary>
    public Func<JsonElement, bool>? InstantFor;
    /// <summary>Any side effect (world, inventory, money, other players, files...). Mutating tools are never readOnly,
    /// even at zero game time: pay, give, name, say, emote, journal, think.</summary>
    public bool Mutates;
    /// <summary>Zero game time like Instant, but still runs in the action lane, serialized with the seat's other
    /// actions (pay, give, name): the world stays frozen while one runs, and it never overtakes an action queued before it.</summary>
    public bool Serialized;
    /// <summary>A pure observation: zero game time and no side effects (<see cref="Instant"/>, no InstantFor, no
    /// <see cref="Mutates"/>). Pure observations skip the duplicated status footer on their results.</summary>
    public bool ReadOnly => Instant && InstantFor == null && !Mutates;
    public bool NeedsWorld = true;
    public bool AppendStatus = true;
    /// <summary>Chat waiting for the player are appended to this tool's result (false for debug ops).</summary>
    public bool Mail = true;
    /// <summary>A superseded tool kept callable for existing MCP/HTTP clients. Legacy tools stay in <see cref="AgentTools.All()"/>
    /// but are hidden from the built-in AI (<see cref="AgentTools.Preferred()"/>) and flagged in /api/contract.</summary>
    public bool Legacy;
}
