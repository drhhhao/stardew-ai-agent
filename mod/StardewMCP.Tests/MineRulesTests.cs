using StardewMCP.Agent;
using Xunit;

namespace StardewMCP.Tests;

public class MineRulesTests
{
    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 5)]
    [InlineData(4, 5)]
    [InlineData(5, 10)]
    [InlineData(9, 10)]
    [InlineData(119, 120)]
    [InlineData(120, 120)]
    [InlineData(121, 125)]
    [InlineData(125, 130)]
    public void Elevator_targets_are_always_the_next_multiple_of_five(int current, int expected) =>
        Assert.Equal(expected, MineRules.NextElevatorFloor(current));

    [Fact]
    public void Valuable_nodes_come_before_plain_stone()
    {
        Assert.True(MineRules.IsValuableNode("Copper Node"));
        Assert.True(MineRules.IsValuableNode("Gem Node"));
        Assert.True(MineRules.IsValuableNode("Coal Node"));
        Assert.False(MineRules.IsValuableNode("Stone"));
    }
}
