using System.Collections;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.Machines;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace StardewMCP.Agent;

// Semantic one-tile interaction tools (phase 3 of docs/tool-decoupling/README.md): open_container, load_machine,
// use_held_item, read_sign. Each does one named job a wrong generic interact can botch — opening a chest, loading a
// machine, triggering a totem, reading a sign — verifies the world changed the intended way and fails honestly when
// it did not. They share the extracted hold/press/use logic with the legacy interact tool (SlotToHold /
// HoldForActionPress / PressAction / UseHeldItemInPlace / WaitAfterInPlaceUse in AgentTools.cs) instead of calling
// interact with a fabricated holding/x/y JSON. The ToolDef registrations live in Agent/SemanticTools.cs.

public static partial class AgentTools
{
    /// <summary>open_container: open the chest or fridge at x,y, verified to be one before the walk and to have opened after the press.</summary>
    internal static IEnumerator OpenContainerSemantic(JsonElement a, CallContext c)
    {
        if (Game1.activeClickableMenu != null)
        {
            c.Fail("A menu is already open. dismiss_menu first, or use the open container without x, y.");
            yield break;
        }
        var tile = new Point(I(a, "x", -1), I(a, "y", -1));
        // A container must stand here BEFORE anything is pressed: the call can never report a shop or a dialogue as "opened".
        var chest = ContainerAt(Loc, tile);
        if (chest == null)
        {
            var there = WorldText.Describe(Loc, tile.X, tile.Y);
            c.Fail(Reasons.NotHere, $"No chest or fridge at ({tile.X},{tile.Y}) on {Loc.Name} ({there.label ?? "nothing there"}). Pick the tile look shows the container on.");
            yield break;
        }
        yield return WalkAdjacent(tile, c);
        if (!c.Ok) yield break;
        // A fresh press with empty hands opens the chest; a held object would be loaded into it instead.
        HoldForActionPress(-1);
        PressAction(tile);
        for (int i = 0; i < 120 && Game1.activeClickableMenu == null; i++) yield return null;
        if (Game1.activeClickableMenu == null && chest.GetMutex().IsLocked())
        {
            // The game lets one farmer at a time open a chest: wait for the other to close it, then press again.
            // Only running ticks count: while an AI thinks the world is frozen and the other farmer can't close it.
            for (int running = 0; running < 60 * ChestBusyWaitSeconds && chest.GetMutex().IsLocked();)
            {
                if (!WorldClock.Frozen) running++;
                yield return null;
            }
            if (!chest.GetMutex().IsLocked())
            {
                PressAction(tile);
                for (int i = 0; i < 120 && Game1.activeClickableMenu == null; i++) yield return null;
            }
        }
        if (Game1.activeClickableMenu is ItemGrabMenu { context: Chest } grab)
        {
            c.Result = $"open_container: the {ContainerName((Chest)grab.context)} at ({tile.X},{tile.Y}) is open. " +
                       "list_chest reads it; store_items and take_items move items without x, y; dismiss_menu leaves it.";
            yield break;
        }
        // No container menu: name what the press did instead, close it, and fail honestly.
        var opened = Game1.activeClickableMenu;
        string note = opened == null ? "nothing opened."
            : WorldText.MenuState().Trim().Replace("\n", " ");
        if (opened is DialogueBox db) { db.closeDialogue(); Game1.dialogueUp = false; }
        else if (opened != null) { opened.exitThisMenu(); yield return Ticks(10); }
        c.Fail(Reasons.NotHere, $"open_container failed: pressing ({tile.X},{tile.Y}) opened no chest or fridge menu — {note}");
    }

    /// <summary>The storage container standing on this tile: a chest or placed mini-fridge as a map object, or the
    /// farmhouse kitchen fridge. The shipping bin is not included (ship sells through it; it is not storage).</summary>
    private static Chest? ContainerAt(GameLocation loc, Point tile)
    {
        if (loc.Objects.TryGetValue(tile.ToVector2(), out var o) && o is Chest ch) return ch;
        if (loc is FarmHouse f && f.fridge.Value != null && f.fridgePosition == tile) return f.fridge.Value;
        return null;
    }

    private static string ContainerName(Chest chest) => chest.fridge.Value ? "fridge" : chest.DisplayName;

    /// <summary>load_machine: put one kind of inventory item into the machine at x,y — verified to be a machine that takes items.</summary>
    internal static IEnumerator LoadMachineSemantic(JsonElement a, CallContext c)
    {
        var item = S(a, "item").Trim();
        if (item.Length == 0)
        {
            c.Fail(Reasons.InvalidArgs, "item is required: what to load, from your inventory (e.g. Copper Ore into a Furnace). An empty call never loads anything.");
            yield break;
        }
        var tile = new Point(I(a, "x", -1), I(a, "y", -1));
        // A villager on the tile would receive the item as a gift: refuse before the press makes it one.
        var villager = Loc.characters.FirstOrDefault(n => !n.IsMonster && !n.IsInvisible && n.TilePoint == tile);
        if (villager != null)
        {
            c.Fail(Reasons.NotHere, $"{villager.displayName} is standing on ({tile.X},{tile.Y}): pressing there holding an item would GIVE it to them, not load a machine. " +
                                    "Wait for them to step off, or gift deliberately with give_gift.");
            yield break;
        }
        if (!Loc.Objects.TryGetValue(tile.ToVector2(), out var obj) || obj is not SObject machine)
        {
            var there = WorldText.Describe(Loc, tile.X, tile.Y);
            c.Fail(Reasons.NotHere, $"No machine at ({tile.X},{tile.Y}) on {Loc.Name} ({there.label ?? "nothing there"}). look and machines() show placed machines with coordinates.");
            yield break;
        }
        if (!MachineTakesItems(machine))
        {
            c.Fail(Reasons.NotHere, $"{machine.DisplayName} is not a machine you load with an item (it fills by itself or takes nothing). " +
                                    (machine.readyForHarvest.Value ? "It has output ready: take it with interact on the tile." : ""));
            yield break;
        }
        if (machine.readyForHarvest.Value)
        {
            c.Fail(Reasons.NotHere, $"The {machine.DisplayName} is finished ({machine.heldObject.Value?.DisplayName} is ready): take the output first (interact on the tile), then load it again.");
            yield break;
        }
        if (machine.heldObject.Value != null)
        {
            c.Fail(Reasons.NotHere, $"The {machine.DisplayName} is already working on {machine.heldObject.Value.DisplayName}. Wait for it (machines() shows the ready time) or empty it first.");
            yield break;
        }
        int slot = FindItem(item);
        if (slot < 0) { c.Fail(Reasons.NoItem, $"No \"{item}\" in inventory."); yield break; }

        int had = P.Items[slot]?.Stack ?? 0;
        yield return WalkAdjacent(tile, c);
        if (!c.Ok) yield break;
        HoldForActionPress(slot);
        bool pressed = PressAction(tile);
        yield return Ticks(30);
        yield return WaitIdle(120);
        // Verification by effect: the machine took the item (the pack slot emptied/shrank) or now holds something.
        bool took = pressed && (machine.heldObject.Value != null || P.Items[slot] == null || P.Items[slot].Stack < had);
        if (!took)
        {
            c.Fail(Reasons.NotHere, $"The {machine.DisplayName} did not take the {item}. It may not be a valid input for this machine — " +
                                    "machines(machine=...) shows exactly what it takes and makes.");
            yield break;
        }
        int used = System.Math.Max(1, had - (P.Items[slot]?.Stack ?? 0));
        string inside = machine.heldObject.Value != null ? $", it now holds {machine.heldObject.Value.DisplayName}" : "";
        c.Result = $"load_machine: the {machine.DisplayName} at ({tile.X},{tile.Y}) took {used}x {item}{inside} and is working.";
    }

    /// <summary>Whether Data/Machines lists this object with a rule triggered by placing an item in it: the definition
    /// of "a machine you load", the same test the machines() overview uses (MachineTools.Scan).</summary>
    private static bool MachineTakesItems(SObject obj)
    {
        if (!DataLoader.Machines(Game1.content).TryGetValue(obj.QualifiedItemId, out var data) || data == null) return false;
        return data.OutputRules?.Any(r => r?.Triggers?.Any(t => t != null && t.Trigger.HasFlag(MachineOutputTrigger.ItemPlacedInMachine)) == true) == true;
    }

    /// <summary>use_held_item: use one inventory item where you stand (a totem, a book...), through the shared hold/use logic.</summary>
    internal static IEnumerator UseHeldItemSemantic(JsonElement a, CallContext c)
    {
        var item = S(a, "item").Trim();
        int slot = SlotToHold(item, c);
        if (!c.Ok) yield break;
        if (item.Length == 0)
        {
            c.Fail(Reasons.InvalidArgs, "item is required: which inventory item to use where you stand (a totem, a book...). " +
                                        "To act on a tile (a machine, a container, a sign) use load_machine, open_container, read_sign or interact instead.");
            yield break;
        }
        var before = Snapshot();
        var startLoc = Loc;
        yield return WaitIdle();
        HoldForActionPress(slot);
        var what = UseHeldItemInPlace(c);
        if (!c.Ok) yield break;
        yield return Ticks(30);
        yield return WaitIdle(600);
        yield return WaitAfterInPlaceUse();
        var moved = Loc != startLoc ? $" You are now in {Loc.Name}." : "";
        c.Result = $"use_held_item: {what}.{moved} {Diff(before)}".Trim();
        var menu = WorldText.MenuState().Trim();
        if (menu.Length > 0) c.Result = c.Result.TrimEnd() + "\n" + menu;
    }

    /// <summary>read_sign: read the text of the sign at x,y, verified to be a sign or a readable text tile; never edits it.</summary>
    internal static IEnumerator ReadSignSemantic(JsonElement a, CallContext c)
    {
        var tile = new Point(I(a, "x", -1), I(a, "y", -1));
        // A placed text sign shows its text on hover; pressing it would open the EDIT box, so the text is read directly.
        if (Loc.Objects.TryGetValue(tile.ToVector2(), out var o) && o.IsTextSign())
        {
            yield return WalkAdjacent(tile, c);
            if (!c.Ok) yield break;
            var signText = (o.SignText ?? "").Replace("^", " ").Replace("\n", " ").Trim();
            c.Result = signText.Length > 0 ? $"read_sign: the sign at ({tile.X},{tile.Y}) says: \"{signText}\""
                : $"read_sign: the sign at ({tile.X},{tile.Y}) is blank.";
            yield break;
        }
        if (o is Sign display)
        {
            var shown = display.displayItem.Value;
            c.Fail(Reasons.NotHere, $"The sign at ({tile.X},{tile.Y}) is a display sign{(shown != null ? $" showing {shown.DisplayName}" : "")}: it has no text to read.");
            yield break;
        }
        // Pierre's shop sign is a map tile answered by Town.checkAction, not an object or an Action property.
        if (Loc is Town && Loc.getTileIndexAt(new xTile.Dimensions.Location(tile.X, tile.Y), "Buildings", "Town") == 620)
        {
            yield return WalkAdjacent(tile, c);
            if (!c.Ok) yield break;
            c.Result = $"read_sign: the sign at ({tile.X},{tile.Y}) says: \"{ActionTiles.SeedShopSign()}\"";
            yield break;
        }
        var action = Loc.doesTileHaveProperty(tile.X, tile.Y, "Action", "Buildings");
        if (action == null || !action.TrimStart().StartsWith("Message", System.StringComparison.OrdinalIgnoreCase))
        {
            var there = WorldText.Describe(Loc, tile.X, tile.Y);
            c.Fail(Reasons.NotHere, $"No sign or readable text at ({tile.X},{tile.Y}) on {Loc.Name} ({there.label ?? "nothing there"}). look shows signs with coordinates.");
            yield break;
        }
        yield return WalkAdjacent(tile, c);
        if (!c.Ok) yield break;
        // Empty hands: a held item would be placed or consumed instead of reading.
        HoldForActionPress(-1);
        PressAction(tile);
        yield return Ticks(15);
        if (Game1.activeClickableMenu is DialogueBox box)
        {
            var text = (box.getCurrentString() ?? "").Replace("^", " ").Replace("\n", " ").Trim();
            box.closeDialogue();
            Game1.dialogueUp = false;
            yield return Ticks(10);
            c.Result = $"read_sign: the sign at ({tile.X},{tile.Y}) says: \"{text}\"";
            yield break;
        }
        // No sign text appeared: name what the press did instead, close it, and fail honestly.
        var opened = Game1.activeClickableMenu;
        string note = opened == null ? "nothing happened." : WorldText.MenuState().Trim().Replace("\n", " ");
        if (opened is DialogueBox db2) { db2.closeDialogue(); Game1.dialogueUp = false; }
        else if (opened != null) { opened.exitThisMenu(); yield return Ticks(10); }
        c.Fail(Reasons.NotHere, $"read_sign failed: pressing ({tile.X},{tile.Y}) showed no sign text — {note}");
    }
}
