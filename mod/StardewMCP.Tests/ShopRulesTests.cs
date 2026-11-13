using StardewMCP.Agent;
using Xunit;

namespace StardewMCP.Tests;

public class ShopRulesTests
{
    [Theory]
    [InlineData(850, ShopAvailability.BeforeOpen)]
    [InlineData(900, ShopAvailability.Open)]
    [InlineData(1200, ShopAvailability.Open)]
    [InlineData(1630, ShopAvailability.ClosingSoon)]
    [InlineData(1700, ShopAvailability.AfterClose)]
    [InlineData(2200, ShopAvailability.AfterClose)]
    public void Counter_hours_include_a_travel_buffer(int now, ShopAvailability expected) =>
        Assert.Equal(expected, ShopRules.Evaluate(now, 900, 1700, false, false));

    [Fact]
    public void Closed_days_and_festivals_win_over_the_clock()
    {
        Assert.Equal(ShopAvailability.ClosedDay, ShopRules.Evaluate(1200, 900, 1700, true, true));
        Assert.Equal(ShopAvailability.Festival, ShopRules.Evaluate(1200, 900, 1700, false, true));
    }

    [Fact]
    public void Closing_buffer_can_be_disabled()
    {
        Assert.Equal(ShopAvailability.Open, ShopRules.Evaluate(1650, 900, 1700, false, false, closingBuffer: 0));
    }
}
