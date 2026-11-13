using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewValley;
using StardewValley.Menus;

namespace StardewMCP.Agent;

// Semantic (intent-shaped) replacements for close_menu's hidden side effects (phase 4A of
// docs/tool-decoupling/README.md). The legacy close_menu in Agent/AgentTools.cs still claims a letter's
// quest and a one-shot grab menu's items before it closes (ClaimBeforeClose, Agent/CloseMenuTools.cs);
// these tools make each of those decisions explicit, and plain closing moves to dismiss_menu, which
// never accepts a quest and never claims an item. The ToolDef registrations live in Agent/SemanticTools.cs;
// the Run bodies reuse the shared AcceptLetterQuest / ClaimGrabMenu helpers so behavior cannot drift.

public static partial class AgentTools
{
    /// <summary>
    /// dismiss_menu: close or cancel the open menu or dialogue, and nothing else. A letter that still offers a
    /// quest or holds attachments is refused (the game would accept/hand them over on exit), as is a one-shot
    /// item menu that still holds items (closing would lose them): the failure names the tool that takes the
    /// explicit decision first. With nothing open the call is an answered no-op (no_change).
    /// </summary>
    internal static IEnumerator DismissMenuSemantic(JsonElement a, CallContext c)
    {
        var menu = Game1.activeClickableMenu;
        if (menu == null) { c.NoChange("No menu open."); yield break; }
        // The box that waits for a proposal's answer comes back unless the proposal itself is cancelled, and the other
        // farmer's game has to confirm that (same handling as the legacy close_menu).
        if (menu is PendingProposalDialog waiting)
        {
            waiting.cancelProposal(P);
            for (int i = 0; i < 300 && Game1.activeClickableMenu == waiting; i++) yield return null;
            if (Game1.activeClickableMenu == waiting)
                c.Fail(Reasons.MenuOpen, "The proposal could not be withdrawn yet: the other farmer's game has not confirmed it. Try dismiss_menu again in a moment.");
            else
                c.Result = "Proposal withdrawn. Menu closed.";
            yield break;
        }
        if (menu is LetterViewerMenu letter)
        {
            if (letter.HasQuestOrSpecialOrder)
            {
                c.Fail("This letter offers a quest; the game accepts it as soon as the letter closes, so dismissing would accept it. " +
                       "Use accept_letter_quest to take the quest (its attachments come with it), or leave the letter open.");
                yield break;
            }
            if (letter.itemsLeftToGrab())
            {
                c.Fail("This letter still has attached items; closing it would hand them over. " +
                       "Use accept_letter_quest to take them, then dismiss_menu.");
                yield break;
            }
        }
        if (menu is ItemGrabMenu { shippingBin: false, showReceivingMenu: true } grab && grab.source != ItemGrabMenu.source_chest
            && grab.ItemsToGrabMenu.actualInventory.Any(i => i != null))
        {
            c.Fail("This one-shot item menu (fishing treasure, a gift, fish pond output...) still holds items; closing it would lose them. " +
                   "Use claim_menu_items to take what fits, then dismiss_menu.");
            yield break;
        }
        if (Game1.activeClickableMenu is DialogueBox box)
            box.closeDialogue();
        else
            Game1.activeClickableMenu.exitThisMenu();
        yield return Ticks(20);
        if (Game1.activeClickableMenu != null) Game1.exitActiveMenu();
        c.Result = "Menu closed.";
    }

    /// <summary>
    /// accept_letter_quest: accept the open letter's quest or special order and hand over its attached items,
    /// exactly the game's own exit would (AcceptQuest + cleanupBeforeExit's attachment hand-over). The letter
    /// stays open; dismiss_menu closes it. A letter without quest or attachments is an answered no-op (no_change).
    /// </summary>
    internal static IEnumerator AcceptLetterQuestSemantic(JsonElement a, CallContext c)
    {
        if (Game1.activeClickableMenu is not LetterViewerMenu letter)
        {
            c.Fail("No letter is open. read_menu shows what is open.");
            yield break;
        }
        var notes = new List<string>();
        if (letter.HasQuestOrSpecialOrder)
        {
            bool specialOrder = letter.specialOrderId != null;
            AcceptLetterQuest(letter, notes); // the letter's own AcceptQuest, shared with close_menu
            if (notes.Count == 0 && specialOrder && letter.specialOrderId == null)
                notes.Add("Accepted the special order."); // a team order is not in the journal, so the shared helper stays silent
        }
        HandOverLetterAttachments(letter, notes);
        if (notes.Count == 0) { c.NoChange("The letter has no quest to accept and no attachments; nothing to do."); yield break; }
        c.Result = string.Join(" ", notes) + " The letter is still open; dismiss_menu closes it.";
        yield break;
    }

    /// <summary>
    /// The letter's own exit hands attachments over in cleanupBeforeExit; this mirrors that hand-over explicitly
    /// (what clicking each attached item does) so quest acceptance and item claiming stay separate decisions.
    /// </summary>
    private static void HandOverLetterAttachments(LetterViewerMenu letter, List<string> notes)
    {
        var attached = letter.itemsToGrab.Where(cc => cc.item != null).Select(cc => cc.item).ToList();
        if (attached.Count == 0) return;
        letter.itemsToGrab.Clear();
        Game1.playSound("coin");
        Game1.player.addItemsByMenuIfNecessary(attached);
        notes.Add($"Received the letter's attached item(s): {Names(attached)}.");
        if (Game1.activeClickableMenu is ItemGrabMenu)
            notes.Add("Not everything fit; an item menu opened with the rest: claim_menu_items takes what fits, dismiss_menu closes it.");
    }

    /// <summary>
    /// claim_menu_items: take what fits from the open one-shot ItemGrabMenu (fishing treasure, a gift, fish pond
    /// output), the way the legacy close_menu did before closing; on an open letter it hands over the attachments
    /// (what clicking each does) without accepting the quest. Chests and the shipping bin are refused (their
    /// items stay put; use take_items to move them) and the menu is left open — claiming never implies closing.
    /// </summary>
    internal static IEnumerator ClaimMenuItemsSemantic(JsonElement a, CallContext c)
    {
        if (Game1.activeClickableMenu is not { } menu)
        {
            c.Fail("No menu is open.");
            yield break;
        }
        if (menu is LetterViewerMenu letter)
        {
            var letterNotes = new List<string>();
            HandOverLetterAttachments(letter, letterNotes);
            if (letterNotes.Count == 0) { c.NoChange("The letter has no attachments to claim. Its quest was NOT accepted — accept_letter_quest does that; dismiss_menu closes the letter."); yield break; }
            c.Result = string.Join(" ", letterNotes) + " The letter is still open; dismiss_menu closes it.";
            yield break;
        }
        if (menu is not ItemGrabMenu grab)
        {
            c.Fail($"{MenuName(menu)} is not an item menu; there is nothing to claim. claim_menu_items is for one-shot item menus " +
                   "(fishing treasure, a gift, fish pond output). read_menu shows what is open.");
            yield break;
        }
        if (grab.source == ItemGrabMenu.source_chest)
        {
            c.Fail("This is a chest menu: its items stay in the chest when the menu closes, so there is nothing to claim. " +
                   "Move what you want with take_items, then dismiss_menu.");
            yield break;
        }
        if (grab.shippingBin || !grab.showReceivingMenu)
        {
            c.Fail("This menu only receives what you put into it; it holds nothing to claim. dismiss_menu closes it.");
            yield break;
        }
        if (!grab.ItemsToGrabMenu.actualInventory.Any(i => i != null))
        {
            c.NoChange("The menu holds nothing to claim.");
            yield break;
        }
        var notes = new List<string>();
        yield return ClaimGrabMenu(notes); // the shared click-each-slot loop, same as close_menu used
        c.Result = notes.Count > 0
            ? string.Join(" ", notes)
            : "Nothing could be taken; make room in your inventory and try claim_menu_items again.";
    }
}
