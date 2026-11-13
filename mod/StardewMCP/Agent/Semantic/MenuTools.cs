using System.Collections;
using System.Text.Json;
using StardewValley;
using StardewValley.Menus;

namespace StardewMCP.Agent;

// Semantic (intent-shaped) menu tools (phase 3 of docs/tool-decoupling/README.md): the frequent menu() operations
// get one named tool each, so routine play needs no menu(action=...). The legacy "menu" tool in Agent/MenuTools.cs
// keeps all its actions and stays in the MCP catalog unchanged — and in the preferred catalog too, as the fallback
// for menus none of these covers. The ToolDef registrations live in Agent/SemanticTools.cs; this file only holds
// the Run bodies, reusing the legacy click/read implementations (ReadMenu / ChooseFloor / ChooseFromList / Amount /
// PressOk) so behavior cannot drift. read_menu is a pure instant observation; every action clicks the menu's own
// buttons, as a player would, and reports the change plus the menu's new state.

public static partial class AgentTools
{
    /// <summary>read_menu: what the open menu holds and which actions work, as a pure instant read (the legacy menu() read).</summary>
    internal static IEnumerator ReadMenuSemantic(JsonElement a, CallContext c)
    {
        if (Game1.activeClickableMenu is not { } menu)
        {
            c.Fail("No menu is open.");
            yield break;
        }
        c.Result = ReadMenu(menu);
        yield break;
    }

    /// <summary>choose_mine_floor: the legacy menu(action=choose) path for the mine elevator, with the floor as a number.</summary>
    internal static IEnumerator ChooseMineFloorSemantic(JsonElement a, CallContext c)
    {
        if (!Has(a, "floor"))
        {
            c.Fail(Reasons.InvalidArgs, "floor is required: the floor number as read_menu lists it (0 = the mine entrance).");
            yield break;
        }
        if (Game1.activeClickableMenu is not MineElevatorMenu m)
        {
            c.Fail("The mine elevator menu is not open. In the Mine, press the elevator switch (interact on its tile) first.");
            yield break;
        }
        yield return MenuActionSemantic(m, ChooseFloor(m, ArgsOf(new { option = I(a, "floor", -1).ToString() }), c), c);
    }

    /// <summary>choose_menu_option: the legacy menu(action=choose) path for option lists (jukebox and choose-one menus).</summary>
    internal static IEnumerator ChooseMenuOptionSemantic(JsonElement a, CallContext c)
    {
        if (S(a, "option").Trim().Length == 0)
        {
            c.Fail(Reasons.InvalidArgs, "option is required: the option label as read_menu lists it.");
            yield break;
        }
        if (Game1.activeClickableMenu is not ChooseFromListMenu m)
        {
            c.Fail("No option list is open (the jukebox, or a choose-one menu). read_menu shows what is open.");
            yield break;
        }
        yield return MenuActionSemantic(m, ChooseFromList(m, a, c), c);
    }

    /// <summary>set_menu_amount: the legacy menu(action=amount) path — type the number into the open number entry and press OK.</summary>
    internal static IEnumerator SetMenuAmountSemantic(JsonElement a, CallContext c)
    {
        if (!Has(a, "value"))
        {
            c.Fail(Reasons.InvalidArgs, "value is required: the number to enter, within the range the menu shows (read_menu).");
            yield break;
        }
        if (Game1.activeClickableMenu is not NumberSelectionMenu m)
        {
            c.Fail("No number entry is open. read_menu shows what is open.");
            yield break;
        }
        yield return MenuActionSemantic(m, Amount(m, a, c), c);
    }

    /// <summary>confirm_menu: press the open menu's OK button, confirming what it shows.</summary>
    internal static IEnumerator ConfirmMenuSemantic(JsonElement a, CallContext c)
    {
        if (Game1.activeClickableMenu is not { } menu)
        {
            c.Fail("No menu is open.");
            yield break;
        }
        if (menu is DialogueBox or ShopMenu or LetterViewerMenu)
        {
            c.Fail($"{MenuName(menu)} has its own tool: respond(option) answers dialogue, buy(item) shops, dismiss_menu closes shops and letters. " +
                   "confirm_menu is for menus with an OK button (number and name entry, choose-one lists).");
            yield break;
        }
        yield return MenuActionSemantic(menu, PressOk(menu, c), c);
    }

    /// <summary>
    /// The tail of the legacy menu tool: after a successful click, wait, then report what changed and the menu's new
    /// state, so the result reads the same as menu(action=...) did.
    /// </summary>
    private static IEnumerator MenuActionSemantic(IClickableMenu menu, IEnumerator run, CallContext c)
    {
        var before = Snapshot();
        yield return run;
        if (!c.Ok) yield break;
        yield return Ticks(10);
        var diff = Diff(before);
        var now = Game1.activeClickableMenu is { } open ? ReadMenu(open) : "The menu is closed.";
        c.Result = $"{c.Result} {diff}".Trim() + "\n" + now;
    }
}
