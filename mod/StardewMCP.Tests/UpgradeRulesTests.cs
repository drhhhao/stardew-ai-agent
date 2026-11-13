using System;
using System.Collections.Generic;
using Xunit;
using StardewMCP.Agent;

namespace StardewMCP.Tests;

public class UpgradeRulesTests
{
    private static readonly FarmBuilding CoopA = new("Coop", 50, 10, 6, 3);
    private static readonly FarmBuilding CoopB = new("Coop", 60, 20, 6, 3);
    private static readonly FarmBuilding Barn = new("Barn", 40, 30, 7, 4);
    private static readonly FarmBuilding Silo = new("Silo", 30, 10, 3, 3);

    [Fact]
    public void Only_eligible_building_is_picked_without_a_tile()
    {
        var farm = new List<FarmBuilding> { CoopA, Barn, Silo };
        var r = UpgradeRules.Choose(farm, "Big Coop", "Coop", false, -1, -1, null);
        Assert.Equal(UpgradeDecision.Upgrade, r.Decision);
        Assert.Same(CoopA, r.Target);
    }

    [Fact]
    public void Several_eligible_buildings_need_a_tile_and_are_listed()
    {
        var farm = new List<FarmBuilding> { CoopB, CoopA, Barn };
        var r = UpgradeRules.Choose(farm, "Big Coop", "Coop", false, -1, -1, null);
        Assert.Equal(UpgradeDecision.NeedTile, r.Decision);
        Assert.Null(r.Target);
        Assert.Contains("(50,10)", r.Message);
        Assert.Contains("(60,20)", r.Message);
        Assert.DoesNotContain("(40,30)", r.Message);
    }

    [Fact]
    public void Any_tile_of_the_building_selects_it()
    {
        var farm = new List<FarmBuilding> { CoopA, CoopB };
        var at = UpgradeRules.At(farm, 55, 12); // bottom-right of CoopA's 6x3 footprint
        Assert.Same(CoopA, at);
        var r = UpgradeRules.Choose(farm, "Big Coop", "Coop", true, 55, 12, at);
        Assert.Equal(UpgradeDecision.Upgrade, r.Decision);
        Assert.Same(CoopA, r.Target);
        Assert.Null(UpgradeRules.At(farm, 56, 12));
    }

    [Fact]
    public void Wrong_type_at_tile_is_refused_with_the_right_ones()
    {
        var farm = new List<FarmBuilding> { CoopA, Barn };
        var r = UpgradeRules.Choose(farm, "Big Coop", "Coop", true, 41, 31, Barn);
        Assert.Equal(UpgradeDecision.Refuse, r.Decision);
        Assert.Contains("is a Barn", r.Message);
        Assert.Contains("(50,10)", r.Message);
    }

    [Fact]
    public void Already_upgraded_building_is_refused()
    {
        var big = new FarmBuilding("Big Coop", 50, 10, 6, 3);
        var r = UpgradeRules.Choose(new List<FarmBuilding> { big }, "Big Coop", "Coop", true, 50, 10, big);
        Assert.Equal(UpgradeDecision.Refuse, r.Decision);
        Assert.Contains("already a Big Coop", r.Message);
    }

    [Fact]
    public void No_building_at_tile_is_refused()
    {
        var r = UpgradeRules.Choose(new List<FarmBuilding> { CoopA }, "Big Coop", "Coop", true, 1, 1, null);
        Assert.Equal(UpgradeDecision.Refuse, r.Decision);
        Assert.Contains("no building at (1,1)", r.Message);
    }

    [Fact]
    public void Unfinished_building_is_not_eligible()
    {
        var building = new FarmBuilding("Coop", 50, 10, 6, 3, DaysBusy: 2);
        var farm = new List<FarmBuilding> { building };
        Assert.Empty(UpgradeRules.Eligible(farm, "Coop"));
        var r = UpgradeRules.Choose(farm, "Big Coop", "Coop", false, -1, -1, null);
        Assert.Equal(UpgradeDecision.Refuse, r.Decision);
        Assert.Contains("still under construction", r.Message);
        var t = UpgradeRules.Choose(farm, "Big Coop", "Coop", true, 50, 10, building);
        Assert.Equal(UpgradeDecision.Refuse, t.Decision);
    }

    [Fact]
    public void Deluxe_needs_the_middle_tier()
    {
        var farm = new List<FarmBuilding> { CoopA };
        var r = UpgradeRules.Choose(farm, "Deluxe Coop", "Big Coop", false, -1, -1, null);
        Assert.Equal(UpgradeDecision.Refuse, r.Decision);
        Assert.Contains("no finished Big Coop", r.Message);
    }

    [Fact]
    public void Takers_names_the_buildings()
    {
        var farm = new List<FarmBuilding> { CoopA, Barn };
        Assert.Contains("(50,10) can take it", UpgradeRules.Takers(farm, "Coop"));
        Assert.Contains("you have none", UpgradeRules.Takers(farm, "Big Barn"));
        Assert.Contains("not finished", UpgradeRules.Takers(new[] { new FarmBuilding("Shed", 1, 1, 7, 3, 1) }, "Shed"));
    }

    [Fact]
    public void A_building_already_being_upgraded_says_so()
    {
        var upgrading = new FarmBuilding("Coop", 50, 10, 6, 3, 2, "Big Coop");
        var farm = new List<FarmBuilding> { upgrading };
        var r = UpgradeRules.Choose(farm, "Big Coop", "Coop", false, -1, -1, null);
        Assert.Equal(UpgradeDecision.Refuse, r.Decision);
        Assert.Contains("already being upgraded into a Big Coop (2 day(s) left)", r.Message);
        var t = UpgradeRules.Choose(farm, "Big Coop", "Coop", true, 51, 11, upgrading);
        Assert.Contains("already being upgraded", t.Message);
        Assert.Contains("already being upgraded into a Big Coop", UpgradeRules.Takers(farm, "Coop"));
    }

    [Fact]
    public void Localized_names_compare_by_id_and_show_by_name()
    {
        var big = new FarmBuilding("Big Coop", 50, 10, 6, 3);
        Func<string, string> shown = id => id switch { "Coop" => "鸡舍", "Big Coop" => "大鸡舍", "Barn" => "畜棚", _ => id };
        var r = UpgradeRules.Choose(new List<FarmBuilding> { big }, "大鸡舍", "Coop", true, 50, 10, big, "Big Coop", shown);
        Assert.Contains("is already a 大鸡舍", r.Message);
        var b = UpgradeRules.Choose(new List<FarmBuilding> { Barn }, "大鸡舍", "Coop", true, 41, 31, Barn, "Big Coop", shown);
        Assert.Contains("is a 畜棚", b.Message);
        Assert.Contains("an upgrade of a 鸡舍 only", b.Message);
        Assert.Contains("your 鸡舍 at (50,10) can take it", UpgradeRules.Takers(new[] { CoopA }, "Coop", shown));
    }

    [Theory]
    [InlineData("Big Coop", true)]
    [InlineData("deluxe barn", true)]
    [InlineData("Coop", false)]
    [InlineData("Silo", false)]
    [InlineData("list", false)]
    [InlineData("", false)]
    [InlineData("Fancy Hutch", true)] // a modded upgrade, once the catalog has named it
    public void Upgrade_names_are_recognised_without_the_game(string name, bool upgrade)
    {
        Assert.Equal(upgrade, UpgradeRules.LooksLikeUpgrade(name, new[] { "Fancy Hutch" }));
    }
}
