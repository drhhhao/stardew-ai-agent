using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace StardewMCP.Agent;

/// <summary>
/// guide(topic): the strategy guide in assets/guide/*.md next to the mod, only when <see cref="ModConfig.GuideKnowledge"/>
/// is on (the tool list is built at startup, so turning it on needs a restart). Reading and topic matching are in
/// <see cref="GuideBook"/> / <see cref="GuideTopics"/>. Instant, no game time.
/// </summary>
public static class GuideTools
{
    private static GuideBook? _book;

    public static List<ToolDef> All(ModConfig config) => !config.GuideKnowledge ? new() : new()
    {
        new()
        {
            Name = "guide",
            Description = "The strategy guide: what to do and when, what is worth it, what to keep and what to avoid. Without a topic it gives the year roadmap and the list of topics; " +
                          "guide(topic) gives one topic (spring, summer, fall, winter, year2, festivals, money, community_center, mines, fishing, relationships, progression, animals). " +
                          "Read the topic before a big choice. Exact numbers come from crops, item_info, fish_info, machines, bundles and progress. Costs no game time.",
            Schema = AgentTools.Obj(new { topic = AgentTools.Str("Topic id or alias, e.g. spring, money, mines, cc, gifts (default: the overview and topic list)") }),
            Instant = true, NeedsWorld = false, AppendStatus = false,
            Run = Guide
        }
    };

    private static IEnumerator Guide(JsonElement a, CallContext c)
    {
        (bool ok, string text) r;
        try
        {
            _book ??= new GuideBook(Path.Combine(AgentTools.Rt.Helper.DirectoryPath, "assets", "guide"));
            r = _book.Answer(AgentTools.S(a, "topic"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            r = (false, $"guide could not read the guide files ({ex.GetType().Name}: {ex.Message}). Tell the player; reinstalling the mod restores them.");
        }
        if (r.ok) c.Result = r.text; else c.Fail(r.text);
        yield break;
    }
}
