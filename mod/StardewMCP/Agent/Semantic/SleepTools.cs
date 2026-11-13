using System.Collections;
using System.Text.Json;
using StardewValley;

namespace StardewMCP.Agent;

// Semantic (intent-shaped) sleep (phase 4B of docs/tool-decoupling/README.md). The legacy go_to_bed in
// Agent/AgentTools.cs refuses until today's journal is written and, once a day, talks a wide-awake farmer
// out of an early night; its menu close on the way to bed accepts a letter's quest and claims a grab
// menu's items. sleep is the single-intent end of the day: no journal requirement, an explicit
// confirm_early instead of the once-a-day persuasion, and a menu that still hides a decision is refused
// rather than decided by a side effect. The night's own loop is the shared SleepThroughNight core, so the
// sleep itself cannot drift from the legacy path.

public static partial class AgentTools
{
    /// <summary>
    /// sleep: end the day. No journal requirement (that rule belongs to the legacy go_to_bed). Before 17:00,
    /// with more than a quarter of the energy bar left, the farmer is wide awake: the call fails with a warning
    /// unless confirm_early=true makes the early night an explicit decision. On the way to bed an open menu is
    /// dismissed, never decided — a letter's quest or attachments or a grab menu's items refuse the call and
    /// name the tool that takes the decision first (accept_letter_quest, claim_menu_items, dismiss_menu).
    /// The rest of the night is the shared legacy core.
    /// </summary>
    internal static IEnumerator SleepSemantic(JsonElement a, CallContext c)
    {
        // Bed in the morning is a choice, and a wide-awake farmer is told so: unlike the legacy once-a-day
        // persuasion, the explicit confirm_early flag is the decision.
        if (Game1.timeOfDay < 1700 && P.Stamina > P.MaxStamina / 4 && !Observation.Flag(a, "confirm_early"))
        {
            c.Fail(Reasons.NotReady, $"It's only {WorldText.FormatTime(Game1.timeOfDay)} and you aren't tired at all (energy {(int)P.Stamina}/{P.MaxStamina}): " +
                                     "lying down now you'd just stare at the ceiling. The shops, the townspeople, the beach and the mines are all open for hours yet. " +
                                     "Pass confirm_early=true only if you really mean to sleep this early.");
            yield break;
        }

        yield return SleepThroughNight(c, claimMenuClose: false);
    }
}
