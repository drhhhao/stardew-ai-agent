using System.Collections.Generic;

namespace StardewMCP.Agent;

/// <summary>Pure rules for tool result text, structured content, loop progress and batch change inference.</summary>
public static class OutcomeRules
{
    public static string Tag(ToolOutcome o)
    {
        var parts = new List<string>(2);
        if (o.Partial) parts.Add(Reasons.Partial);
        if (o.Reason is { Length: > 0 } r && r != Reasons.Ok && r != Reasons.Partial) parts.Add(r);
        return parts.Count == 0 ? "" : "[" + string.Join(" · ", parts) + "]";
    }

    /// <summary>Build structuredContent, preserving counts and adding stoppedBecause to every partial result.</summary>
    public static Dictionary<string, object> Structured(ToolOutcome o, string? text = null)
    {
        var d = new Dictionary<string, object> { ["ok"] = o.Ok, ["reason"] = o.Code };
        if (text != null) d["text"] = text;
        if (o.Partial) d["partial"] = true;
        if (o.Changed.HasValue) d["changed"] = o.Changed.Value;
        Dictionary<string, object>? data = o.Data is { Count: > 0 }
            ? new Dictionary<string, object>(o.Data)
            : o.Partial ? new Dictionary<string, object>() : null;
        if (data != null)
        {
            if (o.Partial) data["stoppedBecause"] = o.Code;
            d["data"] = data;
        }
        return d;
    }

    public static object McpReply(bool ok, string text, ToolOutcome? outcome)
    {
        var o = outcome ?? new ToolOutcome(ok, null, false, null);
        var fullText = TaggedText(ok, text, outcome);
        return new
        {
            content = new[] { new { type = "text", text = fullText } },
            isError = !o.Ok,
            structuredContent = Structured(o, fullText)
        };
    }

    public static string TaggedText(bool ok, string text, ToolOutcome? outcome)
    {
        var tag = Tag(outcome ?? new ToolOutcome(ok, null, false, null));
        return tag.Length > 0 ? tag + " " + text : text;
    }

    public static object McpError(string reason, string text) =>
        McpReply(false, text, new ToolOutcome(false, reason, false, null));

    /// <summary>Explicit changed wins over the old successful-action heuristic; a snapshot change always counts.</summary>
    public static bool IsProgress(bool? changed, bool instant, bool ok, bool snapshotChanged) =>
        changed.HasValue ? changed.Value || snapshotChanged : (!instant && ok) || snapshotChanged;

    public static bool? ChangedFromBatch(int total, int done) => total <= 0 ? null : done > 0;
}
