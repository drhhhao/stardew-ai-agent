using System;
using System.Collections.Generic;

namespace StardewMCP.Agent;

/// <summary>
/// Reason codes for tool results: one fixed vocabulary instead of per-tool wording. Keep this list in step with
/// runner/tool_results.py. Reasons stays in this file above CallContext for external source readers.
/// </summary>
public static class Reasons
{
    public const string Ok = "ok";
    public const string Partial = "partial";
    public const string NoChange = "no_change";
    public const string LowEnergy = "low_energy";
    public const string NoPath = "no_path";
    public const string NotHere = "not_here";
    public const string NoItem = "no_item";
    public const string InventoryFull = "inventory_full";
    public const string MenuOpen = "menu_open";
    public const string TooLate = "too_late";
    public const string ShopClosed = "shop_closed";
    public const string WrongSeason = "wrong_season";
    public const string NeedsUpgrade = "needs_upgrade";
    public const string Interrupted = "interrupted";
    public const string Timeout = "timeout";
    public const string NoMoney = "no_money";
    public const string InvalidArgs = "invalid_args";
    public const string NotFound = "not_found";
    public const string NotReady = "not_ready";
    public const string Error = "error";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Ok, Partial, NoChange, LowEnergy, NoPath, NotHere, NoItem, InventoryFull, MenuOpen, TooLate, ShopClosed,
        WrongSeason, NeedsUpgrade, Interrupted, Timeout, NoMoney, InvalidArgs, NotFound, NotReady, Error, Other
    };
}

/// <summary>Reason, changed flag, partial flag and counts of a tool call.</summary>
public partial class CallContext
{
    public string? Reason;
    public bool Partial;
    public Dictionary<string, object>? Data;
    /// <summary>True when state changed, false for an explicit no-op, null when the tool did not classify it.</summary>
    public bool? Changed;

    public void Fail(string reason, string message)
    {
        Fail(message);
        Reason = reason;
    }

    public void NoChange(string message)
    {
        Result = message;
        Reason = Reasons.NoChange;
        Changed = false;
    }

    public void InvalidArgs(string message, params string[] validValues)
    {
        Fail(Reasons.InvalidArgs, message);
        Data = new Dictionary<string, object> { ["validValues"] = validValues };
    }

    /// <summary>Report done/remaining/skipped counts and why a batch stopped early.</summary>
    public void Batch(int total, int done, int skipped, string? stopReason = null, string? skipReason = null)
    {
        int remaining = Math.Max(0, total - done - skipped);
        Data = new Dictionary<string, object> { ["done"] = done, ["remaining"] = remaining, ["skipped"] = skipped };
        Changed ??= OutcomeRules.ChangedFromBatch(total, done);
        if (remaining == 0 && skipped == 0) return;
        Partial = true;
        Reason ??= stopReason ?? skipReason ?? Reasons.Partial;
    }
}

/// <summary>Counts for a batch tool's loop.</summary>
public sealed class BatchTally
{
    public int Done;
    public int Skipped;
    public string? Stop;
    public string? SkipWhy;

    public void Skip(string? why = null)
    {
        Skipped++;
        SkipWhy ??= why;
    }

    public void Apply(CallContext c, int total) => c.Batch(total, Done, Skipped, Stop, SkipWhy);
}

/// <summary>What a finished call reports besides its text.</summary>
public sealed record ToolOutcome(bool Ok, string? Reason, bool Partial, IReadOnlyDictionary<string, object>? Data)
{
    public bool? Changed { get; init; }

    public static ToolOutcome From(CallContext c) =>
        new(c.Ok, c.Reason, c.Partial, c.Data is { Count: > 0 } ? new Dictionary<string, object>(c.Data) : null)
        { Changed = c.Changed };

    public string Code => Reason is { Length: > 0 } r ? r : Partial ? Reasons.Partial : Ok ? Reasons.Ok : Reasons.Other;
}

/// <summary>Builds MCP tool replies through the game-free outcome rules.</summary>
public static class ToolResult
{
    public static string Tag(ToolOutcome o) => OutcomeRules.Tag(o);
    public static Dictionary<string, object> Structured(ToolOutcome o, string? text = null) => OutcomeRules.Structured(o, text);
    public static object McpReply(bool ok, string text, ToolOutcome? outcome) => OutcomeRules.McpReply(ok, text, outcome);
    public static string TaggedText(bool ok, string text, ToolOutcome? outcome) => OutcomeRules.TaggedText(ok, text, outcome);
    public static object McpError(string reason, string text) => OutcomeRules.McpError(reason, text);
}
