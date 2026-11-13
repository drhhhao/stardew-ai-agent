using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Tools;

namespace StardewMCP.Agent;

/// <summary>
/// chest: look into, fill or empty a storage chest (or the fridge) in one call. It is a macro over the primitives:
/// interact opens the chest (walking next to it), then the menu tool's own take / put clicks move the items, then the
/// menu is closed. Nothing here writes to Chest.Items directly, so whatever hooks the primitives (ownership, theft
/// events) sees these moves too. Phase 3 of docs/tool-decoupling/README.md: this tool is Legacy; the built-in AI gets
/// the semantic list_chest / list_containers / store_items / take_items instead (Agent/Semantic/ChestTools.cs), which
/// reuse the shared parsing, opening, listing and transfer logic kept in this file so the two catalogs cannot drift.
/// </summary>
public static partial class AgentTools
{
    private static ToolDef ChestToolDef() => new()
    {
        Name = "chest",
        // Phase 3 of docs/tool-decoupling/README.md: superseded by list_chest / list_containers / store_items /
        // take_items for the built-in AI; this mode-switch entry stays callable for old MCP/HTTP clients.
        Legacy = true,
        Description = "Legacy: prefer list_chest (read the open chest or fridge without walking), list_containers (every chest and " +
                      "fridge on the farm), store_items (pack into a chest) and take_items (chest into your pack). chest stays callable " +
                      "for old clients: in one call you walk next to a storage chest or the fridge, open it, move the items and close it again. " +
                      "action: list (default), store (your pack into the chest), take (chest into your pack), stash (find or create a chest, then store everything), " +
                      "create (craft/place a chest and report where), or all (every chest and fridge on the farm; no walking). " +
                      "stash with no item stores everything except tools, seeds and cooked dishes; give item/items to keep those instead. " +
                      "quality: normal, silver, gold or iridium to move only that quality. quantity: how many of each named item (default all). " +
                      "x, y is the chest tile; leave it out to use an open chest, or with stash/create let the tool find a free spot near you and craft a Chest (50 wood) when there is none. " +
                      "A chest another farmer has open is waited for (up to 10 s).",
        Schema = Obj(new
        {
            x = Int("Chest tile x"),
            y = Int("Chest tile y"),
            action = new { type = "string", @enum = new[] { "list", "store", "take", "stash", "create", "all" }, description = "list (default), store, take, stash, create or all" },
            item = Str("Item name, or \"everything\""),
            items = new { type = "array", items = new { type = "string" }, description = "Several item names" },
            quality = new { type = "string", @enum = new[] { "normal", "silver", "gold", "iridium" }, description = "Only this quality (default: all)" },
            quantity = Int("How many of each named item (default: all)"),
            reason = Str(ReasonDesc)
        }, "reason"),
        Run = ChestTool
    };

    // ------------------------------------------------------------------ quality

    internal static string QualityName(int quality) => quality switch
    {
        0 => "normal",
        1 => "silver",
        2 => "gold",
        4 => "iridium",
        _ => $"quality {quality}"
    };

    private static int? QualityValue(string text) => text.Trim().ToLowerInvariant() switch
    {
        "normal" or "basic" or "regular" or "0" => 0,
        "silver" or "1" => 1,
        "gold" or "2" => 2,
        "iridium" or "4" => 4,
        _ => null
    };

    /// <summary>
    /// Filter for the optional "quality" argument of menu take / put and chest. Null when the argument is absent;
    /// an unknown quality name matches nothing instead of silently matching everything.
    /// </summary>
    internal static Func<Item, bool>? QualityIs(JsonElement a)
    {
        var text = S(a, "quality").Trim();
        if (text.Length == 0) return null;
        var wanted = QualityValue(text);
        return it => wanted != null && it.Quality == wanted;
    }

    // ------------------------------------------------------------------ transfer arguments

    /// <summary>Parsed item/items/quality/quantity of a store or take: shared by the legacy chest tool and the semantic
    /// store_items / take_items, so the two catalogs cannot drift. "everything" (or "all") must be explicit.</summary>
    private sealed class ChestMoves
    {
        public List<string> Names = new();
        public bool Everything;
        public int? Quality;
        public string QualityText = "";
        public int? Quantity;
    }

    /// <summary>Parse the move arguments of <paramref name="a"/> into <paramref name="mv"/>; false (with c failed) on a bad
    /// quality or, when <paramref name="requireTargets"/>, on no item named at all. <paramref name="stashDefaultsToAll"/>:
    /// a stash with no list tidies the whole pack.</summary>
    private static bool TryParseChestMoves(JsonElement a, CallContext c, bool stashDefaultsToAll, bool requireTargets, out ChestMoves mv)
    {
        mv = new ChestMoves();
        mv.QualityText = S(a, "quality").Trim();
        if (mv.QualityText.Length > 0)
        {
            mv.Quality = QualityValue(mv.QualityText);
            if (mv.Quality == null)
            {
                c.Fail($"Unknown quality \"{mv.QualityText}\". Use normal, silver, gold or iridium.");
                return false;
            }
        }
        mv.Quantity = Has(a, "quantity") ? Math.Max(1, I(a, "quantity", 1)) : null;
        mv.Names = StrList(a, "items");
        var single = S(a, "item").Trim();
        if (single.Length > 0) mv.Names.Add(single);
        mv.Everything = mv.Names.Any(IsAll);
        mv.Names.RemoveAll(IsAll);
        if (stashDefaultsToAll && mv.Names.Count == 0) mv.Everything = true; // stash with no list = tidy the pack
        if (requireTargets && !mv.Everything && mv.Names.Count == 0)
        {
            c.Fail("Say what to move: item, items, or \"everything\" (list_chest shows what is inside).");
            return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ small helpers

    private static JsonElement ArgsOf(object o) => JsonDocument.Parse(JsonSerializer.Serialize(o)).RootElement.Clone();

    private static List<string> StrList(JsonElement a, string name)
    {
        var list = new List<string>();
        if (a.ValueKind == JsonValueKind.Object && a.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var e in arr.EnumerateArray())
                if (e.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString()))
                    list.Add(e.GetString()!.Trim());
        return list;
    }

    private static bool IsAll(string name) =>
        name.Equals("everything", StringComparison.OrdinalIgnoreCase) || name.Equals("all", StringComparison.OrdinalIgnoreCase);

    /// <summary>"Parsnip (gold)"; tools have no quality.</summary>
    private static string StackLabel(Item it) => it is Tool ? it.DisplayName : $"{it.DisplayName} ({QualityName(it.Quality)})";

    private static string Stacks(IEnumerable<Item> items)
    {
        var parts = items.Where(i => i != null)
            .GroupBy(i => (i.QualifiedItemId, i.Quality))
            .Select(g => (label: StackLabel(g.First()), count: g.Sum(i => i.Stack)))
            .OrderBy(x => x.label)
            .Select(x => $"{x.count}x {x.label}")
            .ToList();
        return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
    }

    /// <summary>"12x Parsnip (normal), 3x Parsnip (gold)": counts per label, in the order first seen.</summary>
    private static string Tally(List<(string label, int count)> parts) =>
        string.Join(", ", parts.GroupBy(p => p.label).Select(g => $"{g.Sum(p => p.count)}x {g.Key}"));

    private static int CountOf(IEnumerable<Item?> items, string qid, int quality) =>
        items.Where(i => i != null && i.QualifiedItemId == qid && i.Quality == quality).Sum(i => i!.Stack);

    private static string ChestLabel(ItemGrabMenu g) => g.context switch
    {
        Chest { fridge.Value: true } => "Fridge",
        Chest ch => ch.DisplayName,
        _ => "Chest"
    };

    /// <summary>What the "everything" mode of store leaves in the pack: tools, seeds (category -74) and cooked dishes (category -7).</summary>
    private static bool KeepOutOfChest(Item it) => it is Tool || it.Category == -74 || it.Category == -7;

    /// <summary>How long chest waits, in running seconds, for a chest another farmer has open.</summary>
    private const int ChestBusyWaitSeconds = 10;

    /// <summary>The chest (or mini-fridge) standing on <paramref name="tile"/>, if any.</summary>
    private static Chest? ChestAt(GameLocation loc, Point tile) =>
        loc.Objects.TryGetValue(tile.ToVector2(), out var o) ? o as Chest : null;

    private sealed class ChestSpot
    {
        public Point Tile;
        public string Note = "";
    }

    /// <summary>The nearest real storage chest on this map, ignoring the kitchen fridge.</summary>
    private static ChestSpot? NearestChest(GameLocation loc, Point from)
    {
        ChestSpot? best = null;
        float bestDist = float.MaxValue;
        foreach (var obj in loc.Objects.Values)
        {
            if (obj is not Chest chest || chest.fridge.Value) continue;
            var tile = new Point((int)chest.TileLocation.X, (int)chest.TileLocation.Y);
            float dist = AgentTools.TileDistance(from, tile);
            if (dist >= bestDist) continue;
            best = new ChestSpot { Tile = tile };
            bestDist = dist;
        }
        return best;
    }

    /// <summary>
    /// A stash/create target: the x,y the caller gave, else the nearest chest on this map, else a freshly crafted Chest
    /// placed on a free tile near the farmer. The note says when a chest had to be made.
    /// </summary>
    private static IEnumerator EnsureChest(JsonElement a, CallContext c, ChestSpot spot)
    {
        var loc = Loc;
        if (Has(a, "x") && Has(a, "y"))
        {
            spot.Tile = new Point(I(a, "x", -1), I(a, "y", -1));
            yield break;
        }

        var existing = NearestChest(loc, P.TilePoint);
        if (existing != null)
        {
            spot.Tile = existing.Tile;
            spot.Note = $"Using the chest at ({existing.Tile.X},{existing.Tile.Y}).";
            yield break;
        }

        var craft = new CallContext();
        yield return Craft(ArgsOf(new { recipe = "Chest", quantity = 1, reason = Reason(a) }), craft);
        if (!craft.Ok)
        {
            c.Fail($"No chest on {loc.Name} and one could not be crafted (a Chest needs 50 Wood): {craft.Result}");
            yield break;
        }

        var tiles = FreeTilesNear(loc, P.TilePoint, 8);
        if (tiles.Count == 0)
        {
            c.Fail($"Crafted a Chest but there is no free tile near you on {loc.Name} to place it. Move to open ground and call chest(action=create).");
            yield break;
        }

        string last = "";
        foreach (var tile in tiles)
        {
            var place = new CallContext();
            yield return Place(ArgsOf(new
            {
                item = "Chest",
                tiles = new[] { new[] { tile.X, tile.Y } },
                reason = Reason(a)
            }), place);
            if (place.Ok && ChestAt(loc, tile) != null)
            {
                spot.Tile = tile;
                spot.Note = $"No chest was near, so I crafted and placed one at ({tile.X},{tile.Y}).";
                yield break;
            }
            last = place.Result;
        }
        c.Fail($"Crafted a Chest but could not place it near you on {loc.Name}. {last}".Trim());
    }

    /// <summary>Walkable, empty tiles around the farmer, nearest first (a chest needs a free tile the game will accept).</summary>
    private static List<Point> FreeTilesNear(GameLocation loc, Point from, int radius)
    {
        var tiles = new List<Point>();
        for (int r = 1; r <= radius; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var t = new Point(from.X + dx, from.Y + dy);
                    if (!loc.isTileOnMap(t.X, t.Y) || loc.Objects.ContainsKey(t.ToVector2())) continue;
                    if (loc.furniture.Any(f => f.GetBoundingBox().Contains(t.X * 64 + 32, t.Y * 64 + 32))) continue;
                    if (!Pathfinder.IsWalkable(loc, t.X, t.Y)) continue;
                    tiles.Add(t);
                }
        return tiles;
    }

    // ------------------------------------------------------------------ the tool

    private static IEnumerator ChestTool(JsonElement a, CallContext c)
    {
        var action = S(a, "action", "list").Trim().ToLowerInvariant();
        if (action.Length == 0) action = "list";
        if (action == "all")
        {
            c.Result = ChestLedger.Report(Rt.Log.Player);
            yield break;
        }
        if (action != "list" && action != "store" && action != "take" && action != "stash" && action != "create")
        {
            c.InvalidArgs($"Unknown action \"{action}\". Use list, store, take, stash, create or all.", "list", "store", "take", "stash", "create", "all");
            yield break;
        }

        if (!TryParseChestMoves(a, c, stashDefaultsToAll: action == "stash", requireTargets: action is not ("list" or "create"), out var mv))
            yield break;

        if (action is "create" or "stash")
        {
            if (Game1.activeClickableMenu != null)
            {
                c.Fail("A menu is open. close_menu first.");
                yield break;
            }
            var chestSpot = new ChestSpot();
            yield return EnsureChest(a, c, chestSpot);
            if (!c.Ok) yield break;
            if (action == "create")
            {
                c.Result = chestSpot.Note.Length > 0 ? chestSpot.Note : $"Chest is at ({chestSpot.Tile.X},{chestSpot.Tile.Y}).";
                yield break;
            }
            var storeArgs = ArgsOf(new
            {
                x = chestSpot.Tile.X,
                y = chestSpot.Tile.Y,
                action = "store",
                item = mv.Everything ? "everything" : "",
                items = mv.Names,
                quality = mv.QualityText,
                quantity = mv.Quantity,
                reason = Reason(a)
            });
            var stored = new CallContext();
            yield return ChestTool(storeArgs, stored);
            c.Ok = stored.Ok;
            c.Result = stored.Result;
            if (chestSpot.Note.Length > 0) c.Result = chestSpot.Note + (c.Result.Length > 0 ? " " + c.Result : "");
            yield break;
        }

        if (action == "list")
        {
            var spot = "";
            var opened = false;
            if (Has(a, "x") && Has(a, "y"))
            {
                yield return OpenChestAt(a, c);
                if (!c.Ok) yield break;
                opened = true;
                spot = $" at ({I(a, "x", -1)},{I(a, "y", -1)})";
            }
            else if (Game1.activeClickableMenu is not ItemGrabMenu { context: Chest })
            {
                c.Fail("Give the chest's tile (x, y), or open a chest with interact first.");
                yield break;
            }
            var problems = new List<string>();
            c.Result = ChestListText((ItemGrabMenu)Game1.activeClickableMenu!, mv.Names, mv.Quality, spot, problems);
            if (opened && Game1.activeClickableMenu != null)
                yield return CloseMenu(default, new CallContext());
            yield break;
        }

        yield return ChestStoreTake(a, c, action, mv);
    }

    /// <summary>
    /// Walk to the chest at the x,y of <paramref name="a"/> and open it, the same path a player's hands take (interact,
    /// waiting out a chest another farmer has open for up to <see cref="ChestBusyWaitSeconds"/> s). On success a chest
    /// ItemGrabMenu is open; on failure c is failed and any non-chest menu that opened is closed again.
    /// </summary>
    private static IEnumerator OpenChestAt(JsonElement a, CallContext c)
    {
        if (Game1.activeClickableMenu != null)
        {
            c.Fail("A menu is open. close_menu first, or leave out x and y to use the chest menu that is open.");
            yield break;
        }
        var tile = new Point(I(a, "x", -1), I(a, "y", -1));
        // interact walks next to the tile and presses the action button; it is the same path a player's hands take.
        var sub = new CallContext();
        yield return Interact(ArgsOf(new { x = tile.X, y = tile.Y, reason = Reason(a) }), sub);
        if (!sub.Ok) { c.Fail(sub.Result); yield break; }
        for (int i = 0; i < 120 && Game1.activeClickableMenu == null; i++) yield return null;
        bool busy = false;
        if (Game1.activeClickableMenu == null && ChestAt(Loc, tile) is { } shared && shared.GetMutex().IsLocked())
        {
            // The game lets one farmer at a time open a chest: wait for the other to close it, then try again.
            // Only running ticks count: while an AI thinks the world is frozen and the other farmer can't close it.
            // Ported from Pichaya31847/stardew-ai-agent (MIT, commit 141b3b6; their days 1-34: 19 "Nothing opened"
            // while the other AI stood at the same chest). Theft is still the Ownership patch on the menu's grab.
            for (int running = 0; running < 60 * ChestBusyWaitSeconds && shared.GetMutex().IsLocked();)
            {
                if (!WorldClock.Frozen) running++;
                yield return null;
            }
            busy = shared.GetMutex().IsLocked();
            if (!busy)
            {
                yield return Interact(ArgsOf(new { x = tile.X, y = tile.Y, reason = Reason(a) }), new CallContext());
                for (int i = 0; i < 120 && Game1.activeClickableMenu == null; i++) yield return null;
            }
        }
        if (Game1.activeClickableMenu is not ItemGrabMenu { context: Chest })
        {
            var problem = busy
                ? $"The chest at ({tile.X},{tile.Y}) is open on another farmer's screen; waited {ChestBusyWaitSeconds} s and it is still in use. Try again later."
                : Game1.activeClickableMenu == null
                ? $"Nothing opened at ({tile.X},{tile.Y}). It is not a chest or the fridge, or another player is using it."
                : $"({tile.X},{tile.Y}) opened {MenuName(Game1.activeClickableMenu)}, which is not a chest. Use menu() for it.";
            if (Game1.activeClickableMenu != null) yield return CloseMenu(default, new CallContext());
            c.Fail(problem);
        }
    }

    /// <summary>
    /// The store/take body shared by the legacy chest tool and the semantic store_items / take_items: with x,y the chest
    /// is opened (and closed again afterwards); without, the chest menu already open is used. Sets <paramref name="c"/>;
    /// transfers run through ChestWork so the two catalogs cannot drift.
    /// </summary>
    private static IEnumerator ChestStoreTake(JsonElement a, CallContext c, string action, ChestMoves mv)
    {
        var spot = "";
        var opened = false;
        if (Has(a, "x") && Has(a, "y"))
        {
            yield return OpenChestAt(a, c);
            if (!c.Ok) yield break;
            opened = true;
            spot = $" at ({I(a, "x", -1)},{I(a, "y", -1)})";
        }
        else if (Game1.activeClickableMenu is not ItemGrabMenu { context: Chest })
        {
            c.Fail("Give the chest's tile (x, y), or open a chest with interact first (list_chest then reads it without walking).");
            yield break;
        }

        var used = ((ItemGrabMenu)Game1.activeClickableMenu!).context as Chest;
        yield return ChestWork(action, mv.Names, mv.Everything, mv.Quality, mv.Quantity, spot, c);
        if (used != null && c.Result.Length > 0)
            c.Result += ChestLedger.OthersChanged((used.Location ?? Loc).NameOrUniqueName, (int)used.TileLocation.X, (int)used.TileLocation.Y, Rt.Log.Player);
        if (opened && Game1.activeClickableMenu != null)
            yield return CloseMenu(default, new CallContext());
    }

    /// <summary>The chest menu's contents as one line: slots used, and either everything inside or the stacks matching
    /// <paramref name="names"/> / <paramref name="quality"/> (with what did not match in <paramref name="problems"/>).</summary>
    private static string ChestListText(ItemGrabMenu menu, List<string> names, int? quality, string spot, List<string> problems)
    {
        var label = ChestLabel(menu);
        var inside = menu.ItemsToGrabMenu.actualInventory;
        var shown = PlanMoves(inside, names, false, quality, null, null, false, problems).Select(j => j.item);
        bool filtered = names.Count > 0 || quality != null;
        int used = inside.Count(i => i != null);
        return $"{label}{spot}: {used}/{menu.ItemsToGrabMenu.capacity} slots used. {(filtered ? "Matching" : "Inside")}: {Stacks(shown)}." +
               (problems.Count > 0 ? " " + string.Join(" ", problems) : "");
    }

    /// <summary>The work with the chest menu open. Sets <paramref name="c"/>; the caller closes the menu.</summary>
    private static IEnumerator ChestWork(string action, List<string> names, bool everything, int? quality, int? quantity, string spot, CallContext c)
    {
        var menu = (ItemGrabMenu)Game1.activeClickableMenu!;
        var label = ChestLabel(menu);
        var problems = new List<string>();

        if (action == "list")
        {
            c.Result = ChestListText(menu, names, quality, spot, problems);
            yield break;
        }

        bool store = action == "store";
        if (store && menu.behaviorFunction == null) { c.Fail($"Nothing can be put into {label}."); yield break; }
        if (!store && !menu.showReceivingMenu) { c.Fail($"Nothing can be taken from {label}."); yield break; }

        IList<Item> source = menu.ItemsToGrabMenu.actualInventory;
        Func<Item, bool>? allowed = null;
        if (store)
        {
            source = P.Items;
            allowed = it => menu.inventory.highlightMethod(it);
        }
        var jobs = PlanMoves(source, names, everything, quality, quantity, allowed, store, problems);

        var moved = new List<(string label, int count)>();
        var left = new List<(string label, int count)>();
        string? firstFail = null;
        bool blocked = false;
        foreach (var (item, count) in jobs)
        {
            var itemLabel = StackLabel(item);
            if (blocked) { left.Add((itemLabel, count)); continue; }
            if (Game1.activeClickableMenu is not ItemGrabMenu cur || cur.context is not Chest)
            {
                problems.Add("The chest menu closed before everything was moved.");
                left.Add((itemLabel, count));
                blocked = true;
                continue;
            }
            // The game would only play a "cancel" sound; stop early and say why instead.
            if (!store && !P.couldInventoryAcceptThisItem(item.getOne()))
            {
                left.Add((itemLabel, count));
                blocked = true;
                continue;
            }

            string qid = item.QualifiedItemId;
            int q = item.Quality;
            int had = CountOf(P.Items, qid, q);
            // The menu tool's own take / put, picking the stack by name and quality.
            var args = ArgsOf(new { item = item.DisplayName, quality = QualityName(q), count });
            var sub = new CallContext();
            yield return store ? Put(cur, args, sub) : Take(cur, args, sub);
            int now = CountOf(P.Items, qid, q);
            int delta = store ? had - now : now - had;
            if (delta > 0) moved.Add((itemLabel, delta));
            if (delta < count)
            {
                left.Add((itemLabel, count - delta));
                if (!sub.Ok) firstFail ??= sub.Result.Split('\n')[0];
            }
        }
        yield return Ticks(5);

        var sb = new StringBuilder();
        if (moved.Count > 0)
            sb.Append(store ? $"Stored {Tally(moved)} in {label}{spot}." : $"Took {Tally(moved)} from {label}{spot}.");
        if (left.Count > 0)
            sb.Append($" Did not move ({(store ? "the chest is full or does not take it" : "your inventory is full")}): {Tally(left)}.");
        foreach (var p in problems) sb.Append(' ').Append(p);
        if (firstFail != null && left.Count > 0) sb.Append($" ({firstFail})");
        if (Game1.activeClickableMenu is ItemGrabMenu end && end.context is Chest)
            sb.Append($" {label} now {end.ItemsToGrabMenu.actualInventory.Count(i => i != null)}/{end.ItemsToGrabMenu.capacity} slots used.");

        if (moved.Count == 0) c.Fail(("Nothing " + (store ? "stored." : "taken.") + " " + sb).Trim());
        else c.Result = sb.ToString().Trim();
    }

    // ------------------------------------------------------------------ planning

    /// <summary>
    /// Which stacks of <paramref name="source"/> to move and how many of each. With <paramref name="everything"/> or no
    /// names that is every stack (minus the keepers when <paramref name="skipKeepers"/>); otherwise the stacks of each
    /// named item, limited by quality and by <paramref name="quantity"/> per name. What went wrong is added to <paramref name="problems"/>.
    /// </summary>
    private static List<(Item item, int count)> PlanMoves(IList<Item> source, List<string> names, bool everything, int? quality, int? quantity,
        Func<Item, bool>? allowed, bool skipKeepers, List<string> problems)
    {
        var jobs = new List<(Item item, int count)>();
        var live = source.Where(i => i != null).ToList();
        bool Fits(Item it) => quality == null || it.Quality == quality;

        if (everything || names.Count == 0)
        {
            foreach (var it in live)
            {
                if (!Fits(it) || (skipKeepers && KeepOutOfChest(it)) || (allowed != null && !allowed(it))) continue;
                jobs.Add((it, it.Stack));
            }
            return jobs;
        }

        foreach (var name in names)
        {
            var matches = MatchByName(live, name, problems);
            if (matches == null) continue;
            var fit = matches.Where(Fits).ToList();
            if (fit.Count == 0)
            {
                problems.Add(quality == null ? $"No \"{name}\" found." : $"No {QualityName(quality.Value)} \"{name}\" found.");
                continue;
            }
            int remaining = quantity ?? int.MaxValue;
            foreach (var it in fit)
            {
                if (remaining <= 0) break;
                if (jobs.Any(j => ReferenceEquals(j.item, it))) continue;
                if (allowed != null && !allowed(it)) { problems.Add($"{StackLabel(it)} can't go into this chest."); continue; }
                int n = Math.Min(it.Stack, remaining);
                jobs.Add((it, n));
                remaining -= n;
            }
        }
        return jobs;
    }

    /// <summary>Stacks called <paramref name="name"/>: exact name first, else a partial match that must be one item.</summary>
    private static List<Item>? MatchByName(List<Item> live, string name, List<string> problems)
    {
        var exact = live.Where(i => i.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase) || i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count > 0) return exact;
        var part = live.Where(i => i.DisplayName.Contains(name, StringComparison.OrdinalIgnoreCase) || i.Name.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();
        var kinds = part.Select(i => i.DisplayName).Distinct().ToList();
        if (kinds.Count > 1)
        {
            problems.Add($"\"{name}\" matches several items ({string.Join(", ", kinds.Take(6))}); name one exactly.");
            return null;
        }
        if (part.Count == 0)
        {
            problems.Add($"No \"{name}\" found.");
            return null;
        }
        return part;
    }
}
