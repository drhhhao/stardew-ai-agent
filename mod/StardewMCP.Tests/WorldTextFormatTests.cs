using StardewMCP.Agent;
using Xunit;

namespace StardewMCP.Tests;

public class WorldTextFormatTests
{
    [Theory]
    [InlineData(600, "06:00")]
    [InlineData(610, "06:10")]
    [InlineData(1200, "12:00")]
    [InlineData(1350, "13:50")]
    [InlineData(2350, "23:50")]
    [InlineData(2600, "26:00")] // the game clock runs past midnight until 2 AM (2600)
    [InlineData(0, "00:00")]
    [InlineData(5, "00:05")]
    public void FormatTime_writes_the_game_clock_as_hh_mm(int clock, string expected)
    {
        Assert.Equal(expected, WorldText.FormatTime(clock));
    }

    [Theory]
    [InlineData(0, "up")]
    [InlineData(1, "right")]
    [InlineData(2, "down")]
    [InlineData(3, "left")]
    [InlineData(4, "?")]
    [InlineData(-1, "?")]
    public void DirName_names_the_four_facing_directions(int facing, string expected)
    {
        Assert.Equal(expected, WorldText.DirName(facing));
    }

    [Theory]
    [InlineData(0, "Mine entrance")]
    [InlineData(1, "floor 1")]
    [InlineData(120, "floor 120")]
    [InlineData(121, "Skull Cavern floor 1")] // the game's level 121 is the first Skull Cavern floor (#55)
    [InlineData(200, "Skull Cavern floor 80")]
    public void MineFloorName_counts_skull_cavern_floors_from_one(int level, string expected)
    {
        Assert.Equal(expected, WorldText.MineFloorName(level));
    }
}

public class WorldTextStoneAndTimerTests
{
    [Theory]
    [InlineData("751", "Copper Node")]
    [InlineData("849", "Copper Node")] // volcano copper
    [InlineData("290", "Iron Node")]
    [InlineData("764", "Gold Node")]
    [InlineData("VolcanoGoldNode", "Gold Node")]
    [InlineData("765", "Iridium Node")]
    [InlineData("BasicCoalNode0", "Coal Node")]
    [InlineData("44", "Gem Node")]
    [InlineData("2", "Diamond Node")]
    [InlineData("46", "Mystic Stone")]
    [InlineData("75", "Geode Stone")]
    [InlineData("76", "Frozen Geode Stone")]
    [InlineData("77", "Magma Geode Stone")]
    [InlineData("819", "Omni Geode Stone")]
    [InlineData("668", "Stone")] // plain rock with a small coal chance
    [InlineData("32", "Stone")]
    [InlineData(null, "Stone")]
    public void StoneLabel_names_the_node_by_item_id(string? id, string expected)
    {
        Assert.Equal(expected, WorldText.StoneLabel(id));
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(-5, "")]
    [InlineData(999999, "")] // a cask ageing by quality
    [InlineData(40, "40m left")]
    [InlineData(60, "1h left")]
    [InlineData(130, "2h 10m left")]
    [InlineData(1599, "26h 39m left")]
    [InlineData(1600, "about 1 day left")]
    [InlineData(10000, "about 7 days left")] // keg wine: 7 days in game
    public void TimeLeft_reads_machine_minutes(int minutes, string expected)
    {
        Assert.Equal(expected, WorldText.TimeLeft(minutes));
    }

    [Theory]
    [InlineData("Keg (holding Wine, 2h 10m left)", "Keg (holding Wine)", "2h 10m left")]
    [InlineData("Furnace (holding Copper Bar, 40m left) (owner Abby)", "Furnace (holding Copper Bar) (owner Abby)", "40m left")]
    [InlineData("Keg (holding Wine, 3h left)", "Keg (holding Wine)", "3h left")]
    [InlineData("Keg (holding Wine, about 7 days left)", "Keg (holding Wine)", "about 7 days left")]
    [InlineData("Keg (holding Wine, about 1 day left)", "Keg (holding Wine)", "about 1 day left")]
    [InlineData("Keg (holding Wine, READY)", "Keg (holding Wine, READY)", "")]
    [InlineData("Cask (holding Wine)", "Cask (holding Wine)", "")]
    [InlineData("Copper Node", "Copper Node", "")]
    public void SplitTimeLeft_separates_the_ticking_part_of_a_label(string label, string stable, string left)
    {
        Assert.Equal((stable, left), WorldText.SplitTimeLeft(label));
    }

    [Fact]
    public void StableLabel_makes_a_ticking_machine_compare_equal()
    {
        Assert.Equal(WorldText.StableLabel("Cheese Press (holding Cheese, 3h 20m left)"), WorldText.StableLabel("Cheese Press (holding Cheese, 3h 10m left)"));
        Assert.NotEqual(WorldText.StableLabel("Cheese Press (holding Cheese, 10m left)"), WorldText.StableLabel("Cheese Press (holding Cheese, READY)"));
        Assert.Null(WorldText.StableLabel(null));
        foreach (int m in new[] { 10, 60, 130, 1599, 1600, 10000 })
            Assert.Equal("Keg (holding Wine)", WorldText.StableLabel($"Keg (holding Wine, {WorldText.TimeLeft(m)})"));
    }
}
