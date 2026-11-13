using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace StardewMCP.Agent;

/// <summary>Seasons as a bit mask: 1 spring, 2 summer, 4 fall, 8 winter. Game-free.</summary>
public static class SeasonMask
{
    public const int None = 0, Spring = 1, Summer = 2, Fall = 4, Winter = 8, All = 15;

    /// <summary>The bit of a season index (0 spring .. 3 winter), 0 when out of range.</summary>
    public static int Of(int seasonIndex) => seasonIndex is >= 0 and <= 3 ? 1 << seasonIndex : 0;

    /// <summary>The bit of a season name ("spring", "Winter"...), 0 when it is not a season.</summary>
    public static int Of(string? name) => Of(CropMath.SeasonIndex(name));

    public static bool Has(int mask, int seasonIndex) => (mask & Of(seasonIndex)) != 0;

    /// <summary>"spring/fall"; "" for every season or none.</summary>
    public static string Text(int mask)
    {
        mask &= All;
        if (mask == All || mask == None) return "";
        var parts = new List<string>();
        for (int i = 0; i < 4; i++)
            if ((mask & (1 << i)) != 0) parts.Add(CropMath.SeasonName(i));
        return string.Join("/", parts);
    }

    /// <summary>"spring/fall", "every season" for all four, "no season" for none.</summary>
    public static string Long(int mask) => (mask & All) == All ? "every season" : (mask & All) == None ? "no season" : Text(mask);
}

public enum FishWeather { Any, Rain, Sun }

/// <summary>A fishing time window in game time (600 = 6:00, 2600 = 2:00 next morning). <see cref="End"/> is exclusive unless <see cref="Inclusive"/>.</summary>
public readonly record struct FishWindow(int Start, int End, bool Inclusive = false)
{
    public bool Contains(int time) => time >= Start && (Inclusive ? time <= End : time < End);
}

/// <summary>One Data/Fish entry, parsed. Trap entries (crab pot catches) only carry <see cref="TrapTypes"/>.</summary>
public sealed class FishSpec
{
    public string Id = "", Name = "", Behavior = "";
    public int Difficulty, MinLevel;
    public List<FishWindow> Times = new();
    public FishWeather Weather;
    public bool IsTrap;
    public List<string> TrapTypes = new();
}

/// <summary>What a Data/Locations condition says about season, weather and time, and what is left for the game to check.</summary>
public sealed class ConditionInfo
{
    public int Seasons = SeasonMask.All;
    public FishWeather Weather;
    public List<FishWindow> Times = new();
    /// <summary>The passive festival the entry needs (IS_PASSIVE_FESTIVAL_OPEN), or "".</summary>
    public string Festival = "";
    /// <summary>The entry needs a special order (or its rule) to be active, e.g. the Extended Family quest's legendary fish.</summary>
    public bool SpecialOrder;
    /// <summary>Terms that are neither season, weather, time nor chance; the game checks them (GameStateQuery).</summary>
    public List<string> Other = new();
}

/// <summary>Where and when one fish bites in one map (or area of a map).</summary>
public sealed class FishSpot
{
    public string FishId = "", Location = "", Area = "", Hint = "";
    public int Seasons = SeasonMask.All, MinLevel;
    /// <summary>How many times a player can ever catch it here (SpawnFishData.CatchLimit: 1 for legendary fish); -1 no limit.</summary>
    public int CatchLimit = -1;
    public List<FishWindow> Times = new();
    public FishWeather Weather;
    public bool Boss, MagicBait;
    public string Festival = "";
    public bool SpecialOrder;
    public List<string> Other = new();

    /// <summary>"Forest river", "Beach".</summary>
    public string Place => Area.Length > 0 ? $"{Location} {Area}" : Location;
}

/// <summary>
/// Why a fish does not bite under the given conditions; <see cref="Now"/> when it does. Ordered from the farthest from
/// biting (<see cref="CaughtLimit"/>: never again) to the closest (<see cref="MagicBait"/>).
/// </summary>
public enum FishBlock { Now, CaughtLimit, Season, Level, Festival, Weather, Time, MagicBait }

/// <summary>A fish caught only on hard-coded mine floors (MineShaft.getFish, not Data/Locations).</summary>
public sealed record MineFish(string FishId, int MineArea, int Floor, string Floors);

/// <summary>
/// Fishing rules read from Data/Fish and Data/Locations strings, with no game types so they can be tested:
/// time windows, condition terms, merging a spawn entry with its Data/Fish line, why a fish does not bite now,
/// and parsing the fish_info arguments (location, time, weather).
/// </summary>
public static class FishRules
{
    // ------------------------------------------------------------------ Data/Fish

    /// <summary>Parse a Data/Fish line: "name/difficulty/behavior/min/max/times/seasons/weather/locations/depth/chance/depthMult/minLevel/tutorial", or "name/trap/chance/bait/types/...".</summary>
    public static FishSpec? ParseFish(string id, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var f = raw.Split('/');
        var spec = new FishSpec { Id = id, Name = f[0] };
        if (f.Length > 1 && f[1] == "trap")
        {
            spec.IsTrap = true;
            if (f.Length > 4) spec.TrapTypes.AddRange(f[4].Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return spec;
        }
        if (f.Length < 8) return null;
        spec.Difficulty = Int(f[1]);
        spec.Behavior = f[2];
        spec.Times = ParseWindows(f[5]);
        spec.Weather = f[7] switch { "rainy" => FishWeather.Rain, "sunny" => FishWeather.Sun, _ => FishWeather.Any };
        spec.MinLevel = f.Length > 12 ? Int(f[12]) : 0;
        return spec;
    }

    /// <summary>"600 1100 1900 2600" -> [6:00-11:00), [19:00-2:00).</summary>
    public static List<FishWindow> ParseWindows(string? raw)
    {
        var list = new List<FishWindow>();
        var t = (raw ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i + 1 < t.Length; i += 2)
            if (int.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int a) &&
                int.TryParse(t[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int b))
                list.Add(new FishWindow(a, b));
        return list;
    }

    private static int Int(string s) => int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;

    /// <summary>No windows, or one covering the whole fishing day.</summary>
    public static bool AnyTime(IReadOnlyList<FishWindow> times) =>
        times.Count == 0 || times.Any(w => w.Start <= 600 && (w.End >= 2600 || (w.Inclusive && w.End >= 2550)));

    public static bool InHours(IReadOnlyList<FishWindow> times, int time) => times.Count == 0 || times.Any(w => w.Contains(time));

    /// <summary>600 -> "6:00", 1430 -> "14:30", 2400 -> "24:00" (midnight), 2600 -> "2:00"; compact drops ":00" ("6").</summary>
    public static string Clock(int time, bool compact = false)
    {
        int h = time / 100, m = time % 100;
        if (h > 24 || h == 24 && m > 0) h -= 24;
        return compact && m == 0 ? h.ToString(CultureInfo.InvariantCulture) : $"{h}:{m:00}";
    }

    /// <summary>"6:00-19:00", "6:00-11:00, 19:00-2:00", "any time"; compact "6-11, 19-2".</summary>
    public static string Hours(IReadOnlyList<FishWindow> times, bool compact = false)
    {
        if (AnyTime(times)) return "any time";
        return string.Join(", ", times.Select(w => $"{Clock(w.Start, compact)}-{Clock(w.End, compact)}"));
    }

    public static string WeatherText(FishWeather w) => w switch { FishWeather.Rain => "rain", FishWeather.Sun => "sun", _ => "any weather" };

    // ------------------------------------------------------------------ conditions

    /// <summary>Split a game state query into its comma-separated terms (commas inside quotes stay).</summary>
    public static List<string> Terms(string? condition)
    {
        var terms = new List<string>();
        if (string.IsNullOrWhiteSpace(condition)) return terms;
        var sb = new StringBuilder();
        bool quoted = false;
        foreach (char ch in condition)
        {
            if (ch == '"') quoted = !quoted;
            if (ch == ',' && !quoted)
            {
                if (sb.ToString().Trim().Length > 0) terms.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else sb.Append(ch);
        }
        if (sb.ToString().Trim().Length > 0) terms.Add(sb.ToString().Trim());
        return terms;
    }

    private static readonly HashSet<string> RainWeathers = new(StringComparer.OrdinalIgnoreCase) { "Rain", "Storm", "GreenRain" };

    /// <summary>Read season (SEASON, LOCATION_SEASON), weather (WEATHER), time (TIME) and chance (RANDOM...) terms; keep the rest in <see cref="ConditionInfo.Other"/>.</summary>
    public static ConditionInfo ParseCondition(string? condition)
    {
        var info = new ConditionInfo();
        foreach (var term in Terms(condition))
        {
            bool neg = term.StartsWith("!");
            var parts = (neg ? term.Substring(1) : term).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            string key = parts[0].ToUpperInvariant();
            switch (key)
            {
                case "SEASON":
                case "LOCATION_SEASON":
                {
                    int first = key == "SEASON" ? 1 : 2, mask = 0;
                    for (int i = first; i < parts.Length; i++) mask |= SeasonMask.Of(parts[i]);
                    info.Seasons &= neg ? SeasonMask.All & ~mask : mask;
                    break;
                }
                case "WEATHER":
                {
                    var w = parts.Skip(2).ToList();
                    if (w.Count == 0) { info.Other.Add(term); break; }
                    bool allRain = w.All(RainWeathers.Contains), noRain = !w.Any(RainWeathers.Contains);
                    var weather = allRain ? FishWeather.Rain : noRain ? FishWeather.Sun : FishWeather.Any;
                    if (neg) weather = weather == FishWeather.Rain ? FishWeather.Sun : weather == FishWeather.Sun ? FishWeather.Rain : FishWeather.Any;
                    if (weather != FishWeather.Any) info.Weather = weather;
                    break;
                }
                case "TIME" when !neg && parts.Length >= 2:
                {
                    int a = Int(parts[1]), b = parts.Length >= 3 ? Int(parts[2]) : 2600;
                    info.Times.Add(new FishWindow(a, b, Inclusive: true));
                    break;
                }
                case "RANDOM":
                case "SYNCED_RANDOM":
                case "SYNCED_CHOICE":
                case "SYNCED_SUMMER_RAIN_RANDOM":
                    break; // a chance, not a rule
                default:
                    if (key == "IS_PASSIVE_FESTIVAL_OPEN" && !neg && parts.Length >= 2) info.Festival = parts[1];
                    if (key.StartsWith("PLAYER_SPECIAL_ORDER") && !neg) info.SpecialOrder = true;
                    info.Other.Add(term);
                    break;
            }
        }
        return info;
    }

    /// <summary>
    /// One spawn entry of Data/Locations merged with its Data/Fish line, the way GameLocation.GetFishFromLocationData
    /// and CheckGenericFishRequirements do: the season comes from the entry (Data/Fish seasons are not used in 1.6);
    /// hours, weather and level come from Data/Fish unless the entry ignores it.
    /// </summary>
    public static FishSpot MakeSpot(string fishId, string location, string area, int? spawnSeason, string? condition,
        int spawnMinLevel, bool ignoreFishData, FishSpec? spec, bool boss, bool magicBait, string hint = "", int catchLimit = -1)
    {
        var c = ParseCondition(condition);
        bool useSpec = !ignoreFishData && spec != null && !spec.IsTrap;
        var spot = new FishSpot
        {
            FishId = fishId, Location = location, Area = area, Hint = hint,
            Seasons = c.Seasons & (spawnSeason.HasValue ? SeasonMask.Of(spawnSeason.Value) : SeasonMask.All),
            Times = c.Times.Count > 0 ? c.Times : useSpec ? spec!.Times : new List<FishWindow>(),
            Weather = c.Weather != FishWeather.Any ? c.Weather : useSpec ? spec!.Weather : FishWeather.Any,
            MinLevel = Math.Max(spawnMinLevel, useSpec ? spec!.MinLevel : 0),
            Boss = boss, MagicBait = magicBait, CatchLimit = catchLimit < 0 ? -1 : catchLimit,
            Festival = c.Festival, SpecialOrder = c.SpecialOrder, Other = c.Other
        };
        return spot;
    }

    /// <summary>
    /// Why the spot gives no fish under these conditions, checked in the order a player can act on. <paramref name="caught"/>
    /// is how many the player has caught (Farmer.fishCaught): at the spot's catch limit the game never gives it again.
    /// </summary>
    public static FishBlock Check(FishSpot s, int season, int time, bool rain, int level, bool festivalOpen = false, int caught = 0)
    {
        if (LimitReached(s.CatchLimit, caught)) return FishBlock.CaughtLimit;
        if (!SeasonMask.Has(s.Seasons, season)) return FishBlock.Season;
        if (level < s.MinLevel) return FishBlock.Level;
        if (s.Festival.Length > 0 && !festivalOpen) return FishBlock.Festival;
        if (s.Weather == FishWeather.Rain && !rain || s.Weather == FishWeather.Sun && rain) return FishBlock.Weather;
        if (!InHours(s.Times, time)) return FishBlock.Time;
        if (s.MagicBait) return FishBlock.MagicBait;
        return FishBlock.Now;
    }

    /// <summary>GameLocation.GetFishFromLocationData: a spawn with CatchLimit > -1 is skipped once the player caught that many.</summary>
    public static bool LimitReached(int catchLimit, int caught) => catchLimit > -1 && caught >= catchLimit;

    /// <summary>"once only" for a catch limit of 1, "at most 3 catches" for more, "" for none.</summary>
    public static string LimitText(int catchLimit) => catchLimit switch
    {
        < 0 => "",
        1 => "once only",
        _ => $"at most {catchLimit} catches"
    };

    /// <summary>Short reason for the "Not now" list: "summer/winter", "rain", "6-11, 19-2", "Fishing 5", "Magic Bait".</summary>
    public static string BlockText(FishSpot s, FishBlock b) => b switch
    {
        FishBlock.CaughtLimit => s.CatchLimit == 1 ? "already caught, once only" : $"caught {s.CatchLimit} already, no more",
        FishBlock.Season => SeasonMask.Long(s.Seasons),
        FishBlock.Level => $"Fishing {s.MinLevel}",
        FishBlock.Festival => $"during {Words(s.Festival)}",
        FishBlock.Weather => WeatherText(s.Weather),
        FishBlock.Time => Hours(s.Times, compact: true),
        FishBlock.MagicBait => "Magic Bait",
        _ => "now"
    };

    /// <summary>
    /// Drop spots another spot of the same fish and map already covers (same or wider area, seasons, hours, weather,
    /// level), and exact duplicates. Forest Salmon "fall, any time" covers Forest river "fall, 6:00-19:00".
    /// </summary>
    public static List<FishSpot> Simplify(IEnumerable<FishSpot> spots)
    {
        var list = spots.ToList();
        var keep = new List<FishSpot>();
        for (int i = 0; i < list.Count; i++)
        {
            var b = list[i];
            bool covered = false;
            for (int j = 0; j < list.Count && !covered; j++)
            {
                if (i == j) continue;
                var a = list[j];
                if (!Covers(a, b)) continue;
                covered = !Covers(b, a) || j < i; // equal spots: keep the first
            }
            if (!covered) keep.Add(b);
        }
        return keep;
    }

    private static bool Covers(FishSpot a, FishSpot b) =>
        a.FishId == b.FishId && a.Location == b.Location &&
        (a.Area.Length == 0 || a.Area == b.Area) &&
        (a.Seasons & b.Seasons) == b.Seasons &&
        (AnyTime(a.Times) || Hours(a.Times) == Hours(b.Times)) &&
        (a.Weather == FishWeather.Any || a.Weather == b.Weather) &&
        a.MinLevel <= b.MinLevel &&
        !a.MagicBait | b.MagicBait &&
        (a.Festival.Length == 0 || a.Festival == b.Festival) &&
        !a.SpecialOrder | b.SpecialOrder &&
        a.Other.Count == 0 && a.Hint.Length == 0 | a.Hint == b.Hint &&
        (a.CatchLimit < 0 || b.CatchLimit >= 0 && a.CatchLimit >= b.CatchLimit);

    /// <summary>A spawn rectangle as advice: "stand at x 82-181, y 0-99" (PlayerPosition) or "bobber in x ..., y ..." (BobberPosition), plus "cast 3+ tiles out".</summary>
    public static string SpotHint((int x, int y, int w, int h)? player, (int x, int y, int w, int h)? bobber, int minDistanceFromShore)
    {
        var parts = new List<string>();
        if (player is { } p) parts.Add($"stand at x {p.x}-{p.x + p.w - 1}, y {p.y}-{p.y + p.h - 1}");
        if (bobber is { } b) parts.Add($"bobber in x {b.x}-{b.x + b.w - 1}, y {b.y}-{b.y + b.h - 1}");
        if (minDistanceFromShore > 0) parts.Add($"cast {minDistanceFromShore}+ tiles from shore");
        return string.Join(", ", parts);
    }

    /// <summary>"TopPond" -> "top pond", "River" -> "river", "Default" -> "".</summary>
    public static string AreaLabel(string? areaId)
    {
        if (string.IsNullOrWhiteSpace(areaId) || areaId == "Default") return "";
        return Words(areaId).ToLowerInvariant();
    }

    /// <summary>"TroutDerby" -> "Trout Derby", "Festival_Name" -> "Festival Name".</summary>
    public static string Words(string? id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        var sb = new StringBuilder();
        for (int i = 0; i < id.Length; i++)
        {
            char c = id[i];
            if (c == '_') { sb.Append(' '); continue; }
            if (i > 0 && char.IsUpper(c) && (char.IsLower(id[i - 1]) || char.IsDigit(id[i - 1]))) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    // ------------------------------------------------------------------ crab pots

    /// <summary>A crab pot fish type as words: "freshwater" -> "fresh water", "ocean" -> "ocean water".</summary>
    public static string CrabWaterText(string type) =>
        type.Equals("freshwater", StringComparison.OrdinalIgnoreCase) ? "fresh water" : $"{type.ToLowerInvariant()} water";

    private static readonly string[] ValleyMaps = { "Town", "Forest", "Mountain", "Beach", "Farm" };

    /// <summary>Map names with the main valley maps first (Town, Forest, Mountain, Beach, Farm), then the rest alphabetically.</summary>
    public static List<string> ValleyFirst(IEnumerable<string> maps) =>
        maps.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(m => Array.FindIndex(ValleyMaps, v => v.Equals(m, StringComparison.OrdinalIgnoreCase)) is int i && i >= 0 ? i : ValleyMaps.Length)
            .ThenBy(m => m, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// Festival copies of a map back to the map whose fish they borrow (Data/PassiveFestivals MapReplacements, whose
    /// Data/Locations entries are LOCATION_FISH of the original): BeachNightMarket -> Beach, DesertFestival -> Desert.
    /// </summary>
    public static string BaseMap(string name) => name switch
    {
        "BeachNightMarket" => "Beach",
        "DesertFestival" => "Desert",
        _ => name
    };

    // ------------------------------------------------------------------ mines

    /// <summary>
    /// Fish the mines give from code, not data (MineShaft.getFish, 1.6.15): Stonefish in mine area 0 (floors 1-39, water on
    /// floor 20), Ice Pip in area 40 (water on floor 60), Lava Eel in area 80 (lava on floor 100). No Data/Fish hour,
    /// weather or level rule applies to them. Areas 0 and 40 also give the UndergroundMine data fish; area 80 only Cave Jelly and trash.
    /// </summary>
    public static readonly IReadOnlyList<MineFish> MineFishTable = new[]
    {
        new MineFish("(O)158", 0, 20, "floors 1-39"),
        new MineFish("(O)161", 40, 60, "floors 40-79"),
        new MineFish("(O)162", 80, 100, "floors 80-119")
    };

    /// <summary>Cave Jelly comes from code on floors 80-119 (the other mine floors get it from Data/Locations UndergroundMine).</summary>
    public const string CaveJellyId = "(O)CaveJelly";

    /// <summary>MineShaft.getMineArea for a floor of the regular mines and Skull Cavern (no quarry): 0, 10, 40, 80 or 121.</summary>
    public static int MineArea(int level)
    {
        if (level >= 80 && level <= 120) return 80;
        if (level > 120) return 121;
        if (level >= 40) return 40;
        if (level > 10 && level < 30) return 10;
        return 0;
    }

    /// <summary>The mine-fish row for a mine area (10 counts as 0); null when the area has none (Skull Cavern).</summary>
    public static MineFish? MineFishFor(int area) => MineFishTable.FirstOrDefault(m => m.MineArea == (area == 10 ? 0 : area));

    public static string MineFloorName(int floor) => $"Mines floor {floor}";

    // ------------------------------------------------------------------ arguments

    private static string Norm(string? s)
    {
        var sb = new StringBuilder();
        foreach (char c in (s ?? "").ToLowerInvariant())
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        var n = sb.ToString();
        return n.StartsWith("the") && n.Length > 6 ? n.Substring(3) : n;
    }

    /// <summary>Everyday names for fishing maps, normalized (lower case, letters and digits only) -> internal location name.</summary>
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["pelicantown"] = "Town", ["town"] = "Town", ["townriver"] = "Town",
        ["cindersapforest"] = "Forest", ["cindersap"] = "Forest", ["forestriver"] = "Forest", ["forestpond"] = "Forest",
        ["secretwoods"] = "Woods", ["woods"] = "Woods",
        ["witchswamp"] = "WitchSwamp", ["witchsswamp"] = "WitchSwamp", ["swamp"] = "WitchSwamp",
        ["mutantbuglair"] = "BugLand", ["buglair"] = "BugLand",
        ["sewers"] = "Sewer", ["sewer"] = "Sewer",
        ["calicodesert"] = "Desert", ["desert"] = "Desert",
        ["volcano"] = "Caldera", ["volcanodungeon"] = "Caldera", ["caldera"] = "Caldera",
        ["mountainlake"] = "Mountain", ["lake"] = "Mountain", ["mountains"] = "Mountain",
        ["ocean"] = "Beach", ["sea"] = "Beach", ["pier"] = "Beach",
        ["farmpond"] = "Farm", ["pond"] = "Farm",
        ["nightmarket"] = "Submarine", ["submarine"] = "Submarine",
        ["gingerisland"] = "IslandSouth", ["island"] = "IslandSouth"
    };

    /// <summary>
    /// A mine floor from "Mines", "mine 60", "UndergroundMine20", "floor 100": the floor number, 0 for the mines in general,
    /// null when the text is not about the mines.
    /// </summary>
    public static int? ParseMineFloor(string? query)
    {
        var n = Norm(query);
        foreach (var prefix in new[] { "undergroundmine", "minesfloor", "minefloor", "mines", "mine", "floor" })
        {
            if (!n.StartsWith(prefix)) continue;
            var rest = n.Substring(prefix.Length);
            if (rest.Length == 0) return prefix == "floor" ? null : 0;
            return int.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out int f) && f > 0 ? f : null;
        }
        return null;
    }

    /// <summary>
    /// Resolve a location the player typed against (internal name, display name) pairs: an alias, then an exact internal or
    /// display name, then a unique prefix, then a unique part. Returns the internal name, or null with the close candidates.
    /// </summary>
    public static (string? name, List<string> candidates) ResolveLocation(string? query, IEnumerable<(string Name, string Display)> places)
    {
        var all = places.ToList();
        var q = Norm(query);
        if (q.Length == 0) return (null, new List<string>());
        var exact = all.Where(p => Norm(p.Name) == q || Norm(p.Display) == q).Select(p => p.Name).Distinct().ToList();
        if (exact.Count == 1) return (exact[0], exact);
        if (Aliases.TryGetValue(q, out var alias) && all.Any(p => p.Name == alias)) return (alias, new List<string> { alias });
        if (exact.Count > 1) return (null, exact);
        var prefix = all.Where(p => Norm(p.Name).StartsWith(q) || Norm(p.Display).StartsWith(q)).Select(p => p.Name).Distinct().ToList();
        if (prefix.Count == 1) return (prefix[0], prefix);
        var part = prefix.Count > 0 ? prefix : all.Where(p => Norm(p.Name).Contains(q) || Norm(p.Display).Contains(q)).Select(p => p.Name).Distinct().ToList();
        return part.Count == 1 ? (part[0], part) : (null, part);
    }

    /// <summary>
    /// A time of day as game time: "14:20" -> 1420, "2pm" -> 1400, "6am" -> 600, "1am" -> 2500, "midnight" -> 2400,
    /// "1:30" -> 2530 (hours 0-2 are after midnight), "1420" -> 1420. Minutes round down to 10. Null when unreadable or
    /// outside 6:00-2:00.
    /// </summary>
    public static int? ParseTime(string? text)
    {
        var s = (text ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace(".", "");
        if (s.Length == 0) return null;
        if (s == "midnight") return 2400;
        if (s == "noon") return 1200;
        bool am = s.EndsWith("am"), pm = s.EndsWith("pm");
        if (am || pm) s = s.Substring(0, s.Length - 2);
        int h, m = 0;
        int colon = s.IndexOf(':');
        if (colon >= 0)
        {
            if (!int.TryParse(s.Substring(0, colon), NumberStyles.None, CultureInfo.InvariantCulture, out h) ||
                !int.TryParse(s.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out m)) return null;
        }
        else
        {
            if (!int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out int n)) return null;
            if (s.Length >= 3) { h = n / 100; m = n % 100; }
            else h = n;
        }
        if (m < 0 || m > 59) return null;
        if (am || pm)
        {
            if (h < 1 || h > 12) return null;
            if (pm && h != 12) h += 12;
            if (am && h == 12) h = 24;
            else if (am && h < 6) h += 24;
        }
        else if (h < 6) h += 24;
        int t = h * 100 + m / 10 * 10;
        return t >= 600 && t <= 2600 ? t : null;
    }

    /// <summary>true for rain (rain, storm, green rain), false for dry weather (sun, snow, wind), null when unknown.</summary>
    public static bool? ParseWeather(string? text)
    {
        switch (Norm(text))
        {
            case "rain": case "rainy": case "raining": case "storm": case "stormy": case "thunder": case "thunderstorm":
            case "greenrain": case "wet":
                return true;
            case "sun": case "sunny": case "clear": case "dry": case "snow": case "snowy": case "snowing": case "wind": case "windy":
            case "fine": case "cloudy":
                return false;
            default:
                return null;
        }
    }
}
