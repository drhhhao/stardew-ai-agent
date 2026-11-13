using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewModdingAPI;

namespace StardewMCP.Agent;

// The game-free result contract lives in ToolResult.cs and OutcomeRules.cs. This file keeps what needs
// SMAPI: the per-day result-count summary written next to the day's JSONL log.

/// <summary>Result counts of one tool or one player: how often calls failed or were partial, and why.</summary>
public class ResultCounts
{
    /// <summary>Logged results. Instant observations (look, status...) that succeed are not logged, so they are not counted.</summary>
    public int Calls { get; set; }
    public int Failed { get; set; }
    public int Partial { get; set; }
    /// <summary>Reason code -> results, for failed and partial results (and any result a tool gave a reason for).</summary>
    [JsonPropertyName("reasons")]
    public SortedDictionary<string, int> ByReason { get; set; } = new();

    public void Add(bool failed, bool wasPartial, string? reason)
    {
        Calls++;
        if (failed) Failed++;
        if (wasPartial) Partial++;
        var key = reason is { Length: > 0 } ? reason : failed ? Reasons.Other : wasPartial ? Reasons.Partial : null;
        if (key == null || key == Reasons.Ok) return;
        ByReason[key] = ByReason.TryGetValue(key, out var n) ? n + 1 : 1;
    }
}

public class PlayerResultSummary
{
    public ResultCounts Total { get; set; } = new();
    public SortedDictionary<string, ResultCounts> Tools { get; set; } = new();
}

/// <summary>day_XXX_summary.json: failure reasons per tool and per player for one in-game day, built from the day's JSONL log.</summary>
public class DaySummaryData
{
    public int Day { get; set; }
    public string Updated { get; set; } = "";
    public ResultCounts Total { get; set; } = new();
    public SortedDictionary<string, ResultCounts> Tools { get; set; } = new();
    public SortedDictionary<string, PlayerResultSummary> Players { get; set; } = new();
}

/// <summary>
/// Writes day_XXX_summary.json next to the day's log. It is rebuilt from the JSONL file each time, so it survives a game restart
/// and needs no state of its own. Written at most every 30 s while results come in, when the day number changes, and at day end.
/// </summary>
public static class DaySummary
{
    private static readonly object Gate = new();
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static int _day;
    private static DateTime _lastWrite = DateTime.MinValue;

    public static string FileName(int day) => $"day_{day:000}_summary.json";

    /// <summary>Call after a result line was logged for <paramref name="day"/>.</summary>
    public static void Touch(AgentLog log, int day, IMonitor? monitor = null)
    {
        if (day <= 0) return;
        int previous = 0;
        bool due;
        lock (Gate)
        {
            if (_day > 0 && _day != day) previous = _day;
            _day = day;
            due = DateTime.UtcNow - _lastWrite >= Interval;
            if (due) _lastWrite = DateTime.UtcNow;
        }
        if (previous > 0) Write(log, previous, monitor);
        if (due) Write(log, day, monitor);
    }

    public static void Write(AgentLog log, int day, IMonitor? monitor = null)
    {
        try
        {
            var source = log.DayFile(day);
            if (day <= 0 || !File.Exists(source)) return;
            var summary = Build(day, ReadLines(source));
            File.WriteAllText(Path.Combine(log.Dir, FileName(day)), JsonSerializer.Serialize(summary, AgentLog.Json), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            monitor?.Log($"Day summary failed: {ex.Message}", LogLevel.Warn);
        }
    }

    /// <summary>Count the "result" lines of one day's JSONL. Lines that are not valid JSON (a half-written last line) are skipped.</summary>
    public static DaySummaryData Build(int day, IEnumerable<string> jsonlLines)
    {
        var s = new DaySummaryData { Day = day, Updated = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz") };
        foreach (var line in jsonlLines)
        {
            if (line.Length == 0) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var r = doc.RootElement;
                if (r.ValueKind != JsonValueKind.Object || Str(r, "kind") != "result") continue;
                var tool = Str(r, "tool") ?? "?";
                var player = Str(r, "player") ?? "p1";
                bool failed = r.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.False;
                bool wasPartial = r.TryGetProperty("partial", out var pEl) && pEl.ValueKind == JsonValueKind.True;
                var reason = Str(r, "reason");

                s.Total.Add(failed, wasPartial, reason);
                Get(s.Tools, tool).Add(failed, wasPartial, reason);
                if (!s.Players.TryGetValue(player, out var ps)) s.Players[player] = ps = new PlayerResultSummary();
                ps.Total.Add(failed, wasPartial, reason);
                Get(ps.Tools, tool).Add(failed, wasPartial, reason);
            }
            catch (JsonException)
            {
            }
        }
        return s;
    }

    private static ResultCounts Get(SortedDictionary<string, ResultCounts> map, string key)
    {
        if (!map.TryGetValue(key, out var c)) map[key] = c = new ResultCounts();
        return c;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>The log file is appended to by the game while we read it, so share it.</summary>
    private static List<string> ReadLines(string path)
    {
        var lines = new List<string>();
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs, Encoding.UTF8);
        string? line;
        while ((line = sr.ReadLine()) != null) lines.Add(line);
        return lines;
    }
}
