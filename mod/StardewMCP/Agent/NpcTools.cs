using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace StardewMCP.Agent;

/// <summary>One villager as a player sees them right now. Build it on the game thread.</summary>
internal class VillagerInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Location { get; set; } = "?";
    public int X { get; set; }
    public int Y { get; set; }
    public bool Met { get; set; }
    public int Points { get; set; }
    public int Hearts { get; set; }
    public bool TalkedToToday { get; set; }
    public int GiftsToday { get; set; }
    public int GiftsThisWeek { get; set; }
    /// <summary>friendly, dating, engaged, married... (lower case); empty before you met them.</summary>
    public string Status { get; set; } = "";
    public string BirthdaySeason { get; set; } = "";
    public int BirthdayDay { get; set; }
    /// <summary>0 = today; -1 = unknown.</summary>
    public int DaysToBirthday { get; set; } = -1;
    /// <summary>Can be dated and married (#53).</summary>
    public bool Datable { get; set; }
}

/// <summary>
/// A villager as any screen can know them: who, where, and the facts the tools read about them. Plain data, so a farmhand
/// screen can use what the host screen saw (<see cref="NpcInfo.TrackHost"/>).
/// </summary>
internal sealed record NpcSpot(string Name, string DisplayName, string Location, int X, int Y, bool IsVillager, bool CanSocialize,
    bool Datable, string BirthdaySeason, int BirthdayDay);

/// <summary>
/// Pure helpers behind villagers(), talk_to and travel_to for people (#9): finding villagers, their
/// friendship state, birthdays, place aliases. Game thread only. Tool coroutines live in <see cref="AgentTools"/>.
/// </summary>
internal static class NpcInfo
{
    /// <summary>Points per heart.</summary>
    public const int PointsPerHeart = 250;

    private static readonly string[] SeasonNames = { "spring", "summer", "fall", "winter" };

    /// <summary>Items the game treats as more than a gift; they have no taste.</summary>
    public static readonly HashSet<string> SpecialGifts = new(StringComparer.OrdinalIgnoreCase) { "Bouquet", "Mermaid's Pendant" };

    /// <summary>Shop keepers whose name means their shop (travel_to Pierre). Any other villager means where they are now.</summary>
    public static readonly Dictionary<string, string> PlaceAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pierre"] = "SeedShop",
        ["Clint"] = "Blacksmith",
        ["Robin"] = "ScienceHouse",
        ["Marnie"] = "AnimalShop",
        ["Willy"] = "FishShop",
        ["Gus"] = "Saloon",
        // Late-game areas (#55). Skull Cavern is a mine level, so travel_to handles that name itself.
        ["Sandy"] = "SandyHouse",
        ["Oasis"] = "SandyHouse",
        ["Casino"] = "Club",
        ["MovieTheater"] = "MovieTheater",
        ["Movie Theater"] = "MovieTheater",
        ["Cinema"] = "MovieTheater",
        ["Theater"] = "MovieTheater",
        ["Calico Desert"] = "Desert",
        ["CalicoDesert"] = "Desert",
        ["Calico"] = "Desert",
        ["Desert Trader"] = "Desert",
        ["DesertTrader"] = "Desert",
        ["Ginger Island"] = "IslandSouth",
        ["GingerIsland"] = "IslandSouth",
        ["Island"] = "IslandSouth",
        ["Island Farm"] = "IslandWest",
        ["IslandFarm"] = "IslandWest",
        ["Island FarmHouse"] = "IslandFarmHouse",
        ["IslandFarmHouse"] = "IslandFarmHouse"
    };

    /// <summary>Aliases that name an area rather than a person, so ResolvePlace does not say "X works in Y".</summary>
    public static readonly HashSet<string> AreaAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "Oasis", "Casino",
        "MovieTheater", "Movie Theater", "Cinema", "Theater",
        "Calico Desert", "CalicoDesert", "Calico", "Desert Trader", "DesertTrader",
        "Ginger Island", "GingerIsland", "Island",
        "Island Farm", "IslandFarm", "Island FarmHouse", "IslandFarmHouse"
    };

    // ------------------------------------------------------------------ finding people

    // Where everyone was at the host screen's last look. A farmhand screen keeps only the maps a farmer stands on (and the
    // farm) up to date, so its own copy of a villager elsewhere is where they were when it last saw them, or gone altogether:
    // in EP01 P2 and P3 got "No villager called Lewis" 31 times, every time in a house the villager had just left.
    private static List<NpcSpot> _hostSpots = new();

    /// <summary>Ticks between two of the host's looks (<see cref="TrackHost"/>).</summary>
    public const int TrackEvery = 30;

    /// <summary>Host screen, every <see cref="TrackEvery"/> ticks: remember where every villager is, for the farmhand screens.</summary>
    public static void TrackHost()
    {
        if (Context.IsWorldReady && Context.IsMainPlayer && Players.SplitScreen)
            _hostSpots = Local().Select(n => SpotOf(n)).ToList();
    }

    /// <summary>Forget the host's last look (another save, or the title screen).</summary>
    public static void Reset() => _hostSpots = new List<NpcSpot>();

    private static IEnumerable<NPC> Local() => Utility.getAllCharacters().Where(n => n != null && n is not Monster);

    private static NpcSpot SpotOf(NPC n, string? location = null) =>
        new(n.Name, n.displayName, location ?? n.currentLocation?.NameOrUniqueName ?? "?", n.TilePoint.X, n.TilePoint.Y, n.IsVillager,
            n.CanSocialize, n.datable.Value, (Convert.ToString(n.Birthday_Season) ?? "").Trim().ToLowerInvariant(), Convert.ToInt32(n.Birthday_Day));

    /// <summary>
    /// Every NPC (no monsters) where they really are: this screen's own copies on the host; on a farmhand screen the host's last
    /// look, with the farmhand's own live copies for the map it stands on. Whether a villager can be befriended is asked as
    /// this screen's farmer, since the game's conditions for it may read the player.
    /// </summary>
    public static List<NpcSpot> Everyone()
    {
        if (Context.IsMainPlayer || _hostSpots.Count == 0)
            return Local().Select(n => SpotOf(n)).ToList();
        var here = Game1.currentLocation?.characters.Where(n => n != null && n is not Monster)
            .GroupBy(n => n.Name).ToDictionary(g => g.Key, g => g.First()) ?? new Dictionary<string, NPC>();
        var spots = _hostSpots.Select(s => here.TryGetValue(s.Name, out var live)
                ? SpotOf(live)
                : s with { CanSocialize = s.IsVillager && NPC.CanSocializePerData(s.Name, Game1.getLocationFromName(s.Location) ?? Game1.currentLocation) })
            .ToList();
        var known = spots.Select(s => s.Name).ToHashSet();
        spots.AddRange(here.Values.Where(n => !known.Contains(n.Name)).Select(n => SpotOf(n))); // arrived since the host's last look
        return spots;
    }

    /// <summary>Every villager the player can befriend (one per name).</summary>
    public static List<NpcSpot> Social() =>
        Everyone().Where(s => s.CanSocialize).GroupBy(s => s.Name).Select(g => g.First()).ToList();

    /// <summary>Where the NPC called <paramref name="name"/> (name or display name, any case) is, festival villagers included, or null.</summary>
    public static NpcSpot? Find(string name)
    {
        name = name.Trim();
        if (name.Length == 0) return null;
        if (AgentTools.FestivalActor(name) is { } fest) return SpotOf(fest, Game1.currentLocation?.NameOrUniqueName);
        return Match(Everyone(), s => s.Name, s => s.DisplayName, name);
    }

    /// <summary>The NPC called <paramref name="name"/> on <paramref name="loc"/>, the map this screen's farmer stands on, or null: the one to walk up to and talk to.</summary>
    public static NPC? Here(string name, GameLocation? loc)
    {
        name = name.Trim();
        if (name.Length == 0) return null;
        if (AgentTools.FestivalActor(name) is { } fest) return fest;
        return loc == null ? null : Match(loc.characters.Where(n => n != null && n is not Monster), n => n.Name, n => n.displayName, name);
    }

    /// <summary>By name first, then by display name, ignoring case.</summary>
    private static T? Match<T>(IEnumerable<T> all, Func<T, string> name, Func<T, string> display, string asked) where T : class
    {
        var list = all.ToList();
        return list.FirstOrDefault(x => name(x).Equals(asked, StringComparison.OrdinalIgnoreCase)) ??
               list.FirstOrDefault(x => display(x).Equals(asked, StringComparison.OrdinalIgnoreCase));
    }

    public static bool OnMap(NpcSpot spot, GameLocation? loc) => loc != null && spot.Location == loc.NameOrUniqueName;

    public static string WhereIs(NpcSpot spot) => spot.Location == "?" ? "an unknown place" : $"{spot.Location} ({spot.X},{spot.Y})";

    // ------------------------------------------------------------------ friendship state

    public static VillagerInfo Of(Farmer who, NpcSpot npc)
    {
        var info = new VillagerInfo
        {
            Name = npc.Name,
            DisplayName = npc.DisplayName,
            Location = npc.Location,
            X = npc.X,
            Y = npc.Y,
            BirthdaySeason = npc.BirthdaySeason,
            BirthdayDay = npc.BirthdayDay,
            Datable = npc.Datable
        };
        info.DaysToBirthday = DaysToBirthday(info.BirthdaySeason, info.BirthdayDay);
        if (who.friendshipData.TryGetValue(npc.Name, out var f))
        {
            info.Met = true;
            info.Points = f.Points;
            info.Hearts = f.Points / PointsPerHeart;
            info.TalkedToToday = f.TalkedToToday;
            info.GiftsToday = f.GiftsToday;
            info.GiftsThisWeek = f.GiftsThisWeek;
            info.Status = f.Status.ToString().ToLowerInvariant();
        }
        return info;
    }

    // ------------------------------------------------------------------ birthdays

    public static int SeasonIndex(string season)
    {
        var s = season.Trim().ToLowerInvariant();
        if (s == "autumn") s = "fall";
        return Array.IndexOf(SeasonNames, s);
    }

    /// <summary>Days from today until the next birthday: 0 = today, -1 = unknown. A year is 4 x 28 days.</summary>
    public static int DaysToBirthday(string season, int day)
    {
        int s = SeasonIndex(season);
        if (s < 0 || day < 1 || day > 28) return -1;
        int now = Math.Max(0, SeasonIndex(Game1.currentSeason)) * 28 + Game1.dayOfMonth;
        int birthday = s * 28 + day;
        return ((birthday - now) % 112 + 112) % 112;
    }

    public static string BirthdayText(VillagerInfo v)
    {
        if (v.BirthdayDay < 1 || v.DaysToBirthday < 0) return "birthday unknown";
        var s = $"birthday {v.BirthdaySeason} {v.BirthdayDay}";
        if (v.DaysToBirthday == 0) return s + " (TODAY: a gift counts 8x)";
        if (v.DaysToBirthday <= 3) return s + $" (in {v.DaysToBirthday} day{(v.DaysToBirthday == 1 ? "" : "s")})";
        return s;
    }

    // ------------------------------------------------------------------ gift tastes

    /// <summary>The game's taste codes: 0 love, 2 like, 4 dislike, 6 hate, 7 Stardrop Tea (a love), anything else neutral.</summary>
    public static string TasteWord(int code) => GiftTasteMath.Word(code);

    public static string TasteVerb(int code) => GiftTasteMath.Normalize(code) == GiftTasteMath.Neutral ? "was neutral about" : GiftTasteMath.Word(code);

    public static string ItemName(string id)
    {
        try
        {
            return ItemRegistry.GetData(id)?.DisplayName ?? id;
        }
        catch (Exception)
        {
            return id;
        }
    }

    public static string Signed(int n) => n >= 0 ? $"+{n}" : n.ToString();

    // ------------------------------------------------------------------ gift tastes from the game data (PLAN B7)

    /// <summary>One object a farmer can give, with a made item to ask the game's taste rules about.</summary>
    private sealed record Giftable(string QualifiedId, string Name, string Group, SObject Item);

    private static IDictionary<string, StardewValley.GameData.Objects.ObjectData>? _objectsFrom;
    private static object? _trinketsFrom;
    private static List<Giftable> _giftables = new();
    private static List<(string Id, string Display, string Name)> _objectNames = new();
    private static readonly Dictionary<string, NPC> ScratchNpcs = new();

    /// <summary>
    /// Every object and trinket a farmer can give (<see cref="GiftTasteMath.Listable"/>), built again when the game reloads its
    /// object or trinket data. Also fills <see cref="_objectNames"/> (qualified ids) for villagers(item=X).
    /// </summary>
    private static List<Giftable> Giftables()
    {
        var data = Game1.objectData;
        var trinkets = DataLoader.Trinkets(Game1.content);
        if (ReferenceEquals(data, _objectsFrom) && ReferenceEquals(trinkets, _trinketsFrom)) return _giftables;
        var gifts = new List<Giftable>();
        var names = new List<(string, string, string)>();
        var others = new List<(string, string, string)>();
        foreach (var (id, d) in data)
        {
            if (d == null) continue;
            var qid = "(O)" + id;
            var name = ItemName(qid);
            bool listable = GiftTasteMath.Listable(id, name, d.Type, d.Category, d.CanBeGivenAsGift);
            // Real gifts first, so a name shared with a map object (the Stone you carry, not the rocks you break) finds the gift.
            (listable ? names : others).Add((qid, name, d.Name ?? id));
            if (listable && TasteItem(qid) is { } obj && !obj.bigCraftable.Value)
                gifts.Add(new Giftable(qid, name, GiftTasteMath.GroupOf(d.Category, d.Type), obj));
        }
        foreach (var id in trinkets.Keys)
        {
            var qid = "(TR)" + id;
            var name = ItemName(qid);
            bool listable = GiftTasteMath.Listable(id, name, null, GiftTasteMath.TrinketCategory, true);
            (listable ? names : others).Add((qid, name, id));
            if (listable && TasteItem(qid) is { } trinket)
                gifts.Add(new Giftable(qid, name, GiftTasteMath.GroupOf(GiftTasteMath.TrinketCategory, null), trinket));
        }
        _giftables = gifts;
        _objectNames = names.Concat(others).ToList();
        _objectsFrom = data;
        _trinketsFrom = trinkets;
        return gifts;
    }

    /// <summary>
    /// An item to ask the game's taste rules about, or null. A trinket is made with a fixed seed: ItemRegistry would draw its
    /// seed from Game1.random, and a lookup must not move the game's random numbers.
    /// </summary>
    private static SObject? TasteItem(string qualifiedId)
    {
        if (qualifiedId.StartsWith("(TR)", StringComparison.Ordinal))
            return ItemRegistry.Exists(qualifiedId) ? new StardewValley.Objects.Trinkets.Trinket(qualifiedId[4..], 0) : null;
        return ItemRegistry.Create(qualifiedId, allowNull: true) as SObject;
    }

    /// <summary>The qualified id of an unqualified id from Farmer.giftedItems: an object, else a trinket, else null.</summary>
    private static string? GiftedQualifiedId(string id)
    {
        if (id.StartsWith('(')) return ItemRegistry.Exists(id) ? id : null;
        if (ItemRegistry.Exists("(O)" + id)) return "(O)" + id;
        return ItemRegistry.Exists("(TR)" + id) ? "(TR)" + id : null;
    }

    /// <summary>
    /// A bare NPC with the villager's name, to ask the game's own taste rules. NPC.getGiftTasteForThisItem reads only the
    /// name and the game data, so it answers like the real villager, on any screen and wherever they are; kept per name.
    /// </summary>
    private static NPC TasteNpc(string name)
    {
        if (!ScratchNpcs.TryGetValue(name, out var npc))
            ScratchNpcs[name] = npc = new NPC { Name = name };
        return npc;
    }

    /// <summary>The game's taste code of <paramref name="villager"/> (internal name) for an item.</summary>
    public static int Taste(string villager, Item item) => TasteNpc(villager).getGiftTasteForThisItem(item);

    /// <summary>Whether the game takes gifts for this NPC, as NPC.CanReceiveGifts: can befriend, in Data/NPCGiftTastes, CanReceiveGifts.</summary>
    public static bool TakesGifts(NpcSpot s) =>
        s.CanSocialize && Game1.NPCGiftTastes.ContainsKey(s.Name) && (!NPC.TryGetData(s.Name, out var cd) || cd.CanReceiveGifts);

    /// <summary>Villagers this farmer can befriend who take gifts (<see cref="TakesGifts"/>), sorted by shown name.</summary>
    private static List<NpcSpot> GiftTakers() =>
        Social().Where(TakesGifts).OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// Qualified ids of the items in the farmer's pack and in every chest and fridge they can open. Mini-shipping bins are left
    /// out: what is in them is sold overnight (and asking one for a farmer's items would create their slot).
    /// </summary>
    private static HashSet<string> Owned(Farmer who)
    {
        var ids = new HashSet<string>();
        foreach (var it in who.Items)
            if (it != null) ids.Add(it.QualifiedItemId);
        void Add(Chest chest)
        {
            foreach (var it in chest.GetItemsForPlayer(who.UniqueMultiplayerID))
                if (it != null) ids.Add(it.QualifiedItemId);
        }
        Utility.ForEachLocation(loc =>
        {
            foreach (var obj in loc.objects.Values)
                if (obj is Chest chest && ChestLedger.IsStorage(chest)) Add(chest);
            if (loc.GetFridge(false) is { } fridge) Add(fridge);
            return true;
        });
        return ids;
    }

    /// <summary>
    /// Copy what the game itself has shown this farmer into their book: tastes of gifts given (also before this mod or by hand)
    /// and the hints in dialogue and secret notes (Farmer.giftedItems, filled by onGiftGiven and revealGiftTaste). Always on:
    /// it is what the farmer already knows in game. Ids there are unqualified (objects, and trinkets given as gifts).
    /// </summary>
    public static void MergeRevealed(Farmer who)
    {
        var gifted = who.giftedItems;
        if (gifted == null || gifted.Count == 0) return;
        var book = NpcKnowledge.Book(who);
        var learned = new List<(string, string, int)>();
        foreach (var (villager, items) in gifted)
        {
            if (string.IsNullOrEmpty(villager) || items == null || !Game1.NPCGiftTastes.ContainsKey(villager)) continue;
            foreach (var id in items.Keys)
            {
                var qid = GiftedQualifiedId(id);
                if (qid == null || NpcKnowledge.TryTaste(book, villager, qid, out _)) continue;
                if (TasteItem(qid) is { } obj)
                    learned.Add((villager, qid, Taste(villager, obj)));
            }
        }
        if (learned.Count > 0) NpcKnowledge.Merge(who, learned);
    }

    /// <summary>"Loves: ... | Likes: ... | Dislikes: ... | Hates: ..." for one villager from the game data, [have] on kept items.</summary>
    public static string TasteSummary(Farmer who, string villager)
    {
        var owned = Owned(who);
        var npc = TasteNpc(villager);
        var items = Giftables().Select(g => new GiftItem(g.Name, g.Group, npc.getGiftTasteForThisItem(g.Item), owned.Contains(g.QualifiedId)));
        return GiftTasteMath.Summary(items);
    }

    /// <summary>villagers(item=X): who loves, likes, dislikes or hates one object; only what the farmer knows unless <paramref name="all"/>.</summary>
    public static (bool Ok, string Text) WhoLikes(Farmer who, string query, bool all, NpcSpot? only)
    {
        Giftables();
        int at = GiftTasteMath.Match(_objectNames, query);
        if (at < 0)
            return (false, $"No item called \"{query}\". Gifts are objects (crops, forage, fish, food, artisan goods, minerals...) and trinkets; check the spelling.");
        var (qid, display, internalName) = _objectNames[at];
        if (SpecialGifts.Contains(internalName))
            return (true, $"{display} is a special item, not a normal gift: bouquet(name) and mermaid_pendant(name) use it.");
        if (GiftTasteMath.NotAGiftUse(qid) is { } use)
            return (true, $"{display} is not a gift: {use}.");
        if (TasteItem(qid) is not { } obj || obj.bigCraftable.Value || !obj.canBeGivenAsGift())
            return (true, $"{display} can't be given as a gift.");
        if (only != null && !TakesGifts(only))
            return (true, $"{only.DisplayName} does not take gifts.");

        TryMergeRevealed(who);
        var takers = only != null ? new List<NpcSpot> { only } : GiftTakers();
        var have = Owned(who).Contains(qid) ? $" You have {display} (pack or chest)." : "";
        if (all && only != null)
            return (true, $"{only.DisplayName}: {GiftTasteMath.Word(Taste(only.Name, obj))} ({display} as a gift, game data).{have}");
        if (all)
        {
            var tastes = takers.Select(s => (s.DisplayName, Taste(s.Name, obj)));
            return (true, $"{display} as a gift (game data): {GiftTasteMath.Who(tastes, complete: true)}.{have} {GiftTasteMath.PointsNote}");
        }
        var names = takers.ToDictionary(s => s.Name, s => s.DisplayName);
        var known = NpcKnowledge.KnownFor(NpcKnowledge.Book(who), qid).Where(k => names.ContainsKey(k.Villager)).Select(k => (names[k.Villager], k.Taste)).ToList();
        if (known.Count == 0)
            return (true, $"You don't know {(only != null ? only.DisplayName + "'s" : "anyone's")} taste for {display} yet: a gift shows it (give_gift), " +
                          $"and so do hints villagers drop in conversation.{have}");
        var rest = only != null ? "" : " Everyone else: unknown until you give it to them.";
        return (true, $"What you know about {display} as a gift (your gifts and the game's hints): {GiftTasteMath.Who(known, complete: false)}.{rest}{have}");
    }

    // ------------------------------------------------------------------ text for the model

    public static string Line(VillagerInfo v)
    {
        var status = v.Status.Length > 0 && v.Status != "friendly" ? $" | {v.Status}" : "";
        return $"{v.DisplayName}: {v.Location} ({v.X},{v.Y}) | {v.Hearts} hearts ({v.Points} pts) | talked today: {(v.TalkedToToday ? "yes" : "no")}" +
               $" | gifts {v.GiftsToday} today, {v.GiftsThisWeek} this week | {BirthdayText(v)}{status}{Romance.Brief(v)}";
    }

    public static string Roster(Farmer who)
    {
        var infos = Social().Select(n => Of(who, n)).OrderBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"Villagers on farm day {AgentLog.FarmDay} ({Game1.currentSeason} {Game1.dayOfMonth}, {Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth)}). " +
                      "250 points = 1 heart. Talking: +20 points, once a day. Gifts: at most 1 per day and 2 per week per villager; a birthday gift counts 8x.");
        var relationships = Romance.SelfLine(who);
        if (relationships.Length > 0) sb.AppendLine(relationships);
        foreach (var met in infos.Where(i => i.Met))
            sb.AppendLine(Line(met));
        var unmet = infos.Where(i => !i.Met).ToList();
        if (unmet.Count > 0)
            sb.AppendLine("Not met yet (talk_to_villager introduces you): " + string.Join(", ", unmet.Select(i => $"{i.DisplayName} ({i.Location} {i.X},{i.Y})")));
        sb.AppendLine("Romance: villagers marked \"can date\" can be dated and married. bouquet(name) at 8+ hearts starts dating, mermaid_pendant(name) at 10+ hearts proposes " +
                      "(it needs an upgraded house). propose(to) asks another player to marry you.");
        sb.Append("villagers(name) shows one villager's gift tastes and what dating or marriage still needs; villagers(item=X) who loves or hates an item; " +
                  "talk_to_villager(name) finds them and talks; give_gift(name, item) gives a gift.");
        return sb.ToString();
    }

    /// <summary>
    /// Details for one villager: state, your last gift, what other players were seen doing, their gift tastes and their
    /// reaction to what you carry. <paramref name="revealTastes"/> (<see cref="Knowledge.GiftTastes"/>) shows every taste from
    /// the game data; otherwise only what this farmer found out (their gifts and the game's hints, <see cref="MergeRevealed"/>).
    /// </summary>
    public static string Detail(Farmer who, NpcSpot spot, string viewerSeat, bool revealTastes)
    {
        var v = Of(who, spot);
        TryMergeRevealed(who);
        var book = NpcKnowledge.Book(who);
        var sb = new StringBuilder();
        sb.AppendLine(Line(v));
        if (!v.Met) sb.AppendLine("You have not met them yet: talk_to_villager introduces you.");
        sb.AppendLine(Romance.Requirements(who, v));

        if (book.LastGift.TryGetValue(spot.Name, out var last))
            sb.AppendLine($"Your last gift to them: {ItemName(last.ItemId)} on farm day {last.Day}; they {TasteVerb(last.Taste)} it ({Signed(last.Points)} points).");
        var seen = NpcKnowledge.SeenText(spot.Name, viewerSeat);
        if (seen != null) sb.AppendLine("Seen: " + seen + ".");

        bool takesGifts = TakesGifts(spot);
        if (revealTastes && takesGifts)
        {
            try
            {
                sb.AppendLine("Gift tastes (game data; [have] = in your pack or a chest): " + TasteSummary(who, spot.Name));
                sb.AppendLine(GiftTasteMath.PointsNote);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Gift tastes could not be read from the game data ({ex.GetType().Name}: {ex.Message}); the game data may differ from what the mod expects.");
                revealTastes = false;
            }
        }
        else if (book.Tastes.TryGetValue(spot.Name, out var known) && known.Count > 0)
        {
            sb.AppendLine("What you know about their tastes (your gifts and the game's hints):");
            foreach (var word in new[] { "loved", "liked", "neutral", "disliked", "hated" })
            {
                var names = known.Where(kv => TasteWord(kv.Value) == word).Select(kv => ItemName(kv.Key)).OrderBy(x => x).ToList();
                if (names.Count > 0) sb.AppendLine($"  {word}: {string.Join(", ", names)}");
            }
        }
        else if (takesGifts)
            sb.AppendLine("You do not know any of their tastes yet: gifts show them, villagers drop hints, and other players may tell you (or lie).");

        var stacks = who.Items.OfType<SObject>().GroupBy(i => i.QualifiedItemId).ToList();
        if (stacks.Count > 0 && takesGifts)
        {
            sb.AppendLine(revealTastes ? "Their reaction to what you carry (game data):" : "Their reaction to what you carry:");
            foreach (var g in stacks)
            {
                var first = g.First();
                string taste;
                if (SpecialGifts.Contains(first.Name))
                    taste = "a special item, not a normal gift";
                else if (GiftTasteMath.NotAGiftUse(g.Key) is { } use)
                    taste = "not a gift: " + use;
                else if (first.bigCraftable.Value || !first.canBeGivenAsGift())
                    taste = "can't be given";
                else if (revealTastes)
                    taste = TasteWord(Taste(spot.Name, first));
                else if (NpcKnowledge.TryTaste(book, spot.Name, g.Key, out var code))
                    taste = TasteWord(code) + " (known)";
                else
                    taste = "unknown (give one to find out)";
                sb.AppendLine($"  {first.DisplayName} x{g.Sum(i => i.Stack)}: {taste}");
            }
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary><see cref="MergeRevealed"/> that never breaks the tool: the book just stays as it was.</summary>
    public static void TryMergeRevealed(Farmer who)
    {
        try { MergeRevealed(who); }
        catch (Exception) { /* a data mismatch leaves the book as it was */ }
    }

    // ------------------------------------------------------------------ places

    /// <summary>
    /// Turn what the AI typed into a map name: a real map first, then a shop keeper's name (Pierre = SeedShop),
    /// then any villager (where they are now). Map is null when nothing fits; Note says what was assumed.
    /// </summary>
    public static (string? Map, string Note) ResolvePlace(string input)
    {
        input = input.Trim();
        if (input.Length == 0) return (null, "");
        if (Game1.getLocationFromName(input) != null) return (input, "");

        var key = input.EndsWith("'s", StringComparison.OrdinalIgnoreCase) ? input[..^2] : input;
        if (PlaceAliases.TryGetValue(key, out var shop) && Game1.getLocationFromName(shop) != null)
            return (shop, AreaAliases.Contains(key) ? "" : $"{key} works in {shop}.");

        var npc = Find(key);
        if (npc != null && Game1.getLocationFromName(npc.Location) is { } there)
            return (there.Name, $"{npc.DisplayName} was in {WhereIs(npc)} when you set off; they may have moved.");
        return (null, "");
    }

    /// <summary>Maps that are a shop: travel_to_shop accepts these by name (its keeper aliases cover the rest).</summary>
    public static readonly HashSet<string> ShopMaps = new(StringComparer.OrdinalIgnoreCase)
    {
        "SeedShop", "Blacksmith", "ScienceHouse", "AnimalShop", "FishShop", "Saloon",
        "SandyHouse", "Club", "JojaMart", "Hospital", "AdventureGuild"
    };

    /// <summary>
    /// Like <see cref="ResolvePlace"/> but never a person (the phase-4B travel_to_location): a real map first,
    /// then a late-game area alias from <see cref="AreaAliases"/>. A shop keeper's or villager's name resolves
    /// to null — those belong to travel_to_shop and the talk/gift tools.
    /// </summary>
    public static (string? Map, string Note) ResolveStaticPlace(string input)
    {
        input = input.Trim();
        if (input.Length == 0) return (null, "");
        if (Game1.getLocationFromName(input) != null) return (input, "");

        var key = input.EndsWith("'s", StringComparison.OrdinalIgnoreCase) ? input[..^2] : input;
        if (AreaAliases.Contains(key) && PlaceAliases.TryGetValue(key, out var area) && Game1.getLocationFromName(area) != null)
            return (area, "");
        return (null, "");
    }

    /// <summary>
    /// A destination for travel_to_shop (phase 4B): a known shop map from <see cref="ShopMaps"/>, or a keeper
    /// alias from <see cref="PlaceAliases"/> that resolves to one. An arbitrary villager — or a map that is not
    /// a shop — resolves to null; Note says which keeper was assumed.
    /// </summary>
    public static (string? Map, string Note) ResolveShopPlace(string input)
    {
        input = input.Trim();
        if (input.Length == 0) return (null, "");
        if (Game1.getLocationFromName(input) != null)
            return ShopMaps.Contains(input) ? (input, "") : (null, "");

        var key = input.EndsWith("'s", StringComparison.OrdinalIgnoreCase) ? input[..^2] : input;
        if (PlaceAliases.TryGetValue(key, out var shop) && ShopMaps.Contains(shop) && Game1.getLocationFromName(shop) != null)
            return (shop, AreaAliases.Contains(key) ? "" : $"{key} works in {shop}.");
        return (null, "");
    }

    /// <summary>
    /// When the door tile is a timed door ("LockedDoorWarp x y Map open close") and it is outside those hours,
    /// a sentence naming the opening hours; otherwise "". Best effort: the action text is parsed as the game writes it.
    /// </summary>
    public static string DoorHours(GameLocation loc, Point doorTile, string next)
    {
        try
        {
            var action = loc.doesTileHaveProperty(doorTile.X, doorTile.Y, "Action", "Buildings");
            if (action == null) return "";
            var parts = action.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6 || !parts[0].Equals("LockedDoorWarp", StringComparison.OrdinalIgnoreCase)) return "";
            if (!int.TryParse(parts[4], out var open) || !int.TryParse(parts[5], out var close)) return "";
            int now = Game1.timeOfDay;
            if (now >= open && now < close) return "";
            return $"{next} is closed now (it is {WorldText.FormatTime(now)}; it opens at {WorldText.FormatTime(open)} and closes at {WorldText.FormatTime(close)}). ";
        }
        catch (Exception)
        {
            return "";
        }
    }

    // ------------------------------------------------------------------ who is watching

    /// <summary>Other AI seats whose farmer stands on the same map within 12 tiles of <paramref name="self"/>: they can see what it does.</summary>
    public static string[] WhoCanSee(string mySeat, Farmer self)
    {
        var seen = new List<string>();
        foreach (var seat in Players.Slots.Where(s => s.Role == SeatRole.Ai && s.Id != mySeat && s.Active))
        {
            var name = (seat.State as ScreenState)?.Name;
            if (name == null) continue;
            var other = Game1.getOnlineFarmers().FirstOrDefault(f => f.Name == name);
            if (other?.currentLocation == null || self.currentLocation == null) continue;
            if (other.currentLocation.NameOrUniqueName != self.currentLocation.NameOrUniqueName) continue;
            if (Vector2.Distance(other.Tile, self.Tile) <= 12f) seen.Add(seat.Id);
        }
        return seen.ToArray();
    }
}

/// <summary>villagers() and the people-aware talk_to (#9; Legacy since phase 3 of docs/tool-decoupling/README.md — the
/// built-in AI gets talk_to_villager and give_gift, whose Run bodies live in Agent/Semantic/SocialInteractionTools.cs and
/// share this file's TalkToVillagerCore). New tools live here, registered in <see cref="AgentTools.All"/>.</summary>
public static partial class AgentTools
{
    internal static List<ToolDef> VillagerTools() => new()
    {
        new()
        {
            Name = "villagers",
            Description = "Everyone you can befriend: where they are right now (map and tile), hearts and friendship points, whether you already talked to them today, " +
                          "gifts given today and this week (max 1 per day and 2 per week), birthday (flagged when it is today or within 3 days), whether you can date them and whether you are dating or married. " +
                          "With name: also what dating or marriage still needs, your last gift to them, their gift tastes and what they would make of the items you carry. " +
                          "With item: who loves, likes, dislikes or hates that item (with name too: just that villager). " +
                          (Knowledge.GiftTastes
                              ? "Gift tastes come straight from the game data: every love, like, dislike and hate, items you keep in your pack or chests marked [have]. "
                              : "Tastes are discovered: a gift shows the villager's taste, and so do hints villagers drop; nobody knows them from the start. ") +
                          "Costs no game time.",
            Schema = Obj(new
            {
                name = Str("Optional villager name for details, e.g. Abigail"),
                item = Str("Optional item name: who loves or hates it as a gift, e.g. Diamond")
            }),
            Instant = true, AppendStatus = false,
            Run = VillagersTool
        },
        new()
        {
            Name = "talk_to",
            Legacy = true,
            Description = "Legacy mode-switch entry: finds a villager, walks up to them, talks and optionally gives a gift (gift_item). " +
                          "The built-in AI uses the single-intent tools instead: talk_to_villager (talk only) and give_gift (gift with a " +
                          "required item). Someone walking somewhere is followed and talked to as soon as you are next to them (a corner " +
                          "tile is close enough). If they are on another map you travel there first (go=false: just tell me where they are). " +
                          "Reports where they were, your friendship points before and after, their first line and the reaction to a gift. " +
                          "Talking counts once a day; at most 1 gift per day and 2 per week. Time spent travelling is game time. " +
                          "With a fellow farmer's name: walks up to them when they are on your map; farmers talk with say.",
            Schema = Obj(new
            {
                name = Str("Villager name, e.g. Pierre"),
                gift_item = Str("Optional item name to give as a gift"),
                go = new { type = "boolean", description = "Travel to them when they are on another map (default true)" },
                reason = Str(ReasonDesc)
            }, "name", "reason"),
            Run = TalkToNpc
        }
    };

    /// <summary>Not an MCP tool: the runner reaches it through GET /api/relationships, which queues it on each seat's observation lane.</summary>
    internal static ToolDef RelationshipsOp() => new()
    {
        Name = "relationships_report",
        Description = "Friendship data of this seat's farmer as JSON (runner only).",
        Schema = Obj(new { }),
        Instant = true, AppendStatus = false, Mail = false,
        Run = (a, c) => Instant(c, JsonSerializer.Serialize(NpcStory.Report(P, Rt.Log.Player), AgentLog.Json))
    };

    private static IEnumerator VillagersTool(JsonElement a, CallContext c)
    {
        var name = S(a, "name").Trim();
        var item = S(a, "item").Trim();
        NpcSpot? spot = null;
        if (name.Length > 0)
        {
            spot = NpcInfo.Find(name);
            if (spot == null)
            {
                c.Fail(FarmerHint(name) ?? $"No villager called \"{name}\". Call villagers() without a name for the list.");
                yield break;
            }
        }
        if (item.Length > 0)
        {
            (bool ok, string text) r;
            try { r = NpcInfo.WhoLikes(P, item, Knowledge.GiftTastes, spot); }
            catch (Exception ex) { r = (false, $"villagers could not read the gift tastes from the game data ({ex.GetType().Name}: {ex.Message}). The game data may differ from what the mod expects."); }
            if (r.ok) c.Result = r.text; else c.Fail(r.text);
            yield break;
        }
        c.Result = spot == null ? NpcInfo.Roster(P) : NpcInfo.Detail(P, spot, Rt.Log.Player, Knowledge.GiftTastes);
    }

    private static bool NpcFlag(JsonElement a, string name, bool def)
    {
        if (a.ValueKind != JsonValueKind.Object || !a.TryGetProperty(name, out var v)) return def;
        return v.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => def };
    }

    /// <summary>Game minutes since the farm began; for "time spent" reports.</summary>
    private static int NpcMinutes() => Game1.Date.TotalDays * 1440 + Utility.ConvertTimeToMinutes(Game1.timeOfDay);

    private static (int Points, bool Talked, int GiftsToday) NpcFriendship(string name) =>
        P.friendshipData.TryGetValue(name, out var f) ? (f.Points, f.TalkedToToday, f.GiftsToday) : (0, false, 0);

    /// <summary>Why talking or gifting is pointless right now, or null. Only refuses what the game would refuse for sure.</summary>
    private static string? TalkRefusal(NpcSpot npc, Item? gift)
    {
        if (Game1.eventUp && Game1.CurrentEvent is { isFestival: true } && gift == null) return null;
        if (!P.friendshipData.TryGetValue(npc.Name, out var f)) return null; // not met yet: the first talk introduces you
        if (gift == null)
        {
            return f.TalkedToToday
                ? $"You already talked to {npc.DisplayName} today: talking gives friendship once a day. Give a gift (give_gift) or come back tomorrow."
                : null;
        }
        // A bouquet or Mermaid's Pendant is not an ordinary gift (#53): the game decides whether it is accepted today.
        if (gift is SObject special && NpcInfo.SpecialGifts.Contains(special.Name)) return null;
        if (f.GiftsToday >= 1)
            return $"You already gave {npc.DisplayName} a gift today (limit 1 per day). Try again tomorrow.";
        // Birthdays and spouses are exempt from the weekly limit in the vanilla game, so the game decides those.
        bool spouse = f.Status.ToString().Equals("Married", StringComparison.OrdinalIgnoreCase);
        bool birthday = NpcInfo.Of(P, npc).DaysToBirthday == 0;
        if (f.GiftsThisWeek >= 2 && !spouse && !birthday)
            return $"You already gave {npc.DisplayName} 2 gifts this week (the limit; it starts over on Monday). Talk to them instead, or wait.";
        return null;
    }

    internal static IEnumerator TalkTo(JsonElement a, CallContext c) => TalkToNpc(a, c);

    /// <summary>The failure text for a fellow farmer's name (or nickname) given where a villager was expected, or null.</summary>
    private static string? FarmerHint(string name) =>
        SocialTools.OtherFarmer(name) is { } mate
            ? $"{SocialTools.Label(mate)} is a fellow farmer, not a villager. Farmers talk with say: find them, and once they are on your screen say(text=...) is heard " +
              $"(talk_to_villager {SocialTools.Label(mate)} walks you up to them when they are on the same map as you)."
            : null;

    /// <summary>
    /// talk_to with a fellow farmer's name: when they are on this map, walk up to them and tell the farmer to use say. Never
    /// travels and never says where they are: finding each other is part of the show. Ported from
    /// Pichaya31847/stardew-ai-agent (MIT, WalkUpToFarmer in commit 9f69b72, which travels to their map; here it stays on this one).
    /// </summary>
    private static IEnumerator WalkUpToFarmer(PlayerSlot mate, bool go, CallContext c)
    {
        static int Dist(Point a, Point b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        var label = SocialTools.Label(mate);
        bool SameMap(Farmer? f) => f?.currentLocation != null && f.currentLocation.NameOrUniqueName == Loc.NameOrUniqueName;

        var them = Romance.FarmerOf(mate);
        if (!SameMap(them))
        {
            c.Fail(Reasons.NotHere, $"{label} is a fellow farmer and is not on this map. Farmers talk with say, which they hear only when you are on their screen: " +
                                    "go and find them, then talk_to_villager them again or say(text=...).");
            yield break;
        }
        if (!go)
        {
            c.Result = $"{label} is a fellow farmer on this map. Farmers talk with say, not talk_to_villager: say(text=...) is heard when you are on their screen.";
            yield break;
        }
        for (int leg = 0; leg < 6; leg++)
        {
            them = Romance.FarmerOf(mate);
            if (!SameMap(them)) break;
            var aim = them!.TilePoint;
            if (Dist(P.TilePoint, aim) <= 1) break;
            var walk = new CallContext();
            yield return WalkAdjacent(aim, walk);
            if (!walk.Ok) yield return Ticks(30);
        }
        them = Romance.FarmerOf(mate);
        if (!SameMap(them))
        {
            c.Fail(Reasons.NotHere, $"{label} left this map before you got to them. Go and find them, then say(text=...).");
            yield break;
        }
        if (Dist(P.TilePoint, them!.TilePoint) > 3)
        {
            c.Fail(Reasons.NoPath, $"Couldn't catch up with {label}. Try talk_to_villager again, or say(text=...): it is heard while they are on your screen.");
            yield break;
        }
        c.Result = $"You are next to {label} now. Farmers talk with say, not talk_to_villager: call say(text=...) now and they will hear you.";
    }

    /// <summary>
    /// Legacy talk_to entry (mode-switch: talk, or talk + gift via gift_item). Reads its optional gift from the
    /// argument object and hands both intents to the shared <see cref="TalkToVillagerCore"/> path; the semantic
    /// tools call that path directly with a plain string, never a synthesized argument object.
    /// </summary>
    private static IEnumerator TalkToNpc(JsonElement a, CallContext c)
    {
        var gift = S(a, "gift_item").Trim();
        yield return TalkToVillagerCore(a, c, gift.Length > 0 ? gift : null);
    }

    /// <summary>
    /// The one find-approach-talk-gift path behind talk_to, talk_to_villager and give_gift
    /// (docs/tool-decoupling/README.md phase 3): the same villager lookup, walk-up and the game's own
    /// check-at action, so behavior cannot drift between the legacy and the semantic tools. <paramref name="gift"/>
    /// is the gift item name from the caller, or null for a pure talk — the talk_to_villager path always
    /// passes null, so a talk can never look up, offer or report a gift.
    /// </summary>
    private static IEnumerator TalkToVillagerCore(JsonElement a, CallContext c, string? gift)
    {
        var name = S(a, "name").Trim();
        if (name.Length == 0) { c.Fail("name is empty. villagers() lists who you can talk to."); yield break; }
        if (Game1.activeClickableMenu != null)
        {
            c.Fail("A menu or dialogue is open. Finish it with respond() or close_menu first.");
            yield break;
        }
        var spot = NpcInfo.Find(name);
        bool go = NpcFlag(a, "go", true);
        if (spot == null)
        {
            if (SocialTools.OtherFarmer(name) is { } mate) { yield return WalkUpToFarmer(mate, go, c); yield break; }
            c.Fail($"No villager called \"{name}\". villagers() lists who you can talk to.");
            yield break;
        }

        Item? giftItem = null;
        int giftSlot = -1;
        if (gift != null && gift.Length > 0)
        {
            giftSlot = FindItem(gift, it => it is SObject);
            if (giftSlot < 0) { c.Fail($"No \"{gift}\" in inventory to gift."); yield break; }
            giftItem = P.Items[giftSlot];
        }

        var refusal = TalkRefusal(spot, giftItem);
        if (refusal != null) { c.Fail(refusal); yield break; }

        int startMinutes = NpcMinutes();
        bool traveled = false, opened = false, gaveUp = false;
        string failure = "";
        var before = (Points: 0, Talked: false, GiftsToday: 0);
        int tasteCode = -1;
        int stackBefore = giftItem?.Stack ?? 0;
        bool birthdayToday = false;
        NPC? npc = null; // the villager on this map, once walked up to

        // Find them (they walk around, so look them up again after every trip or failed approach), go to them, talk.
        for (int cycle = 0; cycle < 3 && !opened && !gaveUp; cycle++)
        {
            spot = NpcInfo.Find(name) ?? spot;
            if (!NpcInfo.OnMap(spot, Loc))
            {
                if (Game1.getLocationFromName(spot.Location) is not { } there) { failure = "they can't be found right now"; break; }
                if (!go)
                {
                    c.Fail($"{spot.DisplayName} is not on this map ({Loc.Name}). {spot.DisplayName} is in {NpcInfo.WhereIs(spot)}. " +
                           $"travel_to {there.Name}, or call talk_to_villager with go=true to be taken there.");
                    yield break;
                }
                var trip = new CallContext();
                yield return TravelTo(there.Name, trip);
                traveled = true;
                spot = NpcInfo.Find(name) ?? spot;
                if (!NpcInfo.OnMap(spot, Loc))
                {
                    if (!trip.Ok) { failure = $"couldn't reach {spot.DisplayName} in {NpcInfo.WhereIs(spot)}: {trip.Result}"; break; }
                    failure = $"{spot.DisplayName} moved on to {NpcInfo.WhereIs(spot)}";
                    continue;
                }
            }
            if (NpcInfo.Here(name, Loc) == null)
            {
                // A farmhand reads other maps from the host's last look, up to half a second old: they have just left. Look again.
                failure = $"{spot.DisplayName} has just left {Loc.Name}";
                yield return Ticks(NpcInfo.TrackEvery + 1);
                continue;
            }

            for (int attempt = 0; attempt < 3 && !opened; attempt++)
            {
                if (NpcInfo.Here(name, Loc) == null) break; // they left this map: the outer loop follows them
                var walk = new CallContext();
                yield return ApproachNpc(name, walk);
                if (!walk.Ok) { failure = walk.Result.TrimEnd('.'); gaveUp = true; break; } // out of reach, a menu, or no catching up
                npc = NpcInfo.Here(name, Loc);
                if (npc == null || !WalkMath.InReach(P.TilePoint, npc.TilePoint)) continue; // they left this map
                Face(npc.TilePoint);
                if (giftSlot >= 0) P.CurrentToolIndex = giftSlot;
                else if (P.ActiveObject != null) P.CurrentToolIndex = FirstToolSlot();

                before = NpcFriendship(npc.Name);
                if (giftItem != null)
                {
                    tasteCode = (int)npc.getGiftTasteForThisItem(giftItem);
                    birthdayToday = NpcInfo.Of(P, spot).DaysToBirthday == 0;
                }
                bool handled = Game1.tryToCheckAt(npc.TilePoint.ToVector2(), P);
                if (!handled && Game1.activeClickableMenu == null && !Game1.dialogueUp) FestivalAction(npc.TilePoint); // festival booths/actors
                yield return Ticks(20);
                opened = Game1.activeClickableMenu != null || Game1.dialogueUp;
            }
        }

        int spent = NpcMinutes() - startMinutes;
        var spentText = spent >= 10 ? $" Took {spent} game minutes (now {WorldText.FormatTime(Game1.timeOfDay)})." : "";
        if (!opened || npc == null)
        {
            spot = NpcInfo.Find(name) ?? spot;
            var why = failure.Length > 0 ? failure : $"they may be busy, asleep or moving; {spot.DisplayName} is in {NpcInfo.WhereIs(spot)} now";
            c.Fail($"Couldn't start a conversation with {spot.DisplayName} ({why}).{spentText}");
            yield break;
        }

        var after = NpcFriendship(npc.Name);
        int delta = after.Points - before.Points;
        var sb = new StringBuilder($"Talking to {npc.displayName} (friendship {P.getFriendshipHeartLevelForNPC(npc.Name)} hearts).");
        if (traveled) sb.Append($" You went to {Loc.Name}.");
        sb.Append($" Points {before.Points} -> {after.Points} ({NpcInfo.Signed(delta)}).");

        bool accepted = false;
        if (giftItem != null)
        {
            // A bouquet or pendant may change neither the points nor the gift counter: the item leaving the pack counts too.
            bool taken = giftSlot >= P.Items.Count || !ReferenceEquals(P.Items[giftSlot], giftItem) || giftItem.Stack < stackBefore;
            accepted = after.GiftsToday > before.GiftsToday || delta != 0 || taken;
            var itemName = giftItem.DisplayName;
            if (!accepted)
                sb.Append($" The gift ({itemName}) was not accepted.");
            else if (NpcInfo.SpecialGifts.Contains(giftItem.Name))
            {
                sb.Append($" You gave {itemName}.");
                if (giftItem.Name.Equals("Bouquet", StringComparison.OrdinalIgnoreCase))
                    NpcStory.Emit(Rt.Log, "bouquet_given", $"{P.Name} gave {npc.displayName} a bouquet", new { villager = npc.Name });
            }
            else
            {
                bool firstTime = !NpcKnowledge.TryTaste(NpcKnowledge.Book(P), npc.Name, giftItem.QualifiedItemId, out _);
                sb.Append($" Gift: {npc.displayName} {NpcInfo.TasteVerb(tasteCode)} {itemName}{(firstTime ? " (now you know this taste)" : "")}.");
                NpcKnowledge.Learn(P, npc.Name, giftItem.QualifiedItemId, tasteCode, AgentLog.FarmDay, delta);
                NpcStory.Emit(Rt.Log, "gift", $"{P.Name} gave {npc.displayName} {itemName}: {NpcInfo.TasteWord(tasteCode)}",
                    new { villager = npc.Name, item = itemName, taste = NpcInfo.TasteWord(tasteCode), points = delta, birthday = birthdayToday });
                if (birthdayToday)
                    NpcStory.Emit(Rt.Log, "birthday", $"{P.Name} gave {npc.displayName} a birthday gift ({itemName})", new { villager = npc.Name, item = itemName, points = delta });
            }
        }

        if (accepted || (!before.Talked && after.Talked))
            NpcKnowledge.RecordPublic(npc.Name, new PublicTalk(AgentLog.FarmDay, WorldText.FormatTime(Game1.timeOfDay), Rt.Log.Player,
                P.Name, accepted ? "gift" : "talk", NpcInfo.WhoCanSee(Rt.Log.Player, P)));

        if (Game1.activeClickableMenu is DialogueBox box)
        {
            var line = (box.getCurrentString() ?? "").Replace("\n", " ").Replace("^", " ").Trim();
            if (line.Length > 0) sb.Append($" {npc.displayName} says: \"{line}\"");
        }
        sb.Append(spentText);
        c.Result = sb.ToString();
    }

    /// <summary>Ticks of running world talk_to spends walking up to a villager on one map (40 s, about an hour of game time).</summary>
    private const int ApproachTicks = 60 * 40;
    /// <summary>Ticks between two route plans; ticks a walking farmer may stand still before it plans again; ticks no tile
    /// around the villager may be reached before talk_to gives up.</summary>
    private const int ReplanTicks = 20, ApproachStuckTicks = 60, UnreachableTicks = 180;

    /// <summary>
    /// Walk up to a villager, who may be walking somewhere, and stop as soon as they are in reach: any tile around them,
    /// diagonals too, as the action button reaches those. The route leads to the nearest free tile around them (never a warp)
    /// and is planned again once they walk on, so a moving villager is caught up with instead of missed. Ends without failing
    /// when they or the farmer leave the map (talk_to follows them); fails when a menu opens, when no tile around them can be
    /// reached for a few seconds, or after <see cref="ApproachTicks"/>.
    /// </summary>
    private static IEnumerator ApproachNpc(string name, CallContext c)
    {
        yield return WaitIdle();
        var loc = Loc;
        Point? plannedFor = null;
        int nextPlan = 0, unreachable = 0, still = 0;
        var last = P.Position;
        for (int t = 0; t < ApproachTicks; t++)
        {
            // Stepped onto a warp on the way: stop pushing into it, or the game restarts the fade every tick (as in WalkTo).
            if (Game1.isWarping) { P.controller = null; yield break; }
            if (Game1.activeClickableMenu != null)
            {
                StopWalk();
                c.Fail(Reasons.MenuOpen, "A menu or dialogue opened on the way: deal with it (respond or close_menu), then call talk_to_villager again.");
                yield break;
            }
            var npc = Loc == loc ? NpcInfo.Here(name, loc) : null;
            if (npc == null) { StopWalk(); yield break; }
            var me = P.TilePoint;
            var at = npc.TilePoint;
            if (WalkMath.InReach(me, at)) { StopWalk(); yield break; }

            bool walking = P.controller is WalkController;
            still = walking && Vector2.DistanceSquared(P.Position, last) < 0.01f ? still + 1 : 0;
            last = P.Position;
            bool replan = !walking || still > ApproachStuckTicks || plannedFor is not { } aim || WalkMath.Steps(aim, at) >= 2;
            if (replan && t >= nextPlan)
            {
                nextPlan = t + ReplanTicks;
                var route = new Pathfinder().FindPathToAny(loc, me.ToVector2(), WalkMath.Around(at).Where(q => !FarmerOn(loc, q) && !WarpOn(loc, q)));
                if (route == null)
                {
                    // Nothing around them can be reached right now; they may step out of a corner in a moment.
                    StopWalk();
                    plannedFor = null;
                    if ((unreachable += ReplanTicks) >= UnreachableTicks)
                    {
                        c.Fail(Reasons.NoPath, $"No free tile next to {npc.displayName} at ({at.X},{at.Y}) can be reached from here: " +
                                               "they may be behind a counter, a locked door or furniture.");
                        yield break;
                    }
                }
                else
                {
                    unreachable = still = 0;
                    plannedFor = at;
                    StartWalk(loc, route);
                }
            }
            yield return null;
        }
        StopWalk();
        var spot = NpcInfo.Find(name);
        c.Fail(Reasons.NoPath, $"Couldn't catch up with {spot?.DisplayName ?? name}; they are in {(spot == null ? "an unknown place" : NpcInfo.WhereIs(spot))} now.");
    }

    /// <summary>A warp a farmer would take on this tile: standing there to talk would carry it off the map.</summary>
    private static bool WarpOn(GameLocation loc, Point tile) =>
        loc.warps.Any(w => w.X == tile.X && w.Y == tile.Y && !w.npcOnly.Value);
}
