using StardewMCP.Agent;
using Xunit;

namespace StardewMCP.Tests;

public class CombatRulesTests
{
    [Fact]
    public void Monster_types_map_to_distinct_engagement_styles()
    {
        Assert.Equal(CombatStyle.Melee, CombatRules.StyleFor(MonsterRules.ForName("Green Slime")));
        Assert.Equal(CombatStyle.HitAndRun, CombatRules.StyleFor(MonsterRules.ForName("Serpent")));
        Assert.Equal(CombatStyle.HitAndRun, CombatRules.StyleFor(MonsterRules.ForName("Spider")));
        Assert.Equal(CombatStyle.HitAndRun, CombatRules.StyleFor(MonsterRules.ForName("Hot Head")));
        Assert.Equal(CombatStyle.Ranged, CombatRules.StyleFor(MonsterRules.ForName("Skeleton Mage")));
        Assert.Equal(CombatStyle.Flying, CombatRules.StyleFor(MonsterRules.ForName("Frost Bat")));
    }

    [Fact]
    public void Only_hit_and_run_and_ranged_styles_retreat_after_swinging()
    {
        Assert.False(CombatRules.RetreatAfterSwing(CombatStyle.Melee));
        Assert.False(CombatRules.RetreatAfterSwing(CombatStyle.Flying));
        Assert.True(CombatRules.RetreatAfterSwing(CombatStyle.HitAndRun));
        Assert.True(CombatRules.RetreatAfterSwing(CombatStyle.Ranged));
    }

    [Fact]
    public void Support_casters_are_prioritised_over_closer_ordinary_monsters()
    {
        var shaman = MonsterRules.ForName("Shadow Shaman");
        var slime = MonsterRules.ForName("Green Slime");
        Assert.True(CombatRules.TargetPriority(shaman, 5) < CombatRules.TargetPriority(slime, 2));
    }

    [Fact]
    public void Line_of_sight_stops_at_stone_but_not_at_open_tiles()
    {
        Assert.True(CombatRules.LineClear(0, 0, 3, 0, (x, y) => true));
        Assert.False(CombatRules.LineClear(0, 0, 3, 0, (x, y) => x != 2));
        Assert.False(CombatRules.LineClear(0, 0, 3, 2, (x, y) => !(x == 2 && y == 1)));
    }

    [Fact]
    public void Line_of_sight_does_not_reject_the_two_combatants_own_tiles()
    {
        Assert.True(CombatRules.LineClear(0, 0, 1, 0, (x, y) => false));
    }
}
