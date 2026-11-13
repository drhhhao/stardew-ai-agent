using System.Collections.Generic;
using StardewMCP.Agent;
using Xunit;

namespace StardewMCP.Tests;

public class ProgressMathTests
{
    [Theory]
    [InlineData(1, 100)]
    [InlineData(4, 1300)]
    [InlineData(5, 2150)]
    [InlineData(10, 15000)]
    [InlineData(0, -1)]
    [InlineData(11, -1)]
    public void XpForLevel_matches_the_game_table(int level, int xp)
    {
        Assert.Equal(xp, ProgressMath.XpForLevel(level));
    }

    [Fact]
    public void SkillText_shows_xp_to_the_next_level_and_profession_levels()
    {
        Assert.Equal("Farming 4 (1,120/2,150 XP, 1,030 to go; 5: profession)", ProgressMath.SkillText("Farming", 4, 1120));
        Assert.Equal("Fishing 2 (500/770 XP, 270 to go)", ProgressMath.SkillText("Fishing", 2, 500));
        Assert.Equal("Mining 0 (0/100 XP, 100 to go)", ProgressMath.SkillText("Mining", 0, 0));
        Assert.Equal("Combat 10 (max, 16,200 XP)", ProgressMath.SkillText("Combat", 10, 16200));
        Assert.Equal("Fishing 3+1 (800/1,300 XP, 500 to go)", ProgressMath.SkillText("Fishing", 3, 800, 1));
    }

    [Theory]
    [InlineData(0, 2, 3)]   // Rancher -> Coopmaster, Shepherd
    [InlineData(1, 4, 5)]   // Tiller -> Artisan, Agriculturist
    [InlineData(6, 8, 9)]   // Fisher -> Angler, Pirate
    [InlineData(7, 10, 11)] // Trapper -> Mariner, Luremaster
    [InlineData(25, 28, 29)] // Scout -> Acrobat, Desperado
    public void Level10Of_follows_the_profession_tree(int five, int a, int b)
    {
        Assert.Equal((a, b), ProgressMath.Level10Of(five));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    [InlineData(47, 45)]
    [InlineData(120, 120)]
    [InlineData(150, 120)]
    public void ElevatorFloor_is_every_fifth_floor_up_to_120(int lowest, int floor)
    {
        Assert.Equal(floor, ProgressMath.ElevatorFloor(lowest));
    }

    [Fact]
    public void MinesText_splits_the_skull_cavern_off()
    {
        Assert.Equal("Mines: deepest floor 47, elevator to 45 | Skull Cavern: not entered yet", ProgressMath.MinesText(47, 47));
        Assert.Equal("Mines: deepest floor 120 (bottom reached), elevator to 120 | Skull Cavern deepest floor 30", ProgressMath.MinesText(150, 150));
    }

    [Fact]
    public void Have_shows_chests_only_when_something_is_stored()
    {
        Assert.Equal("have 5", ProgressMath.Have(5, 0));
        Assert.Equal("have 2 pack + 4 chest", ProgressMath.Have(2, 4));
    }

    [Fact]
    public void SlayerText_groups_goals_by_state()
    {
        var text = ProgressMath.SlayerText(new List<ProgressMath.SlayerGoal>
        {
            new("Slimes", 312, 1000, "Slime Charmer Ring", false),
            new("Bats", 150, 200, "Vampire Ring", false),
            new("Duggies", 30, 30, "Hard Hat", false),
            new("Crabs", 60, 60, "Crabshell Ring", true),
            new("Mummies", 0, 100, "", false)
        });
        Assert.Equal("Slayer goals (Adventurer's Guild): Bats 150/200 (Vampire Ring) | Slimes 312/1,000 (Slime Charmer Ring). " +
                     "Done, reward waiting at Gil: Duggies (Hard Hat). Not started: Mummies 0/100. Done: Crabs.", text);
    }

    [Theory]
    [InlineData("id_o_96", "(O)96")]
    [InlineData("id_bc_139", "(BC)139")]
    [InlineData("item_type_arch", null)]
    [InlineData("", null)]
    [InlineData("id_o_", null)]
    public void TagItemId_reads_id_context_tags(string tag, string? expected)
    {
        Assert.Equal(expected, ProgressMath.TagItemId(tag));
    }

    [Fact]
    public void Museum_needs_count_items_kinds_and_the_whole_collection()
    {
        var donated = new Dictionary<string, int> { [""] = 23, ["item_type_arch"] = 10, ["item_type_minerals"] = 13, ["id_o_96"] = 1 };
        int Donated(string tag) => donated.GetValueOrDefault(tag);
        string Name(string id) => id == "(O)97" ? "Dwarf Scroll II" : id;

        var dwarf = new[] { new ProgressMath.MuseumNeed("id_o_96", 1), new ProgressMath.MuseumNeed("id_o_97", 1) };
        Assert.Equal(1, ProgressMath.Missing(dwarf, Donated, 95));
        Assert.Equal("Dwarf Scroll II", ProgressMath.NeedsText(dwarf, Donated, 95, Name));

        var chicken = new[] { new ProgressMath.MuseumNeed("id_o_113", 1), new ProgressMath.MuseumNeed("item_type_arch", 15) };
        Assert.Equal(6, ProgressMath.Missing(chicken, Donated, 95));
        Assert.Equal("(O)113, 5 more artifacts", ProgressMath.NeedsText(chicken, Donated, 95, Name));

        var all = new[] { new ProgressMath.MuseumNeed("", -1) };
        Assert.Equal(72, ProgressMath.Missing(all, Donated, 95));
        Assert.Equal("72 more donations", ProgressMath.NeedsText(all, Donated, 95, Name));

        var twenty = new[] { new ProgressMath.MuseumNeed("", 20) };
        Assert.Equal(0, ProgressMath.Missing(twenty, Donated, 95));
        Assert.Equal("", ProgressMath.NeedsText(twenty, Donated, 95, Name));
    }

    [Fact]
    public void SlayerRewardCollected_reads_the_flags_of_a_goal_without_a_reward_item()
    {
        var mail = new HashSet<string> { "Gil_Telephone" };
        // Item goals: only the Gil_<id> flag the reward menu sets counts.
        Assert.True(ProgressMath.SlayerRewardCollected(true, true, new string?[0], mail.Contains));
        Assert.False(ProgressMath.SlayerRewardCollected(false, true, new[] { "Gil_Telephone" }, mail.Contains));
        // FlameSpirits: no item, dialogue + RewardFlagAll/RewardDialogueFlag "Gil_Telephone".
        Assert.True(ProgressMath.SlayerRewardCollected(false, false, new[] { null, "Gil_Telephone", "Gil_Telephone" }, mail.Contains));
        Assert.False(ProgressMath.SlayerRewardCollected(false, false, new[] { null, "Gil_Telephone", "Gil_Telephone" }, new HashSet<string>().Contains));
        // Nothing to collect at all: done is done.
        Assert.True(ProgressMath.SlayerRewardCollected(false, false, new string?[] { null, "", null }, mail.Contains));
    }
}
