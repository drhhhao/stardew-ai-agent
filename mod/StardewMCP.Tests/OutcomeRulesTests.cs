using System.Collections.Generic;
using System.Text.Json;
using StardewMCP.Agent;
using Xunit;

namespace StardewMCP.Tests;

/// <summary>The result contract rules (OutcomeRules.cs): no_change/invalid_args classification, the changed flag,
/// partial stoppedBecause, and the loop-guard progress rule with an explicit changed.</summary>
public class OutcomeRulesTests
{
    // ------ CallContext.NoChange / InvalidArgs

    [Fact]
    public void NoChange_is_a_success_with_the_no_change_reason_and_changed_false()
    {
        var c = new CallContext();
        c.NoChange("Already paid.");

        Assert.True(c.Ok);
        Assert.Equal("Already paid.", c.Result);
        Assert.Equal(Reasons.NoChange, c.Reason);
        Assert.False(c.Changed);

        var o = ToolOutcome.From(c);
        Assert.True(o.Ok);
        Assert.Equal(Reasons.NoChange, o.Code);
        Assert.False(o.Changed);
    }

    [Fact]
    public void InvalidArgs_fails_with_the_reason_and_lists_valid_values()
    {
        var c = new CallContext();
        c.InvalidArgs("Unknown mode \"x\".", "take", "put", "ok");

        Assert.False(c.Ok);
        Assert.Equal(Reasons.InvalidArgs, c.Reason);
        var outcome = ToolOutcome.From(c);
        var values = Assert.IsType<string[]>(outcome.Data!["validValues"]);
        Assert.Equal(new[] { "take", "put", "ok" }, values);
    }

    [Fact]
    public void Reasons_all_includes_no_change()
    {
        Assert.Contains(Reasons.NoChange, Reasons.All);
        Assert.Contains(Reasons.InvalidArgs, Reasons.All);
    }

    // ------ structured output

    [Fact]
    public void Structured_omits_changed_when_the_tool_did_not_set_it()
    {
        var d = OutcomeRules.Structured(new ToolOutcome(true, null, false, null));
        Assert.False(d.ContainsKey("changed"));
        Assert.False(d.ContainsKey("data"));
        Assert.Equal("ok", d["reason"]);
    }

    [Fact]
    public void Structured_outputs_changed_when_it_has_a_value()
    {
        var noChange = OutcomeRules.Structured(new ToolOutcome(true, Reasons.NoChange, false, null) { Changed = false });
        Assert.Equal(false, noChange["changed"]);
        Assert.Equal(Reasons.NoChange, noChange["reason"]);

        var changed = OutcomeRules.Structured(new ToolOutcome(true, null, false, null) { Changed = true });
        Assert.Equal(true, changed["changed"]);
    }

    [Fact]
    public void Structured_partial_always_carries_stoppedBecause_and_keeps_the_counts()
    {
        var counts = new Dictionary<string, object> { ["done"] = 2, ["remaining"] = 3, ["skipped"] = 1 };
        var withCounts = OutcomeRules.Structured(new ToolOutcome(true, Reasons.LowEnergy, true, counts));
        var data = Assert.IsType<Dictionary<string, object>>(withCounts["data"]);
        Assert.Equal(Reasons.LowEnergy, data["stoppedBecause"]);
        Assert.Equal(2, data["done"]);
        Assert.Equal(3, data["remaining"]);
        Assert.Equal(1, data["skipped"]);

        // A partial result without its own counts still gets a data block with the stop cause.
        var bare = OutcomeRules.Structured(new ToolOutcome(true, null, true, null));
        var bareData = Assert.IsType<Dictionary<string, object>>(bare["data"]);
        Assert.Equal(Reasons.Partial, bareData["stoppedBecause"]);
        Assert.Single(bareData);
    }

    [Fact]
    public void Tag_shows_the_no_change_reason()
    {
        var tag = OutcomeRules.Tag(new ToolOutcome(true, Reasons.NoChange, false, null) { Changed = false });
        Assert.Equal("[no_change]", tag);
    }

    [Fact]
    public void McpReply_includes_the_changed_flag_and_tag()
    {
        var reply = OutcomeRules.McpReply(true, "Already paid.", ToolOutcome.From(NoChangeCtx()));
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(reply));
        var root = doc.RootElement;
        Assert.False(root.GetProperty("isError").GetBoolean());
        var sc = root.GetProperty("structuredContent");
        Assert.False(sc.GetProperty("changed").GetBoolean());
        Assert.Equal(Reasons.NoChange, sc.GetProperty("reason").GetString());
        Assert.StartsWith("[no_change] ", sc.GetProperty("text").GetString());
    }

    // ------ batch changed inference

    [Fact]
    public void Batch_sets_changed_from_its_counts_unless_the_tool_set_it()
    {
        var didSome = new CallContext();
        didSome.Batch(total: 5, done: 2, skipped: 1);
        Assert.True(didSome.Changed);
        Assert.True(didSome.Partial);

        var didNone = new CallContext();
        didNone.Batch(total: 3, done: 0, skipped: 3);
        Assert.False(didNone.Changed);

        var askedNothing = new CallContext();
        askedNothing.Batch(total: 0, done: 0, skipped: 0);
        Assert.Null(askedNothing.Changed);
        Assert.False(askedNothing.Partial);

        var explicitWins = new CallContext { Changed = true };
        explicitWins.Batch(total: 3, done: 0, skipped: 3);
        Assert.True(explicitWins.Changed);
    }

    // ------ loop progress rule

    [Fact]
    public void IsProgress_counts_explicit_changed_and_ignores_successful_actions_on_no_change()
    {
        // Explicit changed=true is progress even for a zero-time call with an unchanged snapshot.
        Assert.True(OutcomeRules.IsProgress(true, instant: true, ok: true, snapshotChanged: false));
        // Explicit changed=false is not progress just for being a successful action.
        Assert.False(OutcomeRules.IsProgress(false, instant: false, ok: true, snapshotChanged: false));
        // ...unless the snapshot moved.
        Assert.True(OutcomeRules.IsProgress(false, instant: false, ok: true, snapshotChanged: true));
        // Null keeps the old rule: a successful non-instant call counts; a successful instant call does not.
        Assert.True(OutcomeRules.IsProgress(null, instant: false, ok: true, snapshotChanged: false));
        Assert.False(OutcomeRules.IsProgress(null, instant: true, ok: true, snapshotChanged: false));
    }

    [Fact]
    public void Detector_treats_explicit_no_change_as_no_progress()
    {
        int day = 1;
        var detector = new LoopDetector(hintAfter: 3, blockAfter: 5, noProgressAfter: 2, () => day);
        var key = LoopDetector.KeyOf("pay", JsonDocument.Parse("{\"who\":\"clint\",\"amount\":200}").RootElement);

        // Two successful no-change calls: the old rule would count a successful action as progress and never hint.
        detector.After(key, instant: false, ok: true, result: "Already paid.", snapshot: "snap", changed: false);
        var notes = detector.After(key, instant: false, ok: true, result: "Already paid.", snapshot: "snap", changed: false);
        Assert.Empty(notes);
        notes = detector.After(key, instant: false, ok: true, result: "Already paid.", snapshot: "snap", changed: false);
        Assert.Single(notes);
        Assert.Equal(GuardKind.NoProgress, notes[0].Kind);
    }

    [Fact]
    public void Detector_counts_explicit_changed_as_progress_even_without_snapshot_movement()
    {
        int day = 1;
        var detector = new LoopDetector(hintAfter: 3, blockAfter: 5, noProgressAfter: 2, () => day);
        var key = LoopDetector.KeyOf("give", JsonDocument.Parse("{\"item\":\"Parsnip\"}").RootElement);

        // A zero-time serialized mutation that changes state resets the no-progress streak.
        detector.After(key, instant: false, ok: true, result: "Given.", snapshot: "snap", changed: false);
        var notes = detector.After(key, instant: false, ok: true, result: "Given.", snapshot: "snap", changed: true);
        Assert.Empty(notes);
        notes = detector.After(key, instant: false, ok: true, result: "Given.", snapshot: "snap", changed: false);
        Assert.Empty(notes); // streak is only 1 after the changed=true reset
    }

    private static CallContext NoChangeCtx()
    {
        var c = new CallContext();
        c.NoChange("Already paid.");
        return c;
    }
}
