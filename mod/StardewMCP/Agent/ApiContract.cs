using System.Collections.Generic;
using System.Linq;

namespace StardewMCP.Agent;

/// <summary>
/// What programs outside the game can rely on: <c>GET /api/contract</c>. Clients read <see cref="ApiVersion"/> (also in
/// <c>/api/state</c>) instead of guessing from missing fields, and take the tool names, reason codes and timing numbers from
/// here instead of copying them from the C# source. Game-free, so the unit tests build the same object.
/// </summary>
public static class ApiContract
{
    /// <summary>
    /// Raised when an endpoint, a result field or a timing rule changes in a way a client has to know about. Adding a tool
    /// or a field does not raise it; renaming or removing one is not allowed anyway (CONTRIBUTING.md, "Don't break").
    /// 1: first version with /api/contract (2026-11).
    /// 2: ToolEntry gains readOnly/zeroTime/serialized, exposing the execution lanes separately instead of overloading
    ///    "instant"; the result contract adds the changed flag, the no_change reason and data.validValues (2026-11).
    /// </summary>
    public const int ApiVersion = 2;

    /// <summary>Real seconds a non-instant action may run before it fails with <c>timeout</c> (go_to_bed has its own limit).</summary>
    public const int ActionTimeoutSeconds = 6 * 60;

    /// <summary>Timing a client has to fit its own timeouts to.</summary>
    public sealed record Timing(int MaxHeldSeconds, int ThinkTimeoutSeconds, int ActionTimeoutSeconds);

    /// <summary>
    /// One tool's execution lanes. <see cref="Instant"/> and <see cref="InstantFor"/> are kept for clients on v1; the lane
    /// triple is the v2 view: <see cref="ReadOnly"/> = never writes game state, <see cref="ZeroTime"/> = never advances game
    /// time (zero-time state mutations such as pay/give/name are zeroTime but not readOnly, and run serialized),
    /// <see cref="Serialized"/> = always runs in the serialized action queue. A tool whose lane depends on its arguments
    /// (InstantFor, e.g. menu() reads while menu(action) acts) has all three false; check InstantFor for that.
    /// </summary>
    public sealed record ToolEntry(string Name, bool Instant, bool InstantFor, bool Legacy, bool ReadOnly, bool ZeroTime, bool Serialized);

    public sealed record Contract(int ApiVersion, string ModVersion, IReadOnlyList<ToolEntry> Tools, IReadOnlyList<string> Reasons, Timing Timing);

    /// <summary>The contract for these tools (sorted by name), reason codes and timing.</summary>
    public static Contract Build(string modVersion, IEnumerable<ToolDef> tools, IEnumerable<string> reasons, Timing timing) =>
        new(ApiVersion, modVersion,
            tools.OrderBy(t => t.Name, System.StringComparer.Ordinal)
                 .Select(t => new ToolEntry(t.Name, t.Instant, t.InstantFor != null, t.Legacy,
                    ReadOnly: t.ReadOnly,
                    ZeroTime: t.Instant,
                    Serialized: t.Serialized || (!t.Instant && t.InstantFor is null)))
                 .ToList(),
            reasons.ToList(),
            timing);
}
