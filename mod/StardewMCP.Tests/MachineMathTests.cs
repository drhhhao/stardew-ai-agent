using System.Collections.Generic;
using System.Linq;
using Xunit;
using StardewMCP.Agent;

namespace StardewMCP.Tests;

public class MachineMathTests
{
    // ------------------------------------------------------------------ time

    [Theory]
    [InlineData(600, 360)]
    [InlineData(1430, 870)]
    [InlineData(2600, 1560)]
    public void Clock_times_become_minutes(int time, int minutes)
    {
        Assert.Equal(minutes, MachineMath.ToMinutes(time));
    }

    [Theory]
    [InlineData(10000, false, "6.25 days")] // Keg wine
    [InlineData(4000, false, "2.5 days")]   // Preserves Jar
    [InlineData(1600, false, "1 day")]
    [InlineData(240, false, "4h")]
    [InlineData(100, false, "1h 40m")]
    [InlineData(50, false, "50 min")]
    [InlineData(0, false, "instant")]
    [InlineData(1600, true, "1 day, ready at 6:00")]
    [InlineData(4800, true, "3 days, ready at 6:00")]
    [InlineData(4000, true, "3 days, ready at 6:00")] // Slime Incubator: only done overnight, so rounded up
    [InlineData(4500, true, "3 days, ready at 6:00")] // Incubator with Coopmaster
    public void Durations(int minutes, bool wholeDays, string expected)
    {
        Assert.Equal(expected, MachineMath.Duration(minutes, wholeDays));
    }

    [Fact]
    public void A_machine_day_is_1600_minutes_like_the_game_night()
    {
        // Utility.CalculateMinutesUntilMorning(600, 1) = 1560 - 360 + 400
        Assert.Equal(1560 - 360 + 400, MachineMath.MinutesPerDay);
    }

    [Theory]
    [InlineData(100, 4000, 40)]  // 2.5 days
    [InlineData(100, 120, 100)]  // a short job counts as one load per day
    [InlineData(300, 1600, 300)]
    public void Value_per_machine_day(double gain, int minutes, double expected)
    {
        Assert.Equal(expected, MachineMath.PerMachineDay(gain, minutes), 3);
    }

    [Theory]
    [InlineData(600, 600, 0, 1600)]     // 10 hours from 6:00: 16:00 today
    [InlineData(2200, 100, 0, 2340)]    // 23:40 tonight
    [InlineData(2200, 300, 1, 600)]     // would end after 2:00: ready when you wake up
    [InlineData(600, 1600, 1, 600)]     // a whole machine-day
    [InlineData(600, 10000, 6, 1240)]   // wine loaded at 6:00
    [InlineData(1430, 0, 0, 1430)]
    public void Ready_time(int now, int minutesLeft, int dayOffset, int time)
    {
        Assert.Equal((dayOffset, time), MachineMath.ReadyAt(now, minutesLeft));
    }

    [Theory]
    [InlineData(600, 9000, 6, 600)]   // Incubator loaded at 6:00: its time runs out at 22:40 on day 5, it hatches at 6:00
    [InlineData(1540, 0, 1, 600)]     // time ran out today: still not ready before the night
    [InlineData(600, 1600, 1, 600)]   // ends right at 6:00
    [InlineData(2200, 300, 1, 600)]
    public void Overnight_machines_are_ready_at_6(int now, int minutesLeft, int dayOffset, int time)
    {
        Assert.Equal((dayOffset, time), MachineMath.ReadyAt(now, minutesLeft, overnight: true));
    }

    [Fact]
    public void Ready_text_of_an_overnight_machine()
    {
        Assert.Equal("tomorrow 6:00", MachineMath.ReadyText(1540, 0, 12, 0, overnight: true));
        Assert.Equal("today 15:40", MachineMath.ReadyText(1540, 0, 12, 0));
    }

    [Fact]
    public void Whole_days_round_up()
    {
        Assert.Equal(1, MachineMath.WholeDays(0));
        Assert.Equal(1, MachineMath.WholeDays(1600));
        Assert.Equal(2, MachineMath.WholeDays(1601));
        Assert.Equal(6, MachineMath.WholeDays(9000));
    }

    [Fact]
    public void Ready_text_names_the_day()
    {
        Assert.Equal("today 16:00", MachineMath.ReadyText(600, 600, 10, 0));
        Assert.Equal("tomorrow 6:00", MachineMath.ReadyText(2200, 300, 10, 0));
        Assert.Equal("Tue 16 12:40", MachineMath.ReadyText(600, 10000, 10, 0));
        Assert.Equal("Wed summer 3 12:40", MachineMath.ReadyText(600, 10000, 25, 0));
        Assert.Equal("today 1:30", MachineMath.ReadyText(2500, 30, 10, 0));
    }

    [Theory]
    [InlineData(1, "Mon")]
    [InlineData(7, "Sun")]
    [InlineData(14, "Sun")]
    [InlineData(19, "Fri")]
    public void Weekdays(int day, string name)
    {
        Assert.Equal(name, MachineMath.Weekday(day));
    }

    // ------------------------------------------------------------------ chances

    [Theory]
    [InlineData(null, 1.0)]
    [InlineData("", 1.0)]
    [InlineData("RANDOM 0.3", 0.3)]
    [InlineData("RANDOM .05", 0.05)]
    [InlineData("!RANDOM 0.25", 0.75)]
    [InlineData("ITEM_ID Target (O)80", 1.0)]
    [InlineData("RANDOM 0.5, RANDOM 0.5", 0.25)]
    [InlineData("SYNCED_RANDOM day key 0.2", 0.2)]
    public void Random_chance_of_a_condition(string? condition, double expected)
    {
        Assert.Equal(expected, MachineMath.RandomChance(condition), 6);
    }

    [Fact]
    public void Queries_split_outside_quotes()
    {
        var parts = MachineMath.SplitQueries("ANY \"ITEM_ID Target 1, ITEM_ID Target 2\" \"X\", RANDOM 0.5");
        Assert.Equal(2, parts.Count);
        Assert.Equal("RANDOM 0.5", parts[1]);
    }

    [Fact]
    public void First_valid_output_chances_chain()
    {
        // Recycling Machine, Trash: 30% Coal, else 30% Iron Ore, else Stone
        var p = MachineMath.OutputChances(new[] { 0.3, 0.3, 1.0 }, useFirstValid: true);
        Assert.Equal(0.3, p[0], 6);
        Assert.Equal(0.21, p[1], 6);
        Assert.Equal(0.49, p[2], 6);
    }

    [Fact]
    public void Random_pick_among_valid_outputs()
    {
        var even = MachineMath.OutputChances(new[] { 1.0, 1.0, 1.0, 1.0 }, useFirstValid: false);
        Assert.All(even, x => Assert.Equal(0.25, x, 6));

        var mixed = MachineMath.OutputChances(new[] { 1.0, 0.5 }, useFirstValid: false);
        Assert.Equal(0.75, mixed[0], 6);
        Assert.Equal(0.25, mixed[1], 6);

        var none = MachineMath.OutputChances(new[] { 0.0 }, useFirstValid: false);
        Assert.Equal(0.0, none[0], 6);
    }

    [Theory]
    [InlineData(0.3, "30%")]
    [InlineData(0.02, "2%")]
    [InlineData(0.025, "2.5%")]
    public void Percent_text(double chance, string expected)
    {
        Assert.Equal(expected, MachineMath.Percent(chance));
    }

    // ------------------------------------------------------------------ wording

    [Fact]
    public void Tags_read_as_words()
    {
        Assert.Equal("any gem", MachineMath.TagText(new[] { "category_gem", "!crystalarium_banned" }));
        Assert.Equal("large eggs", MachineMath.TagText(new[] { "large_egg_item", "egg_item" }));
        Assert.Equal("items tagged modded_thing", MachineMath.TagText(new[] { "modded_thing" }));
        Assert.Equal("", MachineMath.TagText(null));
    }

    [Fact]
    public void Id_tags_read_as_item_names()
    {
        // Bone Mill: RequiredTags ["bone_item", "!id_o_881"]
        string? Name(string id) => id == "(O)881" ? "Bone Fragment" : null;
        Assert.Equal("bone items except Bone Fragment", MachineMath.TagText(new[] { "bone_item", "!id_o_881" }, Name));
        Assert.Equal("bone items except item (O)881", MachineMath.TagText(new[] { "bone_item", "!id_o_881" }));
        Assert.Equal("Bone Fragment", MachineMath.TagText(new[] { "id_o_881" }, Name));
        Assert.Equal("not modded thing", MachineMath.TagText(new[] { "!modded_thing" }));
    }

    [Theory]
    [InlineData("id_o_881", "(O)881")]
    [InlineData("id_bc_12", "(BC)12")]
    [InlineData("id_o_driedfruit", "(O)driedfruit")]
    [InlineData("bone_item", null)]
    [InlineData("id_o", null)]
    public void Id_tag_item(string tag, string? expected)
    {
        Assert.Equal(expected, MachineMath.IdTagItem(tag));
    }

    // ------------------------------------------------------------------ averages

    [Fact]
    public void Expected_quantity_averages_random_modifiers()
    {
        // Bone Mill: 3 Bone Fragments... x2 on "RANDOM .1"
        Assert.Equal(3.3, MachineMath.ExpectedQuantity(3, new List<(string, double, double)> { ("Multiply", 2, 0.1) }), 6);
        Assert.Equal(5, MachineMath.ExpectedQuantity(5, null), 6);
        // Stack mode applies in order: (2 + 1) * 2 = 6 when both apply
        Assert.Equal(6, MachineMath.ExpectedQuantity(2, new List<(string, double, double)> { ("Add", 1, 1), ("Multiply", 2, 1) }), 6);
        // a modifier whose other conditions fail has chance 0
        Assert.Equal(4, MachineMath.ExpectedQuantity(4, new List<(string, double, double)> { ("Set", 99, 0) }), 6);
        // Minimum: the lowest single result of those that apply, else the value
        Assert.Equal(0.5 * 2 + 0.5 * 10, MachineMath.ExpectedQuantity(10, new List<(string, double, double)> { ("Set", 2, 0.5), ("Set", 5, 0) }, "Minimum"), 6);
        Assert.Equal(12, MachineMath.ExpectedQuantity(10, new List<(string, double, double)> { ("Add", 2, 1), ("Subtract", 3, 1) }, "Maximum"), 6);
    }

    [Fact]
    public void Any_branches_split_on_quotes()
    {
        // Loom: ANY "ITEM_QUALITY Input 1 1, RANDOM 0.15" "ITEM_QUALITY Input 2 2, RANDOM 0.5" "ITEM_QUALITY Input 4 4, RANDOM 1"
        Assert.Equal(new[] { "ITEM_QUALITY Input 1 1, RANDOM 0.15", "ITEM_QUALITY Input 2 2, RANDOM 0.5", "ITEM_QUALITY Input 4 4, RANDOM 1" },
            MachineMath.SplitQuoted("\"ITEM_QUALITY Input 1 1, RANDOM 0.15\" \"ITEM_QUALITY Input 2 2, RANDOM 0.5\" \"ITEM_QUALITY Input 4 4, RANDOM 1\""));
        Assert.Equal(new[] { "A", "B" }, MachineMath.SplitQuoted("A  B"));
        Assert.Empty(MachineMath.SplitQuoted(null));
        Assert.Equal(0.5, MachineMath.RandomChance("ITEM_QUALITY Input 2 2, RANDOM 0.5"), 6);
    }

    [Theory]
    [InlineData("(O)680", "green")]
    [InlineData("(O)413", "blue")]
    [InlineData("(O)437", "red")]
    [InlineData("(O)439", "purple")]
    [InlineData("(O)857", "tiger")]
    [InlineData("(O)24", "")]
    public void Slime_from_egg(string egg, string slime)
    {
        Assert.Equal(slime, MachineMath.SlimeFromEgg(egg));
    }

    [Fact]
    public void Conditions_read_as_words()
    {
        Assert.Equal("not edible mushrooms, edible", MachineMath.ConditionText("!ITEM_CONTEXT_TAG Input edible_mushroom, ITEM_EDIBILITY Input 1"));
        Assert.Equal("2% chance", MachineMath.ConditionText("RANDOM 0.02"));
        Assert.Equal("not in winter", MachineMath.ConditionText("!LOCATION_SEASON Target Winter"));
        Assert.Equal("WEIRD_QUERY 1", MachineMath.ConditionText("WEIRD_QUERY 1"));
        Assert.Equal("", MachineMath.ConditionText(null));
    }

    [Fact]
    public void Flavored_prices_and_methods()
    {
        Assert.Equal("3x the input's base price", MachineMath.FlavoredPrice("Wine"));
        Assert.Equal("2x the input's base price + 50", MachineMath.FlavoredPrice("Pickle"));
        Assert.Equal("depends on the input", MachineMath.FlavoredPrice("Unknown"));
        Assert.Equal("ages it to better quality (Cellar only)", MachineMath.MethodText("StardewValley.Objects.Cask, Stardew Valley: OutputCask"));
        Assert.StartsWith("a special output", MachineMath.MethodText("Mod.Thing, Mod: OutputThing"));
    }

    // ------------------------------------------------------------------ best pick

    [Fact]
    public void Best_names_both_limits()
    {
        var options = new List<MachineOption>
        {
            new("Preserves Jar", 100, 40),
            new("Keg", 100, 16),
            new("Dehydrator", 30, 150)
        };
        Assert.Equal("Best: Dehydrator when machines are the limit (150g per machine-day); Preserves Jar when Blueberry is the limit (+100g per item), tied with Keg.",
            MachineMath.Best(options, "Blueberry"));
    }

    [Fact]
    public void Best_when_one_machine_wins_both()
    {
        var options = new List<MachineOption> { new("Keg", 300, 120), new("Preserves Jar", 150, 60) };
        Assert.Equal("Best: Keg both per machine-day (120g) and per Ancient Fruit (+300g).", MachineMath.Best(options, "Ancient Fruit"));
    }

    [Fact]
    public void Best_without_gain()
    {
        Assert.Equal("Best: none of them adds value; sell it as it is.", MachineMath.Best(new List<MachineOption> { new("Keg", -5, -5) }, "X"));
        Assert.Equal("Best: Crystalarium (166.7g per machine-day).", MachineMath.Best(new List<MachineOption> { new("Crystalarium", null, 166.66) }, "Diamond"));
    }

    // ------------------------------------------------------------------ farm overview

    [Fact]
    public void Counts_line_only_with_machines()
    {
        Assert.Equal("", MachineMath.CountsLine(0, 0, 0));
        Assert.Equal("Machines: 4 ready, 6 working, 2 empty (machines() for detail).", MachineMath.CountsLine(4, 6, 2));
    }

    [Fact]
    public void State_lines_group_by_state_machine_and_place()
    {
        var spots = new[]
        {
            new MachineSpot("Keg", "Blueberry Wine", "Shed at (40,12)", 6, 4, MachineState.Ready),
            new MachineSpot("Keg", "Blueberry Wine", "Shed at (40,12)", 5, 4, MachineState.Ready),
            new MachineSpot("Furnace", "Copper Bar", "Farm", 60, 15, MachineState.Ready),
            new MachineSpot("Keg", "Ancient Fruit Wine", "Farm", 61, 15, MachineState.Working, "ready Fri 14 6:00"),
            new MachineSpot("Keg", "Ancient Fruit Wine", "Farm", 62, 15, MachineState.Working, "ready Fri 14 6:00"),
            new MachineSpot("Preserves Jar", "Pickles", "Farm", 63, 15, MachineState.Working, "ready today 18:40"),
            new MachineSpot("Preserves Jar", "Pickles", "Farm", 64, 15, MachineState.Working, "ready tomorrow 6:00"),
            new MachineSpot("Preserves Jar", "", "Farm", 61, 16, MachineState.Empty),
            new MachineSpot("Lightning Rod", "", "Farm", 10, 10, MachineState.Empty, SelfFilling: true)
        };
        var lines = MachineMath.StateLines(spots);
        Assert.Equal(new[]
        {
            "READY (3), collect with interact on the tile:",
            "- Furnace (Copper Bar), Farm: (60,15)",
            "- Keg (Blueberry Wine) x2, Shed at (40,12): (5,4) (6,4)",
            "Working (4):",
            "- Keg (Ancient Fruit Wine) x2, Farm: (61,15) (62,15), ready Fri 14 6:00",
            "- Preserves Jar (Pickles) x2, Farm: (63,15) ready today 18:40; (64,15) ready tomorrow 6:00",
            "Empty (2):",
            "- Lightning Rod, Farm: (10,10) (fills by itself)",
            "- Preserves Jar, Farm: (61,16)"
        }, lines);
    }

    [Fact]
    public void State_lines_cap_long_tile_lists()
    {
        var spots = Enumerable.Range(0, 15).Select(i => new MachineSpot("Keg", "", "Farm", i, 1, MachineState.Empty));
        var line = MachineMath.StateLines(spots, maxTiles: 3)[1];
        Assert.Equal("- Keg x15, Farm: (0,1) (1,1) (2,1) and 12 more", line);
    }
}
