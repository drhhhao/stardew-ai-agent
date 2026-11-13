using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.GameData;
using StardewValley.GameData.Locations;
using StardewValley.Locations;
using StardewValley.TokenizableStrings;
using SObject = StardewValley.Object;

namespace StardewMCP.Agent;

/// <summary>
/// Where an item comes from: crop, fruit tree, animal, fish, crab pot, forage, machine, shop, artifact spot, geode or
/// monster drop. One reverse index over the game's data (Data/Crops, FruitTrees, FarmAnimals, Locations, Fish, Machines,
/// Buildings, Shops, Objects, Monsters), built on first use and kept until the save closes (<see cref="GameSession.OnEnd"/>).
/// It also holds the fishing tables fish_info reads (<see cref="FishSpots"/>, <see cref="CrabPotTypes"/>). The formatting
/// is in the game-free <see cref="SourceBook"/> and <see cref="FishRules"/>.
/// </summary>
public static class KnowledgeSources
{
    private static SourceBook? _book;
    /// <summary>Price inputs the shop lines were worked out with (profit margin, the shops' price-modifier conditions); a change rebuilds the book.</summary>
    private static string _priceState = "";
    private static readonly HashSet<string> _priceConditions = new();
    private static Dictionary<string, FishSpec>? _specs;
    private static Dictionary<string, List<FishSpot>>? _spots;
    private static Dictionary<string, List<string>>? _crabTypes;
    private static Dictionary<string, string>? _displayNames;
    private static readonly List<string> _errors = new(), _bookErrors = new();
    private static bool _registered;

    /// <summary>Short source lines for a qualified item id, e.g. "fish: Town river, Forest river (spring/summer/fall), 9:00-2:00, rain"; empty when unknown.</summary>
    public static IReadOnlyList<string> For(string qualifiedItemId)
    {
        if (string.IsNullOrWhiteSpace(qualifiedItemId)) return Array.Empty<string>();
        try
        {
            var book = Book();
            int level = Game1.player?.FishingLevel ?? 0;
            string qid = CropMath.Qualify(qualifiedItemId);
            int caught = Game1.player != null && Game1.player.fishCaught.TryGetValue(qid, out var n) && n is { Length: > 0 } ? n[0] : 0;
            return book.Lines(qid, boss => Knowledge.Omniscient || level >= boss, caught: caught);
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Forget everything read for this save.</summary>
    public static void Reset()
    {
        _book = null;
        _priceState = "";
        _priceConditions.Clear();
        _specs = null;
        _spots = null;
        _crabTypes = null;
        _displayNames = null;
        _errors.Clear();
        _bookErrors.Clear();
    }

    /// <summary>Parts of the game data that could not be read (a modded or changed file), for a hint in the tools.</summary>
    public static IReadOnlyList<string> Errors => _errors.Concat(_bookErrors).ToList();

    /// <summary>Data/Fish, by unqualified item id.</summary>
    public static IReadOnlyDictionary<string, FishSpec> FishSpecs
    {
        get
        {
            EnsureFish();
            return _specs!;
        }
    }

    /// <summary>Every fishing map (internal name, the farm as "Farm", the mines as "Mines floor 20/60/100") and its fish.</summary>
    public static IReadOnlyDictionary<string, List<FishSpot>> FishSpots
    {
        get
        {
            EnsureFish();
            return _spots!;
        }
    }

    /// <summary>Crab pot fish types ("freshwater", "ocean") of each fishing map.</summary>
    public static IReadOnlyDictionary<string, List<string>> CrabPotTypes
    {
        get
        {
            EnsureFish();
            return _crabTypes!;
        }
    }

    /// <summary>Internal location name -> shown name ("Woods" -> "Secret Woods") for the maps in Data/Locations.</summary>
    public static IReadOnlyDictionary<string, string> DisplayNames
    {
        get
        {
            if (_displayNames != null) return _displayNames;
            Register();
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in LocationNames())
            {
                var data = LocationDataFor(name);
                string display = name;
                try
                {
                    if (!string.IsNullOrEmpty(data?.DisplayName)) display = TokenParser.ParseText(data!.DisplayName) ?? name;
                }
                catch { }
                names[name] = display;
            }
            return _displayNames = names;
        }
    }

    // ------------------------------------------------------------------ building

    private static void Register()
    {
        if (_registered) return;
        _registered = true;
        GameSession.OnEnd("knowledge sources", Reset);
    }

    private static SourceBook Book()
    {
        if (_book != null && PriceState() == _priceState) return _book;
        _bookErrors.Clear();
        _priceConditions.Clear();
        var book = new SourceBook();
        Section("fish data", EnsureFish); // a failed read is cached with the book, not retried on every call
        Section("fish", () => AddFish(book));
        Section("crops", () => AddCrops(book));
        Section("fruit trees", () => AddFruitTrees(book));
        Section("animals", () => AddAnimals(book));
        Section("forage", () => AddForage(book));
        Section("artifact spots", () => AddArtifactSpots(book));
        Section("geodes", () => AddGeodes(book));
        Section("monsters", () => AddMonsters(book));
        Section("machines", () => AddMachines(book));
        Section("buildings", () => AddBuildings(book));
        Section("shops", () => AddShops(book));
        _priceState = PriceState();
        return _book = book;
    }

    /// <summary>
    /// The profit margin and the truth of every condition on a shop price modifier (e.g. Joja's non-member markup), so the
    /// book is rebuilt when one changes (the player joins Joja).
    /// </summary>
    private static string PriceState()
    {
        var parts = new List<string> { (Game1.MasterPlayer?.difficultyModifier ?? 1f).ToString(System.Globalization.CultureInfo.InvariantCulture) };
        foreach (var condition in _priceConditions)
        {
            bool holds;
            try { holds = GameStateQuery.CheckConditions(condition); }
            catch { holds = false; }
            parts.Add(holds ? "1" : "0");
        }
        return string.Join("|", parts);
    }

    private static void Section(string name, Action read)
    {
        try { read(); }
        catch (Exception ex) { _bookErrors.Add($"{name}: {ex.GetType().Name}: {ex.Message}"); }
    }

    /// <summary>Maps with their own Data/Locations entry, the current farm as "Farm"; festival copies and placeholders left out.</summary>
    private static IEnumerable<string> LocationNames()
    {
        yield return "Farm";
        foreach (var key in Game1.locationData.Keys)
        {
            if (key is "Default" or "Temp" or "fishingGame" or "UndergroundMine" or "BeachNightMarket" or "DesertFestival") continue;
            if (key.StartsWith("Farm_")) continue;
            yield return key;
        }
    }

    private static LocationData? LocationDataFor(string name)
    {
        if (name == "Farm") return GameLocation.GetData("Farm");
        return Game1.locationData.TryGetValue(name, out var data) ? data : null;
    }

    private static void EnsureFish()
    {
        if (_spots != null && _specs != null && _crabTypes != null) return;
        Register();
        var specs = new Dictionary<string, FishSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in DataLoader.Fish(Game1.content))
            if (FishRules.ParseFish(kv.Key, kv.Value) is { } spec) specs[kv.Key] = spec;
        _specs = specs;

        var spots = new Dictionary<string, List<FishSpot>>(StringComparer.OrdinalIgnoreCase);
        var crab = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in LocationNames())
        {
            var data = LocationDataFor(name);
            if (data == null) continue;
            try
            {
                var list = FishRules.Simplify(SpotsFrom(name, data, null, false, 0));
                if (list.Count == 0) continue;
                spots[name] = list;
                var types = CrabTypesFor(name, data);
                if (types.Count > 0) crab[name] = types;
            }
            catch (Exception ex) { _errors.Add($"fish in {name}: {ex.GetType().Name}: {ex.Message}"); }
        }

        // the mines: hard-coded fish per floor band (MineShaft.getFish), plus the UndergroundMine data on floors 1-79
        Game1.locationData.TryGetValue("UndergroundMine", out var mine);
        foreach (var row in FishRules.MineFishTable)
        {
            string floor = FishRules.MineFloorName(row.Floor);
            var list = new List<FishSpot> { FishRules.MakeSpot(row.FishId, floor, "", null, null, 0, true, null, false, false) };
            if (row.MineArea == 80)
                list.Add(FishRules.MakeSpot(FishRules.CaveJellyId, floor, "", null, null, 0, true, null, false, false));
            else if (mine != null)
                list.AddRange(SpotsFrom(floor, mine, null, false, 0));
            spots[floor] = FishRules.Simplify(list);
        }
        _spots = spots;
        _crabTypes = crab;
    }

    /// <summary>The fish of one Data/Locations entry, following LOCATION_FISH (farm maps that borrow the Forest's fish).</summary>
    private static List<FishSpot> SpotsFrom(string label, LocationData data, string? forcedArea, bool inherited, int depth)
    {
        var list = new List<FishSpot>();
        if (data.Fish == null) return list;
        foreach (var spawn in data.Fish)
        {
            if (spawn == null || (inherited && !spawn.CanBeInherited)) continue;
            string area = forcedArea ?? AreaName(data, spawn.FishAreaId);
            var ids = spawn.RandomItemId is { Count: > 0 } r ? r : new List<string> { spawn.ItemId };
            foreach (var raw in ids)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                if (raw.StartsWith("LOCATION_FISH ") && depth < 2)
                {
                    var args = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (args.Length > 1 && Game1.locationData.TryGetValue(args[1], out var other) && other != null)
                        list.AddRange(SpotsFrom(label, other, AreaName(data, spawn.FishAreaId), true, depth + 1));
                    continue;
                }
                if (raw.Contains(' ')) continue; // another item query (secret notes, trash...)
                string qid = CropMath.Qualify(raw);
                if (!IsFish(qid)) continue;
                _specs!.TryGetValue(CropMath.Unqualify(qid), out var spec);
                string hint = FishRules.SpotHint(Rect(spawn.PlayerPosition), Rect(spawn.BobberPosition), spawn.MinDistanceFromShore);
                list.Add(FishRules.MakeSpot(qid, label, area, spawn.Season.HasValue ? (int)spawn.Season.Value : null, spawn.Condition,
                    spawn.MinFishingLevel, spawn.IgnoreFishDataRequirements, spec, spawn.IsBossFish, spawn.RequireMagicBait, hint, spawn.CatchLimit));
            }
        }
        return list;
    }

    private static (int, int, int, int)? Rect(Microsoft.Xna.Framework.Rectangle? r) =>
        r is { } v ? (v.X, v.Y, v.Width, v.Height) : null;

    private static string AreaName(LocationData data, string? areaId)
    {
        if (string.IsNullOrEmpty(areaId)) return "";
        if (data.FishAreas != null && data.FishAreas.TryGetValue(areaId, out var area) && !string.IsNullOrEmpty(area?.DisplayName))
        {
            try { return TokenParser.ParseText(area!.DisplayName) ?? FishRules.AreaLabel(areaId); }
            catch { }
        }
        return FishRules.AreaLabel(areaId);
    }

    /// <summary>Objects of type Fish that are not trash: fish, algae, seaweed, jellies.</summary>
    public static bool IsFish(string qid)
    {
        if (!qid.StartsWith("(O)")) return false;
        return Game1.objectData.TryGetValue(CropMath.Unqualify(qid), out var d) && d != null && d.Type == "Fish" && d.Category != SObject.junkCategory;
    }

    /// <summary>
    /// What crab pots catch in a map, as GameLocation.GetCrabPotFishForTile decides: each fish area's types (none listed:
    /// freshwater), freshwater for a map without areas; Ginger Island water outside its areas and the beach farm's are ocean.
    /// Empty where crab pots cannot be placed (CrabPot.IsValidCrabPotLocationTile: the volcano and the mines).
    /// </summary>
    private static List<string> CrabTypesFor(string name, LocationData data)
    {
        var types = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        GameLocation? loc = null;
        try { loc = name == "Farm" ? Game1.getFarm() : Game1.getLocationFromName(name); } catch { }
        if (loc is Caldera or VolcanoDungeon or MineShaft || name is "Caldera" or "Mine" or "UndergroundMine" || name.StartsWith("VolcanoDungeon"))
            return new List<string>();
        if (loc is Farm farm && farm.catchOceanCrabPotFishFromThisSpot(0, 0)) return new List<string> { "ocean" };
        bool island = loc is IslandLocation && loc is not IslandNorth;
        if (data.FishAreas is { Count: > 0 })
        {
            foreach (var area in data.FishAreas.Values)
            {
                if (area?.CrabPotFishTypes is { Count: > 0 } t) types.UnionWith(t);
                else types.Add("freshwater");
            }
            if (island) types.Add("ocean");
        }
        else types.Add(island ? "ocean" : "freshwater");
        return types.ToList();
    }

    // ------------------------------------------------------------------ sources

    private static string Name(string qid)
    {
        try { return ItemRegistry.GetData(qid)?.DisplayName ?? qid; }
        catch { return qid; }
    }

    /// <summary>A plain item id (not an item query) as a qualified id, else null.</summary>
    private static string? PlainId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Contains(' ')) return null;
        try
        {
            string qid = ItemRegistry.QualifyItemId(raw) ?? CropMath.Qualify(raw); // the game's own lookup: "TricornHat" is a hat
            return ItemRegistry.GetData(qid) != null ? qid : null;
        }
        catch { return null; }
    }

    private static IEnumerable<string> Ids(GenericSpawnItemData spawn)
    {
        if (spawn.IsRecipe) yield break; // gives the recipe, not the item
        if (spawn.RandomItemId is { Count: > 0 } r)
        {
            foreach (var id in r)
                if (PlainId(id) is { } q) yield return q;
        }
        else if (PlainId(spawn.ItemId) is { } q) yield return q;
    }

    private static int Seasons(string? condition, Season? season = null)
    {
        var c = FishRules.ParseCondition(condition);
        return c.Seasons & (season.HasValue ? SeasonMask.Of((int)season.Value) : SeasonMask.All);
    }

    public static string FishDetail(FishSpot s)
    {
        var parts = new List<string>();
        if (!FishRules.AnyTime(s.Times)) parts.Add(FishRules.Hours(s.Times));
        if (s.Weather != FishWeather.Any) parts.Add(FishRules.WeatherText(s.Weather));
        if (s.MinLevel > 0) parts.Add($"Fishing {s.MinLevel}+");
        if (s.MagicBait) parts.Add("Magic Bait");
        if (s.Festival.Length > 0) parts.Add("during " + FishRules.Words(s.Festival));
        if (FishRules.LimitText(s.CatchLimit) is { Length: > 0 } limit) parts.Add(limit);
        return string.Join(", ", parts);
    }

    private static void AddFish(SourceBook book)
    {
        foreach (var kv in _spots!)
            foreach (var s in kv.Value)
            {
                if (s.SpecialOrder) continue;
                book.AddPlace(s.FishId, SourceKind.Fish, s.Place, s.Seasons, FishDetail(s), s.Boss ? s.MinLevel : -1, s.CatchLimit);
            }
        foreach (var spec in _specs!.Values.Where(x => x.IsTrap))
            foreach (var type in spec.TrapTypes)
            {
                var maps = FishRules.ValleyFirst(_crabTypes!.Where(c => c.Value.Contains(type, StringComparer.OrdinalIgnoreCase)).Select(c => c.Key));
                book.AddText(CropMath.Qualify(spec.Id), SourceKind.CrabPot, $"{FishRules.CrabWaterText(type)}{(maps.Count > 0 ? $" ({SourceBook.Names(maps, 4)})" : "")}");
            }
    }

    private static void AddCrops(SourceBook book)
    {
        foreach (var kv in Game1.cropData)
        {
            if (kv.Value == null || PlainId(kv.Value.HarvestItemId) is not { } harvest) continue;
            int mask = 0;
            if (kv.Value.Seasons != null)
                foreach (var s in kv.Value.Seasons) mask |= SeasonMask.Of((int)s);
            book.AddText(harvest, SourceKind.Crop, $"from {Name(CropMath.Qualify(kv.Key))}", mask == 0 ? SeasonMask.All : mask);
        }
    }

    private static void AddFruitTrees(SourceBook book)
    {
        foreach (var kv in Game1.fruitTreeData)
        {
            if (kv.Value?.Fruit == null) continue;
            int treeMask = 0;
            if (kv.Value.Seasons != null)
                foreach (var s in kv.Value.Seasons) treeMask |= SeasonMask.Of((int)s);
            if (treeMask == 0) treeMask = SeasonMask.All;
            foreach (var fruit in kv.Value.Fruit)
            {
                if (fruit == null) continue;
                int mask = Seasons(fruit.Condition, fruit.Season) & (fruit.Season.HasValue ? SeasonMask.All : treeMask);
                foreach (var id in Ids(fruit))
                    book.AddText(id, SourceKind.FruitTree, Name(CropMath.Qualify(kv.Key)), mask);
            }
        }
    }

    private static void AddAnimals(SourceBook book)
    {
        foreach (var kv in Game1.farmAnimalData)
        {
            var data = kv.Value;
            if (data == null) continue;
            string name = kv.Key;
            try { if (!string.IsNullOrEmpty(data.DisplayName)) name = TokenParser.ParseText(data.DisplayName) ?? kv.Key; } catch { }
            if (data.ProduceItemIds != null)
                foreach (var p in data.ProduceItemIds)
                    if (PlainId(p?.ItemId) is { } id) book.AddText(id, SourceKind.Animal, name);
            if (data.DeluxeProduceItemIds != null)
                foreach (var p in data.DeluxeProduceItemIds)
                    if (PlainId(p?.ItemId) is { } id) book.AddText(id, SourceKind.Animal, $"{name} (deluxe, high friendship)");
        }
    }

    private static void AddForage(SourceBook book)
    {
        foreach (var name in LocationNames())
        {
            var data = LocationDataFor(name);
            if (data?.Forage == null) continue;
            foreach (var f in data.Forage)
            {
                if (f == null || FishRules.ParseCondition(f.Condition).SpecialOrder) continue;
                foreach (var id in Ids(f))
                    book.AddPlace(id, SourceKind.Forage, name, Seasons(f.Condition, f.Season));
            }
        }
    }

    private static void AddArtifactSpots(SourceBook book)
    {
        void Add(string place, LocationData? data)
        {
            if (data?.ArtifactSpots == null) return;
            foreach (var a in data.ArtifactSpots)
            {
                if (a == null || FishRules.ParseCondition(a.Condition).SpecialOrder) continue;
                int mask = Seasons(a.Condition);
                // LOST_BOOK_OR_ITEM and SECRET_NOTE_OR_ITEM give their last argument once the books or notes run out
                if (a.ItemId is { } q && (q.StartsWith("LOST_BOOK_OR_ITEM ") || q.StartsWith("SECRET_NOTE_OR_ITEM ")))
                {
                    if (PlainId(q.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last()) is { } fallback)
                        book.AddPlace(fallback, SourceKind.ArtifactSpot, place, mask);
                    continue;
                }
                foreach (var id in Ids(a)) book.AddPlace(id, SourceKind.ArtifactSpot, place, mask);
            }
        }

        Game1.locationData.TryGetValue("Default", out var all);
        Add("any map", all);
        foreach (var name in LocationNames()) Add(name, LocationDataFor(name));
        // RANDOM_ARTIFACT_FOR_DIG_SPOT: artifacts list the maps they are dug up in
        foreach (var kv in Game1.objectData)
            if (kv.Value?.ArtifactSpotChances != null)
                foreach (var place in kv.Value.ArtifactSpotChances.Keys)
                    book.AddPlace(CropMath.Qualify(kv.Key), SourceKind.ArtifactSpot, place);
    }

    private static void AddGeodes(SourceBook book)
    {
        foreach (var kv in Game1.objectData)
        {
            var data = kv.Value;
            if (data == null || (!data.GeodeDropsDefaultItems && data.GeodeDrops is not { Count: > 0 })) continue;
            string geode = CropMath.Qualify(kv.Key), name = Name(geode);
            if (data.GeodeDrops != null)
                foreach (var drop in data.GeodeDrops)
                {
                    if (drop == null || FishRules.ParseCondition(drop.Condition).SpecialOrder) continue;
                    foreach (var id in Ids(drop)) book.AddText(id, SourceKind.Geode, name);
                }
            if (data.GeodeDropsDefaultItems)
                foreach (var id in SourceBook.GeodeFallback(geode)) book.AddText(id, SourceKind.Geode, name);
        }
        // Mystery Box and Golden Mystery Box: tables hard-coded in Utility.getTreasureFromGeode
        foreach (var box in new[] { "(O)MysteryBox", "(O)GoldenMysteryBox" })
        {
            if (PlainId(box) == null) continue;
            string name = Name(box);
            foreach (var raw in SourceBook.MysteryBoxDrops(box))
                if (PlainId(raw) is { } id) book.AddText(id, SourceKind.Geode, name, rank: -1);
        }
    }

    private static void AddMonsters(SourceBook book)
    {
        foreach (var kv in DataLoader.Monsters(Game1.content))
        {
            var f = (kv.Value ?? "").Split('/');
            if (f.Length < 7) continue;
            string name = f.Length > 14 && f[14].Trim().Length > 0 ? f[14] : kv.Key;
            var drops = f[6].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i + 1 < drops.Length; i += 2)
            {
                if (drops[i].StartsWith("-")) continue;
                double.TryParse(drops[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double chance);
                string id = CropMath.Qualify(drops[i]);
                if (ItemRegistry.GetData(id) != null) book.AddText(id, SourceKind.Monster, name, rank: chance);
            }
        }
    }

    private static string InputText(IEnumerable<(string? itemId, int count, IList<string>? tags)> triggers)
    {
        var parts = new List<string>();
        foreach (var (itemId, count, tags) in triggers)
        {
            if (!string.IsNullOrEmpty(itemId))
                parts.Add((count > 1 ? $"{count} " : "") + Name(CropMath.Qualify(itemId)));
            else if (tags is { Count: > 0 })
            {
                var words = new List<string>();
                foreach (var tag in tags)
                {
                    if (SourceBook.TagItemId(tag) is { } id) words.Add(Name(id));
                    else if (tag.StartsWith("category_") || tag.StartsWith("item_")) words.Add(SourceBook.TagText(tag));
                }
                if (words.Count > 0) parts.Add((count > 1 ? $"{count} " : "") + string.Join(" ", words));
            }
        }
        parts = parts.Distinct().ToList();
        return parts.Count == 0 ? "" : SourceBook.Names(parts, 3).Replace(", and ", " and ").Replace(", ", " or ");
    }

    private static void AddMachines(SourceBook book)
    {
        var flavored = ItemRegistry.GetObjectTypeDefinition();
        foreach (var kv in DataLoader.Machines(Game1.content))
        {
            if (kv.Value?.OutputRules == null) continue;
            string machine = Name(kv.Key);
            foreach (var rule in kv.Value.OutputRules)
            {
                if (rule?.OutputItem == null) continue;
                string inputs = InputText((rule.Triggers ?? new()).Where(t => t != null).Select(t => ((string?)t.RequiredItemId, t.RequiredCount, (IList<string>?)t.RequiredTags)));
                string text = inputs.Length > 0 ? $"{machine} from {inputs}" : machine;
                foreach (var output in rule.OutputItem)
                {
                    if (output == null) continue;
                    var raws = output.RandomItemId is { Count: > 0 } r ? r : new List<string> { output.ItemId };
                    foreach (var raw in raws)
                    {
                        if (string.IsNullOrWhiteSpace(raw)) continue;
                        string? id = null;
                        if (raw.StartsWith("FLAVORED_ITEM "))
                        {
                            var args = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (args.Length > 1 && Enum.TryParse<SObject.PreserveType>(args[1], true, out var type))
                                id = flavored.GetBaseItemIdForFlavoredItem(type, null);
                        }
                        else id = PlainId(raw);
                        if (id != null && ItemRegistry.GetData(id) != null) book.AddText(id, SourceKind.Machine, text);
                    }
                }
            }
        }
    }

    private static void AddBuildings(SourceBook book)
    {
        foreach (var kv in Game1.buildingData)
        {
            if (kv.Value?.ItemConversions == null) continue;
            string name = kv.Key;
            try { if (!string.IsNullOrEmpty(kv.Value.Name)) name = TokenParser.ParseText(kv.Value.Name) ?? kv.Key; } catch { }
            foreach (var conv in kv.Value.ItemConversions)
            {
                if (conv?.ProducedItems == null) continue;
                string inputs = InputText(new[] { ((string?)null, conv.RequiredCount, (IList<string>?)conv.RequiredTags) });
                foreach (var p in conv.ProducedItems)
                    if (p != null)
                        foreach (var id in Ids(p)) book.AddText(id, SourceKind.Machine, inputs.Length > 0 ? $"{name} from {inputs}" : name);
            }
        }
    }

    private static void AddShops(SourceBook book)
    {
        foreach (var shop in DataLoader.Shops(Game1.content))
        {
            if (shop.Value?.Items == null || SourceBook.SkipShop(shop.Key)) continue;
            string label = SourceBook.ShopLabel(shop.Key), unit = SourceBook.CurrencyUnit(shop.Value.Currency);
            double rank = shop.Key switch { "SeedShop" => 3, "Joja" => 2, "Traveler" => 0.5, _ => shop.Value.Currency == 0 ? 1 : 0 };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in shop.Value.Items)
            {
                if (entry == null || entry.IsRecipe || FishRules.ParseCondition(entry.Condition).SpecialOrder) continue;
                foreach (var id in Ids(entry))
                {
                    if (!seen.Add(id) || ItemRegistry.GetData(id) == null) continue;
                    book.AddText(id, SourceKind.Shop, $"{label} {PriceText(id, shop.Value, entry, unit, shop.Key == "Traveler")}", Seasons(entry.Condition), rank);
                }
            }
        }
    }

    /// <summary>The price as ShopBuilder works it out (base price, then the shop's and the entry's price modifiers); "about" when random.</summary>
    private static string PriceText(string qid, StardewValley.GameData.Shops.ShopData shop, StardewValley.GameData.Shops.ShopItemData entry, string unit, bool random)
    {
        if (!string.IsNullOrEmpty(entry.TradeItemId))
            return $"for {Math.Max(entry.TradeItemAmount, 1)} {Name(CropMath.Qualify(entry.TradeItemId))}" + (entry.Price > 0 ? $" + {entry.Price}{unit}" : "");
        Item? item = null;
        try { item = ItemRegistry.Create(qid); } catch { }
        float price = entry.Price;
        if (price < 0)
        {
            if (entry.UseObjectDataPrice && item is SObject obj) price = obj.Price;
            else if (item != null) price = item.salePrice(ignoreProfitMargins: true);
            else return "(price varies)";
        }
        // ShopBuilder.GetBasePrice: the farm's profit margin, then a whole number
        if (entry.ApplyProfitMargins ?? shop.ApplyProfitMargins ?? item?.appliesProfitMargins() ?? false)
            price *= Game1.MasterPlayer?.difficultyModifier ?? 1f;
        price = (int)price;
        if (!entry.IgnoreShopPriceModifiers && shop.PriceModifiers != null)
            foreach (var m in shop.PriceModifiers)
                if (!string.IsNullOrWhiteSpace(m?.Condition)) _priceConditions.Add(m!.Condition);
        if (entry.PriceModifiers != null)
            foreach (var m in entry.PriceModifiers)
                if (!string.IsNullOrWhiteSpace(m?.Condition)) _priceConditions.Add(m!.Condition);
        bool about = random || (entry.PriceModifiers?.Any(m => m?.RandomAmount is { Count: > 0 }) ?? false);
        if (!entry.IgnoreShopPriceModifiers && shop.PriceModifiers is { Count: > 0 })
            price = Utility.ApplyQuantityModifiers(price, shop.PriceModifiers, shop.PriceModifierMode, null, null, item);
        if (!about && entry.PriceModifiers is { Count: > 0 })
            price = Utility.ApplyQuantityModifiers(price, entry.PriceModifiers, entry.PriceModifierMode, null, null, item);
        return $"{(about ? "about " : "")}{(int)price}{unit}";
    }
}
