using System.Collections.Generic;
using System.Linq;
using Xunit;
using StardewMCP.Agent;

namespace StardewMCP.Tests;

public class FishRulesTests
{
    // Data/Fish lines from the 1.6.15 dump
    private const string Catfish = "Catfish/75/mixed/12/72/600 2400/spring fall winter/rainy/689 .4 680 .1/4/.4/.1/0/true";
    private const string Halibut = "Halibut/50/sinker/10/33/600 1100 1900 2600/spring summer winter/both/681 .35/3/.4/.2/0/false";
    private const string Crimsonfish = "Crimsonfish/95/mixed/20/20/600 2000/winter/both/690 .15/4/.1/.1/5/false";
    private const string Lobster = "Lobster/trap/.05/688 .45 689 .35 690 .35/ocean/2/20/false";
    private const int Spring = 0, Summer = 1, Fall = 2, Winter = 3;

    // ------------------------------------------------------------------ Data/Fish

    [Fact]
    public void Parses_a_rod_fish()
    {
        var f = FishRules.ParseFish("143", Catfish)!;
        Assert.False(f.IsTrap);
        Assert.Equal("Catfish", f.Name);
        Assert.Equal(75, f.Difficulty);
        Assert.Equal("mixed", f.Behavior);
        Assert.Equal(FishWeather.Rain, f.Weather);
        Assert.Equal(0, f.MinLevel);
        Assert.Equal("6:00-24:00", FishRules.Hours(f.Times));
    }

    [Fact]
    public void Parses_two_time_windows()
    {
        var f = FishRules.ParseFish("708", Halibut)!;
        Assert.Equal(FishWeather.Any, f.Weather);
        Assert.Equal("6:00-11:00, 19:00-2:00", FishRules.Hours(f.Times));
        Assert.Equal("6-11, 19-2", FishRules.Hours(f.Times, compact: true));
        Assert.True(FishRules.InHours(f.Times, 1050));
        Assert.False(FishRules.InHours(f.Times, 1100));
        Assert.True(FishRules.InHours(f.Times, 2550));
    }

    [Fact]
    public void Parses_a_crab_pot_entry()
    {
        var f = FishRules.ParseFish("715", Lobster)!;
        Assert.True(f.IsTrap);
        Assert.Equal(new[] { "ocean" }, f.TrapTypes);
    }

    [Fact]
    public void Min_level_is_field_12()
    {
        Assert.Equal(5, FishRules.ParseFish("159", Crimsonfish)!.MinLevel);
    }

    [Theory]
    [InlineData(600, false, "6:00")]
    [InlineData(1430, false, "14:30")]
    [InlineData(2600, false, "2:00")]
    [InlineData(2400, true, "24")]
    [InlineData(2430, false, "0:30")]
    [InlineData(1900, true, "19")]
    public void Clock_text(int time, bool compact, string expected)
    {
        Assert.Equal(expected, FishRules.Clock(time, compact));
    }

    [Fact]
    public void Whole_day_is_any_time()
    {
        Assert.Equal("any time", FishRules.Hours(FishRules.ParseWindows("600 2600")));
        Assert.Equal("any time", FishRules.Hours(new List<FishWindow>()));
    }

    // ------------------------------------------------------------------ conditions

    [Fact]
    public void Location_season_terms()
    {
        var c = FishRules.ParseCondition("LOCATION_SEASON Here spring fall winter");
        Assert.Equal(SeasonMask.Spring | SeasonMask.Fall | SeasonMask.Winter, c.Seasons);
        Assert.Empty(c.Other);
    }

    [Fact]
    public void Negated_season_is_the_complement()
    {
        Assert.Equal(SeasonMask.All & ~SeasonMask.Winter, FishRules.ParseCondition("!SEASON winter").Seasons);
    }

    [Fact]
    public void Legend_condition_rain_and_special_order_check()
    {
        var c = FishRules.ParseCondition("!PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY, WEATHER Here Rain Storm GreenRain");
        Assert.Equal(FishWeather.Rain, c.Weather);
        Assert.False(c.SpecialOrder); // negated: the normal Legend
        Assert.Single(c.Other);
    }

    [Fact]
    public void Positive_special_order_is_flagged()
    {
        Assert.True(FishRules.ParseCondition("PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY").SpecialOrder);
    }

    [Fact]
    public void Festival_and_time_terms()
    {
        var c = FishRules.ParseCondition("IS_PASSIVE_FESTIVAL_OPEN SquidFest, TIME 0600 1800");
        Assert.Equal("SquidFest", c.Festival);
        Assert.Single(c.Times);
        Assert.True(c.Times[0].Contains(1800));
        Assert.False(c.Times[0].Contains(1810));
        Assert.Equal("6:00-18:00", FishRules.Hours(c.Times));
    }

    [Fact]
    public void Random_terms_are_ignored()
    {
        var c = FishRules.ParseCondition("LOCATION_SEASON Here Winter, RANDOM 0.4");
        Assert.Equal(SeasonMask.Winter, c.Seasons);
        Assert.Empty(c.Other);
    }

    [Fact]
    public void Terms_keep_commas_inside_quotes()
    {
        Assert.Equal(2, FishRules.Terms("A \"x, y\", B").Count);
    }

    // ------------------------------------------------------------------ spots

    [Fact]
    public void Spot_uses_location_season_and_fish_data_hours()
    {
        var spec = FishRules.ParseFish("143", Catfish);
        var s = FishRules.MakeSpot("(O)143", "Town", "", null, "LOCATION_SEASON Here spring fall winter", 0, false, spec, false, false);
        Assert.Equal(SeasonMask.Spring | SeasonMask.Fall | SeasonMask.Winter, s.Seasons);
        Assert.Equal(FishWeather.Rain, s.Weather);
        Assert.Equal(FishBlock.Now, FishRules.Check(s, Spring, 1000, rain: true, level: 0));
        Assert.Equal(FishBlock.Weather, FishRules.Check(s, Spring, 1000, rain: false, level: 0));
        Assert.Equal(FishBlock.Season, FishRules.Check(s, Summer, 1000, rain: true, level: 0));
        Assert.Equal(FishBlock.Time, FishRules.Check(s, Fall, 2500, rain: true, level: 0));
        Assert.Equal("6-24", FishRules.BlockText(s, FishBlock.Time));
    }

    [Fact]
    public void Legendary_ignores_fish_data_and_uses_spawn_level()
    {
        // Beach Crimsonfish: Season summer, MinFishingLevel 5, IgnoreFishDataRequirements (Data/Fish says winter 6-20)
        var spec = FishRules.ParseFish("159", Crimsonfish);
        var s = FishRules.MakeSpot("(O)159", "Beach", "", Summer, "!PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY", 5, true, spec, true, false);
        Assert.Equal(SeasonMask.Summer, s.Seasons);
        Assert.Equal("any time", FishRules.Hours(s.Times));
        Assert.Equal(5, s.MinLevel);
        Assert.Equal(FishBlock.Level, FishRules.Check(s, Summer, 2500, false, 4));
        Assert.Equal(FishBlock.Now, FishRules.Check(s, Summer, 2500, false, 5));
        Assert.Equal("Fishing 5", FishRules.BlockText(s, FishBlock.Level));
    }

    [Fact]
    public void Catch_limit_stops_a_caught_legendary()
    {
        // Mountain Legend: CatchLimit 1 in Data/Locations; GetFishFromLocationData skips it once fishCaught reaches the limit
        var s = FishRules.MakeSpot("(O)163", "Mountain", "", 0, null, 10, true, null, true, false, catchLimit: 1);
        Assert.Equal(FishBlock.Now, FishRules.Check(s, Spring, 1000, true, 10, caught: 0));
        Assert.Equal(FishBlock.CaughtLimit, FishRules.Check(s, Spring, 1000, true, 10, caught: 1));
        Assert.Equal(FishBlock.CaughtLimit, FishRules.Check(s, Summer, 1000, true, 0, caught: 1)); // never again beats any other reason
        Assert.Equal("already caught, once only", FishRules.BlockText(s, FishBlock.CaughtLimit));
        Assert.Equal("once only", FishRules.LimitText(1));
        Assert.Equal("", FishRules.LimitText(-1));
        var free = FishRules.MakeSpot("(O)143", "Town", "", null, null, 0, true, null, false, false);
        Assert.Equal(FishBlock.Now, FishRules.Check(free, Spring, 1000, true, 0, caught: 50));
        Assert.True(FishBlock.CaughtLimit < FishBlock.Season); // the "closest to biting" order puts it last
    }

    [Fact]
    public void Simplify_keeps_a_limited_spot_apart_from_an_unlimited_one()
    {
        var limited = FishRules.MakeSpot("(O)1", "Forest", "", null, null, 0, true, null, false, false, catchLimit: 1);
        var free = FishRules.MakeSpot("(O)1", "Forest", "", null, null, 0, true, null, false, false);
        Assert.Single(FishRules.Simplify(new[] { limited, free }));               // the unlimited one covers it
        Assert.Equal(-1, FishRules.Simplify(new[] { limited, free }).Single().CatchLimit);
        var springOnly = FishRules.MakeSpot("(O)1", "Forest", "", Spring, null, 0, true, null, false, false);
        Assert.Equal(2, FishRules.Simplify(new[] { limited, springOnly }).Count); // neither covers the other
    }

    [Fact]
    public void Crab_pot_water_words_and_valley_maps_first()
    {
        Assert.Equal("fresh water", FishRules.CrabWaterText("freshwater"));
        Assert.Equal("ocean water", FishRules.CrabWaterText("ocean"));
        Assert.Equal(new[] { "Town", "Forest", "Mountain", "BugLand", "Desert" },
            FishRules.ValleyFirst(new[] { "Desert", "Mountain", "BugLand", "Town", "Forest" }));
    }

    [Theory]
    [InlineData("BeachNightMarket", "Beach")]
    [InlineData("DesertFestival", "Desert")]
    [InlineData("Town", "Town")]
    public void Festival_maps_borrow_the_fish_of_their_map(string name, string expected) =>
        Assert.Equal(expected, FishRules.BaseMap(name));

    [Fact]
    public void Fish_data_min_level_counts_when_not_ignored()
    {
        var spec = FishRules.ParseFish("165", "Scorpion Carp/90/dart/12/32/600 2000/spring summer fall winter/both/683 .4/2/.15/.1/4/false");
        var s = FishRules.MakeSpot("(O)165", "Desert", "top pond", null, null, 0, false, spec, false, false);
        Assert.Equal(4, s.MinLevel);
        Assert.Equal("Desert top pond", s.Place);
    }

    [Fact]
    public void Magic_bait_and_festival_block_last()
    {
        var bait = FishRules.MakeSpot("(O)798", "Beach", "", null, null, 0, true, null, false, true);
        Assert.Equal(FishBlock.MagicBait, FishRules.Check(bait, Spring, 1000, false, 0));
        var squid = FishRules.MakeSpot("(O)151", "Beach", "", null, "IS_PASSIVE_FESTIVAL_OPEN SquidFest", 0, true, null, false, false);
        Assert.Equal(FishBlock.Festival, FishRules.Check(squid, Winter, 1000, false, 0, festivalOpen: false));
        Assert.Equal(FishBlock.Now, FishRules.Check(squid, Winter, 1000, false, 0, festivalOpen: true));
        Assert.Equal("during Squid Fest", FishRules.BlockText(squid, FishBlock.Festival));
    }

    [Fact]
    public void Simplify_drops_covered_and_duplicate_spots()
    {
        var salmon = FishRules.ParseFish("139", "Salmon/50/mixed/24/65/600 1900/fall/both/684 .35/3/.4/.2/0/true");
        var anyTime = FishRules.MakeSpot("(O)139", "Forest", "", Fall, null, 0, true, salmon, false, false);
        var river = FishRules.MakeSpot("(O)139", "Forest", "river", Fall, null, 0, false, salmon, false, false);
        var again = FishRules.MakeSpot("(O)139", "Forest", "", Fall, null, 0, true, salmon, false, false);
        var town = FishRules.MakeSpot("(O)139", "Town", "", Fall, null, 0, false, salmon, false, false);
        var kept = FishRules.Simplify(new[] { river, anyTime, again, town });
        Assert.Equal(2, kept.Count);
        Assert.Same(anyTime, kept[0]);
        Assert.Same(town, kept[1]);
    }

    [Fact]
    public void Simplify_keeps_a_wider_season_spot_apart()
    {
        var a = FishRules.MakeSpot("(O)1", "Forest", "", Fall, null, 0, true, null, false, false);
        var b = FishRules.MakeSpot("(O)1", "Forest", "", null, null, 0, false, FishRules.ParseFish("1", Halibut), false, false);
        Assert.Equal(2, FishRules.Simplify(new[] { a, b }).Count);
    }

    [Fact]
    public void Spot_hint_text()
    {
        Assert.Equal("stand at x 82-181, y 0-99, cast 3+ tiles from shore", FishRules.SpotHint((82, 0, 100, 100), null, 3));
        Assert.Equal("", FishRules.SpotHint(null, null, 0));
    }

    [Theory]
    [InlineData("TopPond", "top pond")]
    [InlineData("River", "river")]
    [InlineData("Default", "")]
    [InlineData(null, "")]
    public void Area_labels(string? id, string expected)
    {
        Assert.Equal(expected, FishRules.AreaLabel(id));
    }

    // ------------------------------------------------------------------ mines

    [Theory]
    [InlineData(5, 0)]
    [InlineData(20, 10)]
    [InlineData(39, 0)]
    [InlineData(60, 40)]
    [InlineData(100, 80)]
    [InlineData(120, 80)]
    [InlineData(150, 121)]
    public void Mine_areas(int level, int area)
    {
        Assert.Equal(area, FishRules.MineArea(level));
    }

    [Fact]
    public void Mine_fish_table_matches_MineShaft_getFish()
    {
        Assert.Equal("(O)158", FishRules.MineFishFor(FishRules.MineArea(20))!.FishId); // Stonefish
        Assert.Equal("(O)161", FishRules.MineFishFor(FishRules.MineArea(60))!.FishId); // Ice Pip
        Assert.Equal("(O)162", FishRules.MineFishFor(FishRules.MineArea(100))!.FishId); // Lava Eel
        Assert.Null(FishRules.MineFishFor(121));
    }

    [Theory]
    [InlineData("Mines", 0)]
    [InlineData("the mines", 0)]
    [InlineData("mine 60", 60)]
    [InlineData("UndergroundMine100", 100)]
    [InlineData("floor 20", 20)]
    public void Mine_floor_queries(string query, int floor)
    {
        Assert.Equal(floor, FishRules.ParseMineFloor(query));
    }

    [Theory]
    [InlineData("Mountain")]
    [InlineData("floor")]
    [InlineData("")]
    public void Not_mine_queries(string query)
    {
        Assert.Null(FishRules.ParseMineFloor(query));
    }

    // ------------------------------------------------------------------ arguments

    private static readonly (string, string)[] Maps =
    {
        ("Town", "Pelican Town"), ("Forest", "Cindersap Forest"), ("Woods", "Secret Woods"), ("Beach", "The Beach"),
        ("Mountain", "Mountain"), ("Farm", "Hilltop Farm"), ("WitchSwamp", "WitchSwamp"), ("IslandWest", "Ginger Island"),
        ("IslandSouth", "Ginger Island")
    };

    [Theory]
    [InlineData("town", "Town")]
    [InlineData("Pelican Town", "Town")]
    [InlineData("secret woods", "Woods")]
    [InlineData("Cindersap", "Forest")]
    [InlineData("ocean", "Beach")]
    [InlineData("witch's swamp", "WitchSwamp")]
    [InlineData("farm pond", "Farm")]
    [InlineData("mount", "Mountain")]
    [InlineData("Ginger Island", "IslandSouth")]
    public void Resolves_locations(string query, string expected)
    {
        Assert.Equal(expected, FishRules.ResolveLocation(query, Maps).name);
    }

    [Fact]
    public void Unknown_location_gives_no_name()
    {
        var (name, candidates) = FishRules.ResolveLocation("Moon", Maps);
        Assert.Null(name);
        Assert.Empty(candidates);
    }

    [Fact]
    public void Ambiguous_location_lists_candidates()
    {
        var (name, candidates) = FishRules.ResolveLocation("island", new[] { ("IslandWest", "West"), ("IslandNorth", "North") });
        Assert.Null(name);
        Assert.Equal(2, candidates.Count);
    }

    [Theory]
    [InlineData("14:20", 1420)]
    [InlineData("14:25", 1420)]
    [InlineData("2pm", 1400)]
    [InlineData("6am", 600)]
    [InlineData("12pm", 1200)]
    [InlineData("12am", 2400)]
    [InlineData("1am", 2500)]
    [InlineData("1:30", 2530)]
    [InlineData("1420", 1420)]
    [InlineData("600", 600)]
    [InlineData("18", 1800)]
    [InlineData("midnight", 2400)]
    public void Parses_times(string text, int expected)
    {
        Assert.Equal(expected, FishRules.ParseTime(text));
    }

    [Theory]
    [InlineData("3am")]
    [InlineData("27:00")]
    [InlineData("soon")]
    [InlineData("13pm")]
    public void Rejects_bad_times(string text)
    {
        Assert.Null(FishRules.ParseTime(text));
    }

    [Theory]
    [InlineData("rain", true)]
    [InlineData("Storm", true)]
    [InlineData("green rain", true)]
    [InlineData("sunny", false)]
    [InlineData("snow", false)]
    public void Parses_weather(string text, bool rain)
    {
        Assert.Equal(rain, FishRules.ParseWeather(text));
    }

    [Fact]
    public void Unknown_weather_is_null()
    {
        Assert.Null(FishRules.ParseWeather("fog"));
    }

    [Fact]
    public void Season_mask_text()
    {
        Assert.Equal("spring/fall", SeasonMask.Text(SeasonMask.Spring | SeasonMask.Fall));
        Assert.Equal("", SeasonMask.Text(SeasonMask.All));
        Assert.Equal("every season", SeasonMask.Long(SeasonMask.All));
        Assert.Equal(SeasonMask.Winter, SeasonMask.Of("Winter"));
    }
}
