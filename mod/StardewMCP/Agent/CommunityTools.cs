using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Quests;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;
using SObject = StardewValley.Object;

namespace StardewMCP.Agent;

// Community Center bundles and Museum donations (#13).
//
// The bundle page is driven through the game's own JunimoNoteMenu by clicks (the game deposits the item, pays the
// reward, plays the room-complete scene); success is read back from Game1.netWorldState.Value.Bundles, never from
// the UI. Members of JunimoNoteMenu, Bundle and LibraryMuseum that this code could not check against the decompiled
// game are reached through LateBound: a missing member becomes a clear "tool failed" line instead of a crash.
// BundleReader, BundleLedger and LateBound are written to be reused by the Joja alternative (#26).

/// <summary>A game member this mod looks up by name at run time was not there (the game version differs).</summary>
internal sealed class LateBoundException : Exception
{
    public LateBoundException(string message) : base(message) { }
}

/// <summary>Reads, writes and calls game members by name, for the few ones that could not be checked at compile time.</summary>
internal static class LateBound
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>Read a field or property by name (any visibility, base classes included); false when there is none.</summary>
    public static bool TryRead(object? target, Type type, string name, out object? value)
    {
        value = null;
        for (var t = type; t != null; t = t.BaseType)
        {
            var f = t.GetField(name, Any);
            if (f != null && (f.IsStatic || target != null))
            {
                value = f.GetValue(f.IsStatic ? null : target);
                return true;
            }
            var p = t.GetProperty(name, Any);
            if (p != null && p.GetIndexParameters().Length == 0 && p.GetMethod is { } getter && (getter.IsStatic || target != null))
            {
                value = getter.Invoke(getter.IsStatic ? null : target, null);
                return true;
            }
        }
        return false;
    }

    /// <summary>The member's value, or null when it is missing or null.</summary>
    public static object? TryGet(object? target, string name) =>
        target != null && TryRead(target, target.GetType(), name, out var v) ? v : null;

    public static object? TryGetStatic(Type type, string name) =>
        TryRead(null, type, name, out var v) ? v : null;

    /// <summary>The member's value (may be null); throws <see cref="LateBoundException"/> when the member does not exist.</summary>
    public static object? Require(object target, string name)
    {
        if (!TryRead(target, target.GetType(), name, out var v))
            throw new LateBoundException($"{target.GetType().Name} has no member \"{name}\" (the game version may differ from the one this mod was written for)");
        return v;
    }

    /// <summary>Write a field or property by name; false when there is none or it can't be written.</summary>
    public static bool TrySet(object? target, string name, object? value)
    {
        if (target == null) return false;
        for (var t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, Any);
            if (f != null && !f.IsStatic && !f.IsInitOnly)
            {
                f.SetValue(target, value);
                return true;
            }
            var p = t.GetProperty(name, Any);
            if (p != null && p.GetIndexParameters().Length == 0 && p.SetMethod is { IsStatic: false } setter)
            {
                setter.Invoke(target, new[] { value });
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Call a method by name (any visibility, static or instance, ignoring case; missing optional arguments are filled in).
    /// False when no overload takes these arguments; a failure inside the game method is rethrown as <see cref="LateBoundException"/>.
    /// </summary>
    public static bool TryCall(object? target, Type type, string name, out object? result, params object?[] args)
    {
        result = null;
        for (var t = type; t != null; t = t.BaseType)
        {
            foreach (var m in t.GetMethods(Any))
            {
                if (!m.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || m.IsGenericMethodDefinition) continue;
                if (!m.IsStatic && target == null) continue;
                var ps = m.GetParameters();
                if (args.Length > ps.Length) continue;

                var full = new object?[ps.Length];
                bool fits = true;
                for (int i = 0; i < ps.Length && fits; i++)
                {
                    var pt = ps[i].ParameterType;
                    if (pt.IsByRef) { fits = false; break; }
                    if (i < args.Length)
                    {
                        fits = args[i] == null ? !pt.IsValueType || Nullable.GetUnderlyingType(pt) != null : pt.IsInstanceOfType(args[i]);
                        full[i] = args[i];
                    }
                    else if (ps[i].HasDefaultValue)
                        full[i] = ps[i].DefaultValue;
                    else
                        fits = false;
                }
                if (!fits) continue;

                try
                {
                    result = m.Invoke(m.IsStatic ? null : target, full);
                    return true;
                }
                catch (TargetInvocationException ex)
                {
                    throw new LateBoundException($"{type.Name}.{m.Name} failed: {(ex.InnerException ?? ex).Message}");
                }
            }
        }
        return false;
    }

    public static object? Call(object target, string name, params object?[] args)
    {
        if (TryCall(target, target.GetType(), name, out var r, args)) return r;
        throw new LateBoundException($"{target.GetType().Name} has no method \"{name}\" taking {args.Length} argument(s) of these types (the game version may differ from the one this mod was written for)");
    }
}

/// <summary>
/// Reads the Community Center bundles from the world state and describes them. Shared with the Joja alternative (#26),
/// which closes the same bundles: <see cref="ClosedReason"/> tells the two routes apart.
/// </summary>
internal static class BundleReader
{
    /// <summary>The six rooms in the order the game numbers their areas (0-5).</summary>
    public static string[] Rooms => BundlePlan.Rooms;

    private static readonly Dictionary<int, string> Categories = new()
    {
        [-2] = "any Gem", [-4] = "any Fish", [-5] = "any Egg", [-6] = "any Milk", [-7] = "any Cooked dish", [-12] = "any Mineral",
        [-14] = "any Meat", [-15] = "any Metal", [-16] = "any Building material", [-18] = "any Animal product",
        [-19] = "any Fertilizer", [-20] = "any Junk", [-21] = "any Bait", [-22] = "any Tackle", [-25] = "any Ingredient",
        [-26] = "any Artisan good", [-27] = "any Syrup", [-28] = "any Monster loot", [-74] = "any Seed", [-75] = "any Vegetable",
        [-79] = "any Fruit", [-80] = "any Flower", [-81] = "any Forage"
    };

    /// <summary>The player's own mail flag, or the host's (world-wide unlocks such as the Joja membership live on the host).</summary>
    public static bool HasMail(Farmer who, string id) =>
        who.mailReceived.Contains(id) || Game1.MasterPlayer.mailReceived.Contains(id);

    /// <summary>The membership is bought (#26): received, or already queued for tomorrow's mail on the day it was bought.</summary>
    public static bool IsJoja(Farmer who) => HasMail(who, "JojaMember") || JojaRoute.Pending(who, "JojaMember");

    /// <summary>Why the bundles can't be used right now, or null when they can.</summary>
    public static string? ClosedReason(Farmer who)
    {
        if (IsJoja(who))
            return "This farm took a Joja membership, so the Community Center bundles are closed for good.";
        if (Game1.currentLocation is CommunityCenter || HasMail(who, "ccDoorUnlock") || HasMail(who, "canReadJunimoText") || HasMail(who, "ccIsComplete"))
            return null;
        return "The Community Center is not open yet: its door has not been unlocked (the Community Center scene), and the Wizard teaches you to read the Junimo notes.";
    }

    /// <summary>Every bundle, rooms in game order. Empty when the world has no bundle data.</summary>
    public static List<BundleInfo> Read()
    {
        var list = new List<BundleInfo>();
        foreach (var kv in Game1.netWorldState.Value.BundleData)
            if (BundlePlan.Parse(kv.Key, kv.Value, Flags, id => ItemRegistry.QualifyItemId(id) ?? "(O)" + id) is { } info)
                list.Add(info);
        return list.OrderBy(b => b.Area < 0 ? 99 : b.Area).ThenBy(b => b.Index).ToList();
    }

    /// <summary>The donated flags of one bundle (one per ingredient); empty when the bundle is unknown.</summary>
    public static bool[] Flags(int index) =>
        Game1.netWorldState.Value.Bundles.TryGetValue(index, out var f) && f != null ? f : Array.Empty<bool>();

    /// <summary>True when this item can fill the ingredient (item or category, quality at least the asked one). The stack size is checked separately.</summary>
    public static bool Fits(Item item, BundleNeed need) => BundlePlan.Fits(need, item.QualifiedItemId, item.Category, item.Quality);

    public static bool BundleMatches(BundleInfo b, string want)
    {
        if (want.Length == 0) return true;
        if (int.TryParse(want, out var n) && n == b.Index) return true;
        return b.Name.Equals(want, StringComparison.OrdinalIgnoreCase) || b.Name.Contains(want, StringComparison.OrdinalIgnoreCase);
    }

    public static bool RoomMatches(string room, string want) =>
        room.Equals(want, StringComparison.OrdinalIgnoreCase) || room.Contains(want, StringComparison.OrdinalIgnoreCase);

    public static string ItemName(string id)
    {
        if (id == "-1") return "Gold";
        if (int.TryParse(id, out var n) && n < -1) return Categories.TryGetValue(n, out var cat) ? cat : $"category {n}";
        return ItemRegistry.GetData(ItemRegistry.QualifyItemId(id) ?? "(O)" + id)?.DisplayName ?? id;
    }

    public static string Describe(BundleNeed n)
    {
        if (n.Id == "-1") return $"{n.Count}g";
        var quality = n.Quality switch { 1 => " (silver or better)", 2 => " (gold or better)", 4 => " (iridium)", _ => "" };
        return $"{(n.Count > 1 ? n.Count + " x " : "")}{ItemName(n.Id)}{quality}";
    }

    /// <summary>"O 465 20" in bundle data: type letter, item id, count.</summary>
    public static string RewardText(string reward)
    {
        var p = reward.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (p.Length < 2) return reward;
        string? prefix = p[0] switch { "O" or "R" => "(O)", "BO" => "(BC)", "F" => "(F)", "W" => "(W)", "H" => "(H)", "B" => "(B)", _ => null };
        var count = p.Length > 2 && int.TryParse(p[2], out var n) && n > 1 ? n + " x " : "";
        var name = prefix == null ? null : ItemRegistry.GetData(prefix + p[1])?.DisplayName;
        return count + (name ?? reward);
    }

    /// <summary>What finishing a room gives: the Joja project of the same room, the Bulletin Board's friendship, the Joja Mart's theater; null when unknown.</summary>
    public static string? RoomUnlock(string room) => room switch
    {
        "Bulletin Board" => "+2 hearts with every villager you can't marry",
        "Abandoned Joja Mart" => "the movie theater",
        _ => JojaRoute.Projects.FirstOrDefault(p => p.Room == room)?.Gives
    };

    /// <summary>Bundle needs may be shown: the Community Center is open, or GuideKnowledge previews it. Never on the Joja route.</summary>
    public static bool NeedsVisible(Farmer who) => !IsJoja(who) && (ClosedReason(who) == null || Knowledge.Omniscient);

    /// <summary>An unfinished bundle whose slots count as needs: the six rooms, and the Joja Mart's Missing Bundle once the rooms are done.</summary>
    public static bool Wanted(BundleInfo b, Farmer who) => !b.Complete && (b.Area >= 0 || HasMail(who, "ccIsComplete"));

    /// <summary>Open slots of the wanted bundles, gold left out.</summary>
    public static List<(BundleInfo bundle, BundleNeed need)> OpenItemSlots(IEnumerable<BundleInfo> all, Farmer who) =>
        all.Where(b => Wanted(b, who)).SelectMany(b => b.Open.Where(n => !n.IsGold).Select(n => (b, n))).ToList();

    /// <summary>"have 1 pack + 3 Farm chest (64,15)" for an item slot, "you can pay it" / "you have 900g" for gold.</summary>
    private static (string text, bool enough) Have(Farmer who, BundleNeed n, List<(Item item, string where)> stored)
    {
        if (n.IsGold) return who.Money >= n.Count ? ("you can pay it", true) : ($"you have {who.Money}g", false);
        int pack = who.Items.Where(i => i != null && Fits(i, n)).Sum(i => i.Stack);
        var places = stored.Where(s => Fits(s.item, n)).GroupBy(s => s.where).Select(g => (g.Key, g.Sum(s => s.item.Stack))).ToList();
        return (BundlePlan.HaveText(n.Count, pack, places), pack + places.Sum(p => p.Item2) >= n.Count);
    }

    /// <summary>The first source lines of the item and, when all of them are seasonal, how long its season lasts; "" for gold and categories.</summary>
    private static string Sources(BundleNeed n)
    {
        if (n.ItemId == null) return "";
        var all = KnowledgeSources.For(n.ItemId);
        if (all.Count == 0) return "";
        var text = " | " + string.Join("; ", all.Take(2));
        var season = BundlePlan.SeasonNote(all, Game1.currentSeason, Game1.dayOfMonth);
        return season.Length > 0 ? text + " | " + season : text;
    }

    /// <summary>
    /// The text of the bundles tool: rooms (with what each unlocks), bundles, what is donated (and by whom, when this mod saw it)
    /// and each missing slot with what you have of it (pack and chests) and where it comes from.
    /// </summary>
    public static string Report(IReadOnlyList<BundleInfo> all, Farmer who, string? room, bool missingOnly = false, bool preview = false)
    {
        var sb = new StringBuilder();
        if (preview) sb.AppendLine("(Community Center not open yet: preview)");
        sb.AppendLine($"Community Center: {all.Count(b => b.Complete)}/{all.Count} bundles complete." +
                      (preview ? "" : " Donate with donate_bundle(item) while standing inside the Community Center."));
        if (!preview && !HasMail(who, "canReadJunimoText"))
            sb.AppendLine("(You can't read Junimo script yet: the Wizard teaches it. The data below is exact anyway.)");
        var stored = ChestLedger.StoredItems();
        foreach (var g in all.GroupBy(b => b.Room))
        {
            if (room != null && !RoomMatches(g.Key, room)) continue;
            bool roomDone = g.All(b => b.Complete);
            if (missingOnly && roomDone) continue;
            var unlock = RoomUnlock(g.Key) is { } u ? (roomDone ? $" (gave {u})" : $" → unlocks {u}") : "";
            sb.AppendLine($"== {g.Key}: {g.Count(b => b.Complete)}/{g.Count()} bundles complete{unlock}");
            foreach (var b in g)
            {
                if (b.Complete)
                {
                    if (!missingOnly) sb.AppendLine($"  [{b.Index}] {b.Name} COMPLETE → {RewardText(b.Reward)}");
                    continue;
                }
                var done = b.Needs.Where(n => n.Done)
                    .Select(n => Describe(n) + (BundleLedger.Who(b.Index, n.Slot) is { } w ? $" (by {w})" : ""));
                var open = b.Open.ToList();
                var lead = b.Remaining < open.Count ? $"needs {b.Remaining} more of:" : "needs:";
                var doneText = !missingOnly && done.Any() ? " | done: " + string.Join(", ", done) : "";
                sb.AppendLine($"  [{b.Index}] {b.Name} {b.Filled}/{b.Required} → {RewardText(b.Reward)}{doneText} | {lead}");
                foreach (var n in open)
                {
                    var (have, enough) = Have(who, n, stored);
                    sb.AppendLine($"    - {Describe(n)}: {have}{(enough ? "" : Sources(n))}");
                }
            }
        }
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Who donated what. The game keeps only "slot filled" flags, so this remembers the donations made through
/// donate_bundle in memory (lost on restart); a donation made by hand is not listed.
/// </summary>
internal static class BundleLedger
{
    private static readonly object Gate = new();
    private static readonly Dictionary<(int bundle, int slot), (string who, int day)> Donors = new();

    public static void Record(int bundle, int slot, string who, int day)
    {
        lock (Gate) Donors[(bundle, slot)] = (who, day);
    }

    public static string? Who(int bundle, int slot)
    {
        lock (Gate) return Donors.TryGetValue((bundle, slot), out var d) ? $"{d.who} day {d.day}" : null;
    }
}

public static partial class AgentTools
{
    private static List<ToolDef> CommunityTools() => new()
    {
        new()
        {
            Name = "bundles",
            Description = "Community Center bundles, room by room: what each room unlocks, what each bundle needs (item, quality, count), what is already donated and by whom, " +
                          "the reward, and for each missing item how many you have (pack and every chest and fridge on the farm) and where it comes from. " +
                          (Knowledge.Omniscient
                              ? "Before the Community Center opens it shows a preview of what the bundles will need. "
                              : "Needs the Community Center to be open (the Wizard teaches you to read the notes). ") +
                          "action keep: ship or keep? for every item in your pack, whether a bundle, the Museum or a quest still wants it and its best machine, ending with what is safe to ship. " +
                          "Game time stays frozen.",
            Schema = Obj(new
            {
                room = Str("Optional: only this room (Pantry, Crafts Room, Fish Tank, Boiler Room, Vault, Bulletin Board)"),
                missing_only = new { type = "boolean", description = "Only unfinished bundles and their missing items (default false)" },
                action = new { type = "string", @enum = new[] { "list", "keep" }, description = "list (default): the bundles; keep: ship-or-keep check of your pack" }
            }),
            Instant = true,
            Run = (a, c) => CommunityGuard(BundlesRun(a, c), c)
        },
        new()
        {
            Name = "donate_bundle",
            Description = "Donate an item from your inventory to the first Community Center bundle that needs it. You must be inside the Community Center (travel_to CommunityCenter). " +
                          "It uses the game's own bundle page, so rewards and room-complete scenes happen as for a player. " +
                          "item \"gold\" pays a Vault bundle. bundle (name or number from bundles) picks the bundle if several take the item. " +
                          "Reports the bundle's progress and any bundle or room it completes.",
            Schema = Obj(new
            {
                item = Str("Item name from your inventory, or \"gold\" for a Vault bundle"),
                bundle = Str("Optional: bundle name or number (see bundles)"),
                reason = Str(ReasonDesc)
            }, "item", "reason"),
            Run = (a, c) => CommunityGuard(DonateBundleRun(a, c), c)
        },
        new()
        {
            Name = "museum",
            Description = "The Museum (ArchaeologyHouse, Gunther). action list (default): the items in your inventory that the Museum still accepts. " +
                          "donate: put an item (or \"all\") on the next free display spot; you must be inside the Museum (travel_to ArchaeologyHouse). " +
                          "rewards: talk to Gunther and take the rewards you have earned. list keeps game time frozen.",
            Schema = Obj(new
            {
                action = new { type = "string", @enum = new[] { "list", "donate", "rewards" }, description = "list (default), donate or rewards" },
                item = Str("donate: item name from your inventory, or \"all\""),
                reason = Str(ReasonDesc)
            }),
            InstantFor = a => S(a, "action").Trim().ToLowerInvariant() is "" or "list",
            Legacy = true,
            Run = (a, c) => CommunityGuard(MuseumRun(a, c), c)
        }
    };

    /// <summary>Runs a tool body and turns a missing game member (<see cref="LateBoundException"/>) into a plain tool failure.</summary>
    private static IEnumerator CommunityGuard(IEnumerator inner, CallContext c)
    {
        try
        {
            while (true)
            {
                bool more;
                try { more = inner.MoveNext(); }
                catch (LateBoundException ex) { c.Fail(ex.Message); more = false; }
                if (!more) yield break;
                yield return inner.Current;
            }
        }
        finally
        {
            (inner as IDisposable)?.Dispose();
        }
    }

    // ------------------------------------------------------------------ bundles

    private static IEnumerator BundlesRun(JsonElement a, CallContext c)
    {
        var action = S(a, "action").Trim().ToLowerInvariant();
        if (action is not ("" or "list" or "keep")) { c.InvalidArgs($"Unknown action \"{action}\". Use list or keep.", "list", "keep"); yield break; }
        try
        {
            if (action == "keep")
            {
                c.Result = KeepText(P);
                yield break;
            }
            var closed = BundleReader.ClosedReason(P);
            bool preview = closed != null;
            if (preview && !BundleReader.NeedsVisible(P)) { c.Fail(closed!); yield break; }
            var all = BundleReader.Read();
            if (all.Count == 0) { c.Fail("The game has no bundle data yet."); yield break; }
            var room = S(a, "room").Trim();
            if (room.Length > 0 && !all.Any(b => BundleReader.RoomMatches(b.Room, room)))
            {
                c.Fail($"No room \"{room}\". Rooms: {string.Join(", ", all.Select(b => b.Room).Distinct())}");
                yield break;
            }
            c.Result = BundleReader.Report(all, P, room.Length > 0 ? room : null, Observation.Flag(a, "missing_only"), preview);
        }
        catch (Exception ex) when (ex is not LateBoundException)
        {
            c.Fail($"bundles could not read the game data ({ex.GetType().Name}: {ex.Message}). Tell the player; the game data may differ from what the mod expects.");
        }
    }

    /// <summary>
    /// bundles(action=keep): for every object in the pack, the reasons to keep it (open bundle slots the chests do not cover,
    /// a Museum piece not donated yet, an item quest or special order) and its best machine, then what is safe to ship.
    /// </summary>
    private static string KeepText(Farmer who)
    {
        var header = new List<string> { "Ship or keep? (your pack; your chests fill bundle slots first)" };
        var slots = new List<(BundleInfo bundle, BundleNeed need)>();
        if (BundleReader.IsJoja(who)) header.Add("(Joja route: no bundle needs.)");
        else if (!BundleReader.NeedsVisible(who)) header.Add("(Community Center not open yet: bundle needs unknown.)");
        else
        {
            if (BundleReader.ClosedReason(who) != null) header.Add("(Community Center not open yet: bundle needs are a preview)");
            slots = BundleReader.OpenItemSlots(BundleReader.Read(), who);
        }

        var pack = who.Items.Where(i => i is SObject { bigCraftable.Value: false } and not Furniture).ToList();
        if (pack.Count == 0) return BundlePlan.KeepReport(header, Array.Empty<KeepRow>());

        // Bundle slots take chest stacks first, so the pack keeps only what the chests do not cover.
        var stored = slots.Count == 0
            ? new List<Item>()
            : ChestLedger.StoredItems().Select(s => s.item).Where(i => slots.Any(x => BundleReader.Fits(i, x.need))).ToList();
        var items = stored.Concat(pack).ToList();
        var stacks = items.Select((it, i) => new KeepStack(it.Quality, it.Stack, i >= stored.Count)).ToList();
        // A bundle that takes N of M holds items back only for the N slots it still needs, the ones the chests cover first.
        var open = slots;
        slots = BundlePlan.PickSlots(open, stacks, (s, n) => BundleReader.Fits(items[s], open[n].need)).Select(i => open[i]).ToList();
        var needs = slots.Select(x => x.need).ToList();
        var alloc = BundlePlan.Allocate(needs, stacks, (s, n) => BundleReader.Fits(items[s], needs[n]));

        var quests = QuestWants(who);
        var questLeft = quests.ToDictionary(q => q.key, q => q.count);
        var museumSeen = new HashSet<string>();
        var rows = new List<KeepRow>();
        for (int p = 0; p < pack.Count; p++)
        {
            var it = pack[p];
            var tags = new List<string>();
            int keep = 0;
            foreach (var (n, amount) in alloc[stored.Count + p])
            {
                var (b, need) = slots[n];
                var quality = need.Quality > 0 ? ", " + BundlePlan.QualityWords(need.Quality) : "";
                tags.Add($"bundle {b.Name} ({b.Room}) {amount}{(amount < need.Count ? $" of {need.Count}" : "")}{quality}");
                keep += amount;
            }
            if (museumSeen.Add(it.QualifiedItemId) && LibraryMuseum.IsItemSuitableForDonation(it.QualifiedItemId))
            {
                tags.Add("museum-new");
                keep += 1;
            }
            foreach (var q in quests.Where(q => q.wants(it)))
            {
                int take = Math.Min(questLeft[q.key], Math.Max(0, it.Stack - keep));
                if (take <= 0) continue;
                questLeft[q.key] -= take;
                tags.Add($"{q.name} {take}");
                keep += take;
            }
            var route = MachineTools.BestOutputs(it.QualifiedItemId, 1).FirstOrDefault();
            var label = it.Quality == 0 ? it.DisplayName : $"{it.DisplayName} ({QualityName(it.Quality)})";
            rows.Add(new KeepRow(label, it.Stack, tags, keep, it.canBeShipped(), route, BundlePlan.IsSupply(it.Category, (it as SObject)?.Type)));
        }
        return BundlePlan.KeepReport(header, rows);
    }

    /// <summary>
    /// What the open item quests (ItemDeliveryQuest) and special orders (deliver and donate objectives) still want: a key, a
    /// short name ("quest Robin's Request", "order Gus's Crop Order"), the count still missing and an item test.
    /// </summary>
    private static List<(object key, string name, int count, Func<Item, bool> wants)> QuestWants(Farmer who)
    {
        var list = new List<(object key, string name, int count, Func<Item, bool> wants)>();
        foreach (var q in who.questLog)
            if (q is ItemDeliveryQuest d && !d.completed.Value && !string.IsNullOrEmpty(d.ItemId.Value))
                list.Add((d, $"quest {d.GetName()}", Math.Max(1, d.number.Value), i => i.QualifiedItemId == d.ItemId.Value));
        foreach (var order in who.team.specialOrders)
        {
            if (order.questState.Value != SpecialOrderStatus.InProgress) continue;
            foreach (var o in order.objectives)
            {
                if (o.IsComplete()) continue;
                int left = Math.Max(1, o.GetMaxCount() - o.GetCount());
                if (o is DeliverObjective deliver && deliver.acceptableContextTagSets.Count > 0)
                    list.Add((o, $"order {order.GetName()}", left, i => MatchesTagSets(deliver.acceptableContextTagSets, i)));
                else if (o is DonateObjective donate)
                    list.Add((o, $"order {order.GetName()}", left, donate.IsValidItem));
            }
        }
        return list;
    }

    /// <summary>DeliverObjective's item test: some tag set whose every comma part has a matching tag ("a/b" = either).</summary>
    private static bool MatchesTagSets(IEnumerable<string> sets, Item item)
    {
        var tags = item.GetContextTags();
        return sets.Any(set => set.Split(',').All(part => ItemContextTagManager.DoAnyTagsMatch(part.Split('/'), tags)));
    }

    private sealed record DonationPick(BundleInfo Bundle, BundleNeed Need, int InvSlot);

    /// <summary>Inventory slots holding an item called <paramref name="name"/>: exact name matches, else partial ones.</summary>
    private static List<int> StacksNamed(string name)
    {
        name = name.Trim().ToLowerInvariant();
        var exact = new List<int>();
        var loose = new List<int>();
        for (int i = 0; i < P.Items.Count; i++)
        {
            var it = P.Items[i];
            if (it == null) continue;
            var dn = it.DisplayName.ToLowerInvariant();
            var n = it.Name.ToLowerInvariant();
            if (dn == name || n == name) exact.Add(i);
            else if (dn.Contains(name) || n.Contains(name)) loose.Add(i);
        }
        return exact.Count > 0 ? exact : loose;
    }

    /// <summary>The first open bundle slot (rooms in game order) that the named item, or gold, can fill. Lower-quality stacks go first.</summary>
    private static (DonationPick? pick, string? error) PickDonation(List<BundleInfo> all, string want, bool gold, string bundleArg)
    {
        var open = all.Where(b => b.Area >= 0 && !b.Complete && BundleReader.BundleMatches(b, bundleArg)).ToList();
        if (open.Count == 0)
            return (null, bundleArg.Length > 0 ? $"No unfinished bundle matches \"{bundleArg}\". Use bundles to see them." : "Every bundle in the Community Center is already complete.");

        var stacks = gold ? new List<int>() : StacksNamed(want);
        if (!gold && stacks.Count == 0)
            return (null, $"No \"{want}\" in your inventory.");

        foreach (var b in open)
        {
            var missing = b.Needs.Where(x => !x.Done).ToList();
            foreach (var n in missing)
            {
                if (gold)
                {
                    if (n.Id == "-1") return (new DonationPick(b, n, -1), null);
                    continue;
                }
                foreach (var slot in stacks.OrderBy(s => P.Items[s].Quality))
                {
                    var stack = P.Items[slot];
                    if (BundleReader.Fits(stack, n) && stack.Stack >= n.Count) return (new DonationPick(b, n, slot), null);
                }
            }
        }

        if (gold) return (null, "No unfinished bundle takes gold.");
        return (null, $"\"{want}\" fits no open bundle slot: either no bundle needs it, its quality is too low, or you hold fewer than the bundle asks. Use bundles to see what is needed.");
    }

    private static IEnumerator DonateBundleRun(JsonElement a, CallContext c)
    {
        var closed = BundleReader.ClosedReason(P);
        if (closed != null) { c.Fail(closed); yield break; }
        if (Loc is not CommunityCenter)
        {
            c.Fail($"Bundles are donated inside the Community Center; you are in {Loc.Name}. Use travel_to CommunityCenter first.");
            yield break;
        }
        if (Game1.activeClickableMenu != null || Game1.eventUp)
        {
            c.Fail("Close the open menu or finish the scene first.");
            yield break;
        }

        var all = BundleReader.Read();
        if (all.Count == 0) { c.Fail("The game has no bundle data yet."); yield break; }
        var want = S(a, "item").Trim();
        if (want.Length == 0) { c.Fail("item is required: an inventory item name, or \"gold\"."); yield break; }
        bool gold = want.Equals("gold", StringComparison.OrdinalIgnoreCase) || want.Equals("money", StringComparison.OrdinalIgnoreCase);

        var (pick, error) = PickDonation(all, want, gold, S(a, "bundle").Trim());
        if (pick == null) { c.Fail(error!); yield break; }
        var b = pick.Bundle;
        var need = pick.Need;
        if (gold && P.Money < need.Count) { c.Fail($"{b.Name} costs {need.Count}g and you have {P.Money}g."); yield break; }

        var itemName = gold ? "gold" : P.Items[pick.InvSlot].DisplayName;
        int doneBefore = BundleReader.Flags(b.Index).Count(x => x);
        var before = Snapshot();
        bool success = false;
        bool rewardTaken = false;

        var menu = OpenJunimoMenu(b.Area);
        Game1.activeClickableMenu = menu;
        try
        {
            // The menu ignores clicks while its screen swipe plays.
            for (int i = 0; i < 180 && LateBound.TryGetStatic(typeof(JunimoNoteMenu), "canClick") is false; i++) yield return null;
            yield return Ticks(6);

            Click(menu, FindBundleComponent(menu, b.Index));
            for (int i = 0; i < 90 && LateBound.TryGet(menu, "specificBundlePage") is not true; i++) yield return null;
            if (LateBound.TryGet(menu, "specificBundlePage") is not true)
            {
                c.Fail($"The {b.Name} bundle page did not open.");
                yield break;
            }
            yield return Ticks(6);

            if (gold)
            {
                if (LateBound.TryGet(menu, "purchaseButton") is not ClickableComponent buy)
                {
                    c.Fail($"The {b.Name} page has no purchase button.");
                    yield break;
                }
                Click(menu, buy);
                for (int i = 0; i < 30 && !success; i++)
                {
                    yield return null;
                    success = BundleReader.Flags(b.Index).Count(x => x) > doneBefore;
                }
            }
            else
            {
                var invMenu = LateBound.TryGet(menu, "inventory") as InventoryMenu;
                var slotCc = invMenu?.inventory.FirstOrDefault(s => s.name == pick.InvSlot.ToString());
                if (slotCc == null)
                {
                    c.Fail($"Your {itemName} (slot {pick.InvSlot}) is not shown on the bundle page.");
                    yield break;
                }
                Click(menu, slotCc); // pick the stack up
                yield return Ticks(3);
                if (LateBound.TryGet(menu, "heldItem") == null)
                {
                    c.Fail($"Could not pick up {itemName}.");
                    yield break;
                }

                // Empty slots first; the game matches the item to its ingredient itself, so any free slot works.
                var slots = IngredientSlots(menu).OrderBy(s => LateBound.TryGet(s, "item") == null ? 0 : 1).ToList();
                if (slots.Count == 0)
                {
                    c.Fail($"The {b.Name} page shows no ingredient slots.");
                    yield break;
                }
                foreach (var slot in slots)
                {
                    Click(menu, slot);
                    for (int i = 0; i < 12 && !success; i++)
                    {
                        yield return null;
                        success = BundleReader.Flags(b.Index).Count(x => x) > doneBefore;
                    }
                    if (success || LateBound.TryGet(menu, "heldItem") == null) break;
                }
            }

            if (success && BundleReader.Flags(b.Index).Count(x => x) >= b.Required)
            {
                // The game plays the completion animation, then returns to the room's page.
                for (int i = 0; i < 600 && LateBound.TryGet(menu, "specificBundlePage") is true; i++) yield return null;
                yield return Ticks(40);
                if (Game1.activeClickableMenu == menu && LateBound.TryGet(menu, "presentButton") is ClickableComponent gift && gift.visible)
                {
                    // Best effort: the gift icon on the room's page opens the reward grab menu.
                    Click(menu, gift);
                    for (int i = 0; i < 60 && Game1.activeClickableMenu is not ItemGrabMenu; i++) yield return null;
                    if (Game1.activeClickableMenu is ItemGrabMenu)
                    {
                        var rewardBefore = Snapshot();
                        yield return CommunityTakeAll();
                        rewardTaken = Diff(rewardBefore).Length > 0;
                    }
                }
            }
        }
        finally
        {
            ReturnJunimoHeldItem(menu);
            if (Game1.activeClickableMenu == menu)
            {
                menu.exitThisMenu();
                if (Game1.activeClickableMenu == menu) Game1.exitActiveMenu();
            }
            else if (Game1.activeClickableMenu is ItemGrabMenu rewards && rewards.ItemsToGrabMenu.actualInventory.All(i => i == null))
                Game1.exitActiveMenu();
        }
        if (!c.Ok) yield break;
        yield return Ticks(10);

        var after = BundleReader.Read();
        var now = after.FirstOrDefault(x => x.Index == b.Index) ?? b;
        if (!success)
        {
            var diff = Diff(before);
            c.Fail($"Nothing was donated to {b.Name}: the game did not take {itemName}. {diff}".Trim());
            yield break;
        }

        var filled = now.Needs.Where(n => n.Done && !b.Needs[n.Slot].Done).ToList();
        foreach (var n in filled) BundleLedger.Record(b.Index, n.Slot, P.Name, AgentLog.FarmDay);
        var sb = new StringBuilder();
        sb.Append(gold ? $"Paid {need.Count}g into {b.Name} ({b.Room})" : $"Donated {itemName} to {b.Name} ({b.Room})");
        sb.Append($": {now.Filled}/{now.Required} filled.");
        Rt.Log.Add("bundle_donated", text: $"{P.Name} donated {itemName} to {b.Name}",
            args: new { player = Rt.Log.Player, bundle = b.Name, room = b.Room, item = itemName, filled = now.Filled, required = now.Required });

        if (now.Complete)
        {
            var reward = BundleReader.RewardText(b.Reward);
            sb.Append($" Bundle complete! Reward: {reward}{(rewardTaken ? " (in your inventory now)" : "; if it is not in your inventory, open the Junimo note in this room and click the gift")}.");
            Rt.Log.Add("bundle_completed", text: $"{P.Name} completed the {b.Name} bundle",
                args: new { player = Rt.Log.Player, bundle = b.Name, room = b.Room, reward });
            var roomBundles = after.Where(x => x.Room == b.Room).ToList();
            if (roomBundles.All(x => x.Complete))
            {
                sb.Append($" The {b.Room} is complete!");
                Rt.Log.Add("room_completed", text: $"{P.Name} completed the {b.Room}", args: new { player = Rt.Log.Player, room = b.Room });
            }
            if (after.All(x => x.Complete)) sb.Append(" The whole Community Center is complete!");
        }
        else
        {
            var left = now.Needs.Where(n => !n.Done).Select(BundleReader.Describe).ToList();
            int remaining = now.Required - now.Filled;
            sb.Append(left.Count == 0 ? "" : $" Still needs {(remaining < left.Count ? $"{remaining} more of" : "")}: {string.Join(", ", left)}.");
        }
        var inv = Diff(before);
        if (inv.Length > 0) sb.Append(' ').Append(inv);
        c.Result = sb.ToString();
    }

    /// <summary>The bundle page for one room, opened as the game does when a player uses the room's Junimo note.</summary>
    private static IClickableMenu OpenJunimoMenu(int area)
    {
        var dict = new Dictionary<int, bool[]>();
        foreach (var kv in Game1.netWorldState.Value.Bundles.Pairs)
            dict[kv.Key] = kv.Value;
        object? menu;
        try
        {
            menu = Activator.CreateInstance(typeof(JunimoNoteMenu), BindingFlags.Public | BindingFlags.Instance | BindingFlags.OptionalParamBinding,
                null, new object[] { area, dict }, null);
        }
        catch (MissingMethodException)
        {
            throw new LateBoundException("JunimoNoteMenu has no (area, bundles) constructor in this game version; stand at the room's Junimo note and use interact instead.");
        }
        catch (TargetInvocationException ex)
        {
            throw new LateBoundException($"The bundle page could not be opened: {(ex.InnerException ?? ex).Message}");
        }
        return (IClickableMenu)menu!;
    }

    private static ClickableComponent FindBundleComponent(IClickableMenu menu, int index)
    {
        if (LateBound.Require(menu, "bundles") is IEnumerable list)
            foreach (var o in list)
                if (o is ClickableComponent cc && LateBound.TryGet(o, "bundleIndex") is int bi && bi == index)
                    return cc;
        throw new LateBoundException($"Bundle {index} is not on the opened room page (JunimoNoteMenu.bundles / Bundle.bundleIndex differ in this game version).");
    }

    private static List<ClickableComponent> IngredientSlots(IClickableMenu menu) =>
        (LateBound.TryGet(menu, "ingredientSlots") as IEnumerable)?.OfType<ClickableComponent>().ToList() ?? new List<ClickableComponent>();

    /// <summary>Whatever the menu still holds on the cursor goes back into the inventory (or the ground), so closing the menu loses nothing.</summary>
    private static void ReturnJunimoHeldItem(IClickableMenu menu)
    {
        if (LateBound.TryGet(menu, "heldItem") is not Item held) return;
        LateBound.TrySet(menu, "heldItem", null);
        if (P.addItemToInventory(held) is Item rest)
            Game1.createItemDebris(rest, P.getStandingPosition(), P.FacingDirection);
    }

    /// <summary>Click every item of the open reward/grab menu into the inventory until it is empty or the inventory is full.</summary>
    private static IEnumerator CommunityTakeAll()
    {
        for (int i = 0; i < 60; i++)
        {
            if (Game1.activeClickableMenu is not ItemGrabMenu now) yield break;
            var list = now.ItemsToGrabMenu.actualInventory;
            var item = list.FirstOrDefault(x => x != null);
            if (item == null || !P.couldInventoryAcceptThisItem(item)) yield break;
            int slot = IndexOf(list, item);
            var cc = now.ItemsToGrabMenu.inventory.FirstOrDefault(s => s.name == slot.ToString());
            if (cc == null) yield break;
            Click(now, cc);
            yield return Ticks(3);
        }
    }

    // ------------------------------------------------------------------ museum

    private static IEnumerator MuseumRun(JsonElement a, CallContext c)
    {
        var action = S(a, "action").Trim().ToLowerInvariant();
        if (action.Length == 0) action = "list";
        switch (action)
        {
            case "list":
                var museum = Game1.getLocationFromName("ArchaeologyHouse") as LibraryMuseum;
                if (museum == null) { c.Fail("The Museum (ArchaeologyHouse) is not available."); yield break; }
                c.Result = MuseumListText(museum);
                break;
            case "donate":
                yield return MuseumDonate(a, c);
                break;
            case "rewards":
                yield return MuseumRewards(c);
                break;
            default:
                c.InvalidArgs($"Unknown action \"{action}\". Use list, donate or rewards.", "list", "donate", "rewards");
                break;
        }
    }

    /// <summary>LibraryMuseum.isItemSuitableForDonation: true for an artifact or mineral that is not on display yet.</summary>
    private static bool MuseumAccepts(LibraryMuseum museum, Item item)
    {
        // Looked up by name: depending on the game version it takes the Item or its item id.
        foreach (object arg in new object[] { item, item.QualifiedItemId })
            if (LateBound.TryCall(museum, museum.GetType(), "isItemSuitableForDonation", out var r, arg) && r is bool accepted)
                return accepted;
        throw new LateBoundException("LibraryMuseum.isItemSuitableForDonation takes neither an Item nor an item id in this game version.");
    }

    /// <summary>Rewards Gunther holds for this player (LibraryMuseum.getRewardsForPlayer), or -1 when that can't be asked.</summary>
    private static int MuseumRewardCount(LibraryMuseum museum)
    {
        try
        {
            if (LateBound.TryCall(museum, museum.GetType(), "getRewardsForPlayer", out var r, P) && r is ICollection list)
                return list.Count;
        }
        catch (LateBoundException) { }
        return -1;
    }

    private static string MuseumListText(LibraryMuseum museum)
    {
        var sb = new StringBuilder();
        var rows = new List<string>();
        for (int i = 0; i < P.Items.Count; i++)
        {
            var it = P.Items[i];
            if (it is SObject && MuseumAccepts(museum, it))
                rows.Add($"{it.DisplayName} x{it.Stack} (slot {i})");
        }
        sb.Append(rows.Count == 0
            ? "The Museum accepts none of the items you carry. It takes one of each artifact and mineral that is not on display yet."
            : $"The Museum accepts: {string.Join(", ", rows)}. It keeps one of each; donate with donate_museum(item=...) inside the Museum (travel_to ArchaeologyHouse).");
        int rewards = MuseumRewardCount(museum);
        if (rewards > 0) sb.Append($"\nGunther has {rewards} reward(s) waiting for you: claim_museum_rewards() inside the Museum.");
        return sb.ToString();
    }

    /// <summary>
    /// Puts items on the next free display spot, the way MuseumMenu does (museumPieces gets the item id, the stack goes down by one).
    /// Left out on purpose: MuseumMenu's UI, its global chat line and its sounds beyond one.
    /// </summary>
    private static IEnumerator MuseumDonate(JsonElement a, CallContext c)
    {
        if (Loc is not LibraryMuseum museum)
        {
            c.Fail($"Donate inside the Museum; you are in {Loc.Name}. Use travel_to ArchaeologyHouse first.");
            yield break;
        }
        if (Game1.activeClickableMenu != null || Game1.eventUp) { c.Fail("Close the open menu or finish the scene first."); yield break; }
        var want = S(a, "item").Trim();
        if (want.Length == 0) { c.Fail("item is required: an inventory item name, or \"all\"."); yield break; }

        var items = new List<Item>();
        if (want.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var carried in P.Items)
                if (carried is SObject && MuseumAccepts(museum, carried)) items.Add(carried);
            if (items.Count == 0) { c.Fail("The Museum accepts none of the items you carry (list_museum_donations)."); yield break; }
        }
        else
        {
            int slot = FindItem(want, x => x is SObject);
            if (slot < 0) { c.Fail($"No \"{want}\" in your inventory."); yield break; }
            var named = P.Items[slot];
            if (!MuseumAccepts(museum, named))
            {
                c.Fail($"The Museum does not need {named.DisplayName}: it only takes artifacts and minerals, one of each, that are not on display yet.");
                yield break;
            }
            items.Add(named);
        }

        var pieces = LateBound.Require(museum, "museumPieces");
        if (pieces == null) { c.Fail("The Museum has no display list in this game version."); yield break; }
        int rewardsBefore = MuseumRewardCount(museum);
        var donated = new List<string>();
        string? stopped = null;
        foreach (var it in items)
        {
            if (!MuseumAccepts(museum, it)) continue; // an earlier piece of the same kind went up
            var spot = FreeMuseumSpot(museum, pieces);
            if (spot == null) { stopped = "The Museum has no free display spot left."; break; }
            var name = it.DisplayName;
            LateBound.Call(pieces, "Add", spot.Value, it.ItemId);
            it.Stack--;
            if (it.Stack <= 0) P.removeItemFromInventory(it);
            Game1.playSound("stoneStep");
            donated.Add(name);
            Rt.Log.Add("museum_donated", text: $"{P.Name} donated {name} to the Museum",
                args: new { player = Rt.Log.Player, item = name, x = (int)spot.Value.X, y = (int)spot.Value.Y });
            yield return Ticks(8);
        }
        if (donated.Count == 0) { c.Fail(stopped ?? "Nothing was donated."); yield break; }

        TryCheckArchaeologyAchievements();
        var text = $"Donated to the Museum: {string.Join(", ", donated)}.";
        if (stopped != null) text += " " + stopped;
        int rewardsAfter = MuseumRewardCount(museum);
        if (rewardsAfter > 0 && rewardsAfter > rewardsBefore) text += $" Gunther has {rewardsAfter} reward(s) for you now: claim_museum_rewards().";
        c.Result = text;
    }

    /// <summary>The first display tile (row by row) that LibraryMuseum.isTileSuitableForMuseumPiece accepts and nothing stands on yet.</summary>
    private static Vector2? FreeMuseumSpot(LibraryMuseum museum, object pieces)
    {
        var layer = museum.Map.Layers[0];
        for (int y = 0; y < layer.LayerHeight; y++)
            for (int x = 0; x < layer.LayerWidth; x++)
            {
                if (LateBound.Call(museum, "isTileSuitableForMuseumPiece", x, y) is not true) continue;
                var v = new Vector2(x, y);
                if (LateBound.Call(pieces, "ContainsKey", v) is not true) return v;
            }
        return null;
    }

    /// <summary>MuseumMenu asks the stats to check the archaeology achievements after each piece; best effort.</summary>
    private static void TryCheckArchaeologyAchievements()
    {
        try { LateBound.TryCall(Game1.stats, Game1.stats.GetType(), "checkForArchaeologyAchievements", out _); }
        catch (LateBoundException) { }
    }

    /// <summary>Gunther's "Collect" answer opens a grab menu with the earned rewards; take them all.</summary>
    private static IEnumerator MuseumRewards(CallContext c)
    {
        if (Loc is not LibraryMuseum museum)
        {
            c.Fail($"Gunther is in the Museum; you are in {Loc.Name}. Use travel_to ArchaeologyHouse first.");
            yield break;
        }
        if (Game1.activeClickableMenu != null || Game1.eventUp) { c.Fail("Close the open menu or finish the scene first."); yield break; }
        if (MuseumRewardCount(museum) == 0)
        {
            c.Fail("Gunther has no reward for you yet. Rewards come at donation milestones; list_museum_donations shows what you can donate.");
            yield break;
        }

        // Gunther answers at his desk: an Action tile that names him, else his own tile.
        Point? at = null;
        var layer = museum.Map.Layers[0];
        for (int y = 0; y < layer.LayerHeight && at == null; y++)
            for (int x = 0; x < layer.LayerWidth; x++)
                if (museum.doesTileHaveProperty(x, y, "Action", "Buildings") is { } act && act.Contains("Gunther", StringComparison.OrdinalIgnoreCase))
                {
                    at = new Point(x, y);
                    break;
                }
        if (at == null && museum.characters.FirstOrDefault(n => n.Name == "Gunther") is { } gunther) at = gunther.TilePoint;
        if (at == null) { c.Fail("Gunther is not in the Museum right now (it may be closed)."); yield break; }

        var before = Snapshot();
        yield return WalkAdjacent(at.Value, c);
        if (!c.Ok) yield break;
        PressAction(at.Value);
        for (int i = 0; i < 90 && Game1.activeClickableMenu is not DialogueBox; i++) yield return null;
        yield return Ticks(15);

        var box = Game1.activeClickableMenu as DialogueBox;
        if (box == null || !box.isQuestion || box.responses == null)
        {
            var said = box != null ? box.getCurrentString() : "nothing opened";
            c.Fail($"Gunther did not offer a choice ({said}). Stand next to his desk and use interact on it, then menu/respond.");
            yield break;
        }
        int option = Array.FindIndex(box.responses, r => r.responseKey.Equals("Collect", StringComparison.OrdinalIgnoreCase)
                                                        || r.responseText.Contains("collect", StringComparison.OrdinalIgnoreCase));
        if (option < 0)
        {
            box.closeDialogue();
            c.Fail("Gunther did not offer to hand out rewards (none waiting).");
            yield break;
        }

        using (var doc = JsonDocument.Parse($"{{\"option\":{option}}}"))
        {
            var pickOption = doc.RootElement.Clone();
            yield return Respond(pickOption, c);
        }
        if (!c.Ok) yield break;
        for (int i = 0; i < 90 && Game1.activeClickableMenu is not ItemGrabMenu; i++) yield return null;
        if (Game1.activeClickableMenu is ItemGrabMenu)
        {
            yield return CommunityTakeAll();
            if (Game1.activeClickableMenu is ItemGrabMenu g && g.ItemsToGrabMenu.actualInventory.All(i => i == null))
            {
                g.exitThisMenu();
                yield return Ticks(10);
            }
        }

        var diff = Diff(before);
        if (diff.Length == 0)
        {
            c.Fail("No reward reached your inventory." + (Game1.activeClickableMenu is ItemGrabMenu ? " The reward menu is still open: free inventory space, then menu(action=take) or close_menu." : ""));
            yield break;
        }
        var leftover = Game1.activeClickableMenu is ItemGrabMenu ? " Some rewards are still in the open menu (inventory full?): menu(action=take) after freeing space." : "";
        Rt.Log.Add("museum_rewards", text: $"{P.Name} collected the Museum rewards", args: new { player = Rt.Log.Player });
        c.Result = $"Collected the Museum rewards from Gunther. {diff}{leftover}".Trim();
    }
}
