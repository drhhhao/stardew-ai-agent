using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using StardewValley;
using StardewValley.Locations;

namespace StardewMCP.Agent;

/// <summary>
/// fish_info: which fish bite where and when, read from Data/Locations and Data/Fish through <see cref="KnowledgeSources"/>
/// (the mines' hard-coded fish from <see cref="FishRules.MineFishTable"/>). A pure observation (Instant, no game time).
/// Legendary fish show only with GuideKnowledge or once the Fishing level reaches theirs; spawn chances are never shown.
/// </summary>
public static class FishInfoTools
{
    private const int MaxNotNow = 16, MaxBundleFish = 5, MaxMaps = 14;

    public static List<ToolDef> All() => new()
    {
        new()
        {
            Name = "fish_info",
            Description = "Which fish bite where and when, read from the game's own data. With no arguments: the fish that can bite where you are right now " +
                          "(this map, season, time, weather and your Fishing level), each with its price, hours, weather, difficulty, whether you have caught it " +
                          "and whether a Community Center bundle still needs it; then the fish of this map that bite at other times or seasons, what crab pots catch here, " +
                          "and bundle fish biting elsewhere now. location=<map> shows another map (Town, Forest, Mountain, Beach, Woods, Desert, Sewer, Mines...). " +
                          "fish=<name> shows every map, season, hours and weather for one fish. season, weather (rain or sun) and time (e.g. 18:00) change the conditions checked. " +
                          "Check it before you go fishing. Costs no game time.",
            Schema = AgentTools.Obj(new
            {
                location = AgentTools.Str("Map name, e.g. Beach, Forest, Town, Mountain, Woods, Mines (default: where you are)"),
                fish = AgentTools.Str("Fish name, e.g. Catfish: where and when it bites"),
                season = AgentTools.Str("spring, summer, fall or winter (default: the map's season today)"),
                weather = AgentTools.Str("rain or sun (default: today's weather)"),
                time = AgentTools.Str("Time of day, e.g. 18:00 or 6pm (default: now)")
            }),
            Instant = true, AppendStatus = false,
            Run = (a, c) => Report(c, () => Build(a))
        }
    };

    /// <summary>Runs the report; a data mismatch (a modded or changed game file) becomes a clear failure, not a crash.</summary>
    private static IEnumerator Report(CallContext c, Func<(bool ok, string text)> build)
    {
        (bool ok, string text) r;
        try { r = build(); }
        catch (Exception ex) { r = (false, $"fish_info could not read the game data ({ex.GetType().Name}: {ex.Message}). Tell the player; the game data may differ from what the mod expects."); }
        if (r.ok) c.Result = r.text; else c.Fail(r.text);
        yield break;
    }

    /// <summary>The conditions a report checks; null fields come from each map (its season and weather today).</summary>
    private sealed record Cond(int? Season, bool? Rain, int Time, int Level);

    private static (bool ok, string text) Build(JsonElement a)
    {
        if (Game1.player == null || Game1.locationData == null || Game1.objectData == null) return (false, "No save is loaded.");
        string fish = AgentTools.S(a, "fish").Trim(), location = AgentTools.S(a, "location").Trim();
        string seasonArg = AgentTools.S(a, "season").Trim(), weatherArg = AgentTools.S(a, "weather").Trim(), timeArg = AgentTools.S(a, "time").Trim();

        int? season = null;
        if (seasonArg.Length > 0)
        {
            int s = CropMath.SeasonIndex(seasonArg);
            if (s < 0) return (false, $"Unknown season \"{seasonArg}\". Use spring, summer, fall or winter.");
            season = s;
        }
        bool? rain = null;
        if (weatherArg.Length > 0)
        {
            rain = FishRules.ParseWeather(weatherArg);
            if (rain == null) return (false, $"Unknown weather \"{weatherArg}\". Use rain or sun.");
        }
        int time = Game1.timeOfDay;
        if (timeArg.Length > 0 && !timeArg.Equals("now", StringComparison.OrdinalIgnoreCase))
        {
            if (FishRules.ParseTime(timeArg) is not int t) return (false, $"Unreadable time \"{timeArg}\". Give a time between 6:00 and 2:00, e.g. 18:00 or 6pm.");
            time = t;
        }
        var cond = new Cond(season, rain, time, Game1.player.FishingLevel);

        if (location.Length > 0)
        {
            var (places, error) = ResolvePlaces(location);
            if (places == null) return (false, error);
            return fish.Length > 0 ? FishReport(fish, cond, places) : (true, string.Join("\n\n", places.Select(p => PlaceReport(p, Instance(p), cond, false))));
        }
        if (fish.Length > 0) return FishReport(fish, cond, null);

        var here = Game1.currentLocation;
        if (here == null) return (false, "You are not on a map.");
        string? place = HerePlace(here);
        if (place == null)
            return (true, $"No fish bite in {here.DisplayName} ({here.Name}). Maps with fish: {MapList()}. Ask fish_info(location=...) for one of them.");
        return (true, PlaceReport(place, here, cond, true));
    }

    // ------------------------------------------------------------------ places

    /// <summary>The fishing table for the map the player stands on: the farm, a mine floor band, the map a festival copy stands in for, or the map's own name.</summary>
    private static string? HerePlace(GameLocation here)
    {
        if (here is MineShaft shaft)
        {
            var row = FishRules.MineFishFor(shaft.getMineArea());
            return row == null ? null : FishRules.MineFloorName(row.Floor);
        }
        string name = here is Farm ? "Farm" : FishRules.BaseMap(here.Name);
        return KnowledgeSources.FishSpots.ContainsKey(name) ? name : null;
    }

    private static (List<string>? places, string error) ResolvePlaces(string query)
    {
        if (FishRules.ParseMineFloor(query) is int floor)
        {
            if (floor == 0) return (FishRules.MineFishTable.Select(m => FishRules.MineFloorName(m.Floor)).ToList(), "");
            var row = FishRules.MineFishFor(FishRules.MineArea(floor));
            if (row == null) return (null, $"Mine floor {floor} is in the Skull Cavern; it has no fishing water.");
            return (new List<string> { FishRules.MineFloorName(row.Floor) }, "");
        }
        var maps = KnowledgeSources.DisplayNames.Select(kv => (kv.Key, kv.Value));
        var (name, candidates) = FishRules.ResolveLocation(query, maps);
        if (name == null)
            return (null, candidates.Count > 0
                ? $"Several maps match \"{query}\": {string.Join(", ", candidates.Take(MaxMaps))}. Ask again with the full name."
                : $"No map called \"{query}\". Maps with fish: {MapList()}.");
        if (!KnowledgeSources.FishSpots.ContainsKey(name))
            return (null, $"No fish bite in {Label(name)}. Maps with fish: {MapList()}.");
        return (new List<string> { name }, "");
    }

    private static GameLocation? Instance(string place)
    {
        try
        {
            if (place == "Farm") return Game1.getFarm();
            if (place.StartsWith("Mines floor")) return Game1.getLocationFromName("Mine");
            return Game1.getLocationFromName(place);
        }
        catch { return null; }
    }

    private static string MapList() =>
        string.Join(", ", KnowledgeSources.FishSpots.Keys.Where(k => !k.StartsWith("Island") && k is not ("Submarine" or "Caldera" or "BoatTunnel"))
            .OrderBy(k => k.StartsWith("Mines") ? 1 : 0).ThenBy(k => k)) + " (and Ginger Island maps)";

    /// <summary>"Woods (Secret Woods)"; just the name when it is shown the same.</summary>
    private static string Label(string place)
    {
        if (KnowledgeSources.DisplayNames.TryGetValue(place, out var shown) && !string.Equals(shown, place, StringComparison.OrdinalIgnoreCase) && shown.Length > 0)
            return $"{place} ({shown})";
        return place;
    }

    // ------------------------------------------------------------------ spot checks

    private static int SeasonAt(GameLocation? loc, Cond c) => c.Season ?? (int)(loc?.GetSeason() ?? Game1.season);

    private static bool RainAt(GameLocation? loc, Cond c) => c.Rain ?? (loc?.IsRainingHere() ?? Game1.isRaining);

    /// <summary>Whether the spot is shown at all, and why it does not bite under these conditions (Now when it does).</summary>
    private static (bool visible, FishBlock block) Evaluate(FishSpot s, GameLocation? loc, Cond c)
    {
        if (s.Boss && !Knowledge.Omniscient && c.Level < s.MinLevel) return (false, FishBlock.Season);
        bool otherOk = s.Other.Count == 0 || OtherTermsHold(s.Other, loc);
        if (!otherOk && (s.SpecialOrder || s.Festival.Length == 0)) return (false, FishBlock.Season);
        return (true, FishRules.Check(s, SeasonAt(loc, c), c.Time, RainAt(loc, c), c.Level, otherOk, CaughtCount(s.FishId)));
    }

    private static int CaughtCount(string qid) =>
        Game1.player.fishCaught.TryGetValue(qid, out var n) && n is { Length: > 0 } ? n[0] : 0;

    private static bool OtherTermsHold(List<string> terms, GameLocation? loc)
    {
        try { return GameStateQuery.CheckConditions(string.Join(", ", terms), loc); }
        catch { return false; }
    }

    private static readonly Dictionary<string, IReadOnlyList<string>> NeedCache = new();

    private static IReadOnlyList<string> Needs(string qid)
    {
        if (!NeedCache.TryGetValue(qid, out var lines))
        {
            try { lines = BundleNeeds.For(qid); }
            catch { lines = Array.Empty<string>(); }
            NeedCache[qid] = lines;
        }
        return lines;
    }

    private static string FishName(string qid)
    {
        try { return ItemRegistry.GetData(qid)?.DisplayName ?? qid; }
        catch { return qid; }
    }

    private static int Price(string qid) =>
        Game1.objectData.TryGetValue(CropMath.Unqualify(qid), out var d) && d != null ? d.Price : 0;

    private static string Caught(string qid) => CaughtCount(qid) > 0 ? "caught" : "not caught";

    private static FishSpec? Spec(string qid) =>
        KnowledgeSources.FishSpecs.TryGetValue(CropMath.Unqualify(qid), out var spec) ? spec : null;

    // ------------------------------------------------------------------ one map

    private static string PlaceReport(string place, GameLocation? loc, Cond c, bool here)
    {
        NeedCache.Clear();
        int season = SeasonAt(loc, c);
        bool rain = RainAt(loc, c);
        var sb = new StringBuilder();
        string conditions = $"{CropMath.SeasonName(season)}, {FishRules.Clock(c.Time)}, {(rain ? "rain" : "sun")}, Fishing {c.Level}";
        string label = place.StartsWith("Mines floor") ? MineLabel(place) : Label(place);

        var spots = KnowledgeSources.FishSpots.TryGetValue(place, out var list) ? list : new List<FishSpot>();
        var byFish = new List<(string fish, List<(FishSpot spot, FishBlock block)> spots)>();
        foreach (var s in spots)
        {
            var (visible, block) = Evaluate(s, loc, c);
            if (!visible) continue;
            int i = byFish.FindIndex(x => x.fish == s.FishId);
            if (i < 0) byFish.Add((s.FishId, new List<(FishSpot, FishBlock)> { (s, block) }));
            else byFish[i].spots.Add((s, block));
        }

        var now = new List<string>();
        var notNow = new List<string>();
        foreach (var (fish, fs) in byFish)
        {
            var bite = fs.Where(x => x.block == FishBlock.Now).Select(x => x.spot).ToList();
            if (bite.Count > 0)
            {
                now.Add(NowLine(fish, bite));
                continue;
            }
            // the regular spot that comes closest to biting (season, then level, weather, hours); festival-only spots last
            var closest = fs.OrderBy(x => x.spot.Festival.Length > 0 ? 1 : 0).ThenByDescending(x => (int)x.block).First();
            notNow.Add($"{FishName(fish)} ({FishRules.BlockText(closest.spot, closest.block)})");
        }

        sb.AppendLine(here ? $"Fish here now ({label}, {conditions}):" : $"Fish in {label} ({conditions}):");
        if (now.Count == 0) sb.AppendLine("- nothing bites under these conditions");
        foreach (var line in now) sb.AppendLine(line);
        if (notNow.Count > 0)
            sb.AppendLine($"Not now: {SourceBook.Names(notNow, MaxNotNow)}");

        if (KnowledgeSources.CrabPotTypes.TryGetValue(place, out var types) && types.Count > 0)
        {
            var catches = KnowledgeSources.FishSpecs.Values.Where(x => x.IsTrap && x.TrapTypes.Any(t => types.Contains(t, StringComparer.OrdinalIgnoreCase)))
                .Select(x => FishName(CropMath.Qualify(x.Id))).Distinct().ToList();
            if (catches.Count > 0)
                sb.AppendLine($"Crab pot {(here ? "here" : "there")}: {string.Join(", ", types.Select(FishRules.CrabWaterText))} -> {string.Join(", ", catches)}");
        }

        var elsewhere = BundleFishElsewhere(place, c, byFish.Where(x => x.spots.Any(s => s.block == FishBlock.Now)).Select(x => x.fish).ToHashSet());
        if (elsewhere.Length > 0) sb.AppendLine(elsewhere);
        return sb.ToString().TrimEnd();
    }

    private static string MineLabel(string place)
    {
        var row = FishRules.MineFishTable.FirstOrDefault(m => FishRules.MineFloorName(m.Floor) == place);
        return row == null ? place : $"{place}, the water of {row.Floors}";
    }

    /// <summary>One "bites now" line from the spots that bite now (the first without an area restriction, else the first).</summary>
    private static string NowLine(string fish, List<FishSpot> bite)
    {
        var s = bite.OrderBy(b => b.Area.Length == 0 ? 0 : 1).First();
        var areas = bite.Any(b => b.Area.Length == 0) ? new List<string>() : bite.Select(b => b.Area).Distinct().ToList();
        var spec = Spec(fish);
        var parts = new List<string>
        {
            $"{FishName(fish)} {Price(fish)}g",
            FishRules.Hours(s.Times),
            FishRules.WeatherText(s.Weather)
        };
        if (spec != null && !spec.IsTrap) parts.Add($"diff {spec.Difficulty} {spec.Behavior}");
        if (areas.Count > 0) parts.Add($"{string.Join("/", areas)} only");
        if (s.Hint.Length > 0) parts.Add(s.Hint);
        if (s.MinLevel > 0) parts.Add($"Fishing {s.MinLevel}+");
        if (s.Boss) parts.Add("legendary");
        if (s.Festival.Length > 0) parts.Add("during " + FishRules.Words(s.Festival));
        if (FishRules.LimitText(s.CatchLimit) is { Length: > 0 } limit) parts.Add(limit);
        var needs = Needs(fish);
        if (needs.Count > 0) parts.Add("NEEDED: " + needs[0] + (needs.Count > 1 ? $" (+{needs.Count - 1} more)" : ""));
        parts.Add(Caught(fish));
        return "- " + string.Join(" | ", parts);
    }

    /// <summary>"Bundle fish biting now elsewhere: Catfish (Town river, Woods); ..." for needed fish that do not bite here now.</summary>
    private static string BundleFishElsewhere(string place, Cond c, HashSet<string> bitingHere)
    {
        var found = new List<(string fish, List<string> places)>();
        foreach (var kv in KnowledgeSources.FishSpots)
        {
            if (kv.Key == place) continue;
            GameLocation? loc = null;
            foreach (var s in kv.Value)
            {
                if (bitingHere.Contains(s.FishId) || Needs(s.FishId).Count == 0) continue;
                loc ??= Instance(kv.Key);
                var (visible, block) = Evaluate(s, loc, c);
                if (!visible || block != FishBlock.Now) continue;
                int i = found.FindIndex(x => x.fish == s.FishId);
                if (i < 0) found.Add((s.FishId, new List<string> { s.Place }));
                else if (!found[i].places.Contains(s.Place)) found[i].places.Add(s.Place);
            }
        }
        if (found.Count == 0) return "";
        var items = found.Take(MaxBundleFish).Select(f => $"{FishName(f.fish)} ({SourceBook.Names(f.places, 3)})");
        return "Bundle fish biting now elsewhere: " + string.Join("; ", items) + (found.Count > MaxBundleFish ? $"; and {found.Count - MaxBundleFish} more" : "");
    }

    // ------------------------------------------------------------------ one fish

    private static (bool ok, string text) FishReport(string query, Cond c, List<string>? onlyPlaces)
    {
        NeedCache.Clear();
        var (matches, exact) = FindFish(query);
        if (matches.Count == 0) return (false, $"No fish called \"{query}\". Check the spelling, or call fish_info without fish to see what bites here.");
        if (!exact && matches.Count > 1)
            return (false, $"Several fish match \"{query}\": {string.Join(", ", matches.Select(FishName).Distinct().OrderBy(n => n).Take(MaxMaps))}. Ask again with the full name.");
        string qid = matches[0];
        var spec = Spec(qid);
        var sb = new StringBuilder();

        var head = new List<string> { $"{FishName(qid)} {Price(qid)}g" };
        if (spec != null && !spec.IsTrap) head.Add($"diff {spec.Difficulty} {spec.Behavior}");
        head.Add(Caught(qid));
        sb.AppendLine(string.Join(" | ", head));
        var needs = Needs(qid);
        if (needs.Count > 0) sb.AppendLine("NEEDED: " + string.Join("; ", needs));

        if (spec is { IsTrap: true })
        {
            foreach (var type in spec.TrapTypes)
            {
                var maps = FishRules.ValleyFirst(KnowledgeSources.CrabPotTypes.Where(kv => kv.Value.Contains(type, StringComparer.OrdinalIgnoreCase)).Select(kv => kv.Key));
                sb.AppendLine($"Crab pot catch, not a rod fish: any {FishRules.CrabWaterText(type)}{(maps.Count > 0 ? $" ({SourceBook.Names(maps, 8)})" : "")}. Bait the pot; it fills overnight, any season.");
            }
        }

        var lines = new List<string>();
        var biting = new List<string>();
        var blocks = new List<string>();
        bool hiddenBoss = false, onlyLimited = true;
        int globalSeason = c.Season ?? (int)Game1.season;
        foreach (var kv in KnowledgeSources.FishSpots)
        {
            if (onlyPlaces != null && !onlyPlaces.Contains(kv.Key)) continue;
            GameLocation? loc = null;
            foreach (var s in kv.Value.Where(x => x.FishId == qid))
            {
                loc ??= Instance(kv.Key);
                var (visible, block) = Evaluate(s, loc, c);
                if (!visible)
                {
                    if (s.Boss) hiddenBoss = true;
                    continue;
                }
                var parts = new List<string> { SeasonMask.Long(s.Seasons), FishRules.Hours(s.Times), FishRules.WeatherText(s.Weather) };
                if (s.MinLevel > 0) parts.Add($"Fishing {s.MinLevel}+");
                if (s.Hint.Length > 0) parts.Add(s.Hint);
                if (s.MagicBait) parts.Add("Magic Bait only");
                if (s.Festival.Length > 0) parts.Add("during " + FishRules.Words(s.Festival));
                if (s.Boss) parts.Add("legendary");
                if (FishRules.LimitText(s.CatchLimit) is { Length: > 0 } limit) parts.Add(limit);
                lines.Add($"- {s.Place}: {string.Join(" | ", parts)}");
                if (block != FishBlock.CaughtLimit) onlyLimited = false;
                if (block == FishBlock.Now) { if (!biting.Contains(s.Place)) biting.Add(s.Place); }
                else blocks.Add($"{s.Place} {FishRules.BlockText(s, block)}");
            }
        }

        if (lines.Count > 0)
        {
            sb.AppendLine("Where and when it bites:");
            foreach (var l in lines) sb.AppendLine(l);
            string when = $"{CropMath.SeasonName(globalSeason)} {FishRules.Clock(c.Time)}{(c.Rain is bool r ? r ? ", rain" : ", sun" : "")}, Fishing {c.Level}";
            sb.AppendLine(biting.Count > 0
                ? $"Bites now ({when}) at: {SourceBook.Names(biting, 8)}."
                : onlyLimited
                    ? "Already caught as many times as the game allows (legendary fish: once); it will not bite again."
                    : $"Not biting anywhere now ({when}); needs: {SourceBook.Names(blocks.Distinct().ToList(), 6)}.");
        }
        else if (hiddenBoss)
            sb.AppendLine("A legendary fish: where it bites shows here once your Fishing level is high enough.");
        else if (spec is not { IsTrap: true })
            sb.AppendLine(onlyPlaces != null ? $"It does not bite in {string.Join(", ", onlyPlaces)}." : "No map lists it as a rod catch.");

        var other = KnowledgeSources.For(qid).Where(l => !l.StartsWith("fish:") && !l.StartsWith("crab pot:")).ToList();
        if (other.Count > 0) sb.AppendLine("Also: " + string.Join(" | ", other));
        return (true, sb.ToString().TrimEnd());
    }

    /// <summary>Fish (objects of type Fish, not trash) whose shown name, English name or id equals the query; else those containing it.</summary>
    private static (List<string> matches, bool exact) FindFish(string query)
    {
        string q = query.Trim().ToLowerInvariant(), id = CropMath.Unqualify(query.Trim()).ToLowerInvariant();
        var exact = new List<string>();
        var containing = new List<string>();
        foreach (var kv in Game1.objectData)
        {
            string qid = CropMath.Qualify(kv.Key);
            if (kv.Value == null || !KnowledgeSources.IsFish(qid)) continue;
            string display = FishName(qid).ToLowerInvariant(), name = (kv.Value.Name ?? "").ToLowerInvariant();
            if (display == q || name == q || kv.Key.ToLowerInvariant() == id) exact.Add(qid);
            else if (display.Contains(q) || name.Contains(q)) containing.Add(qid);
        }
        return exact.Count > 0 ? (exact, true) : (containing, false);
    }
}
