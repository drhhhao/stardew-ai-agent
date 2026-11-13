using System.Collections.Generic;
using System.Linq;
using Xunit;
using StardewMCP.Agent;

namespace StardewMCP.Tests;

/// <summary>Speed-Gro growth, shop conditions and prices, recipe unlocks and buff text (KnowledgeMath.cs).</summary>
public class KnowledgeFactsTests
{
    private static readonly bool[] SpringOnly = { true, false, false, false };
    private static readonly int[] Strawberry = { 1, 1, 2, 2, 2 };

    // ------------------------------------------------------------------ fertilizer

    [Theory]
    [InlineData("speed_gro", 0.1f)]
    [InlineData("Speed-Gro", 0.1f)]
    [InlineData("deluxe_speed_gro", 0.25f)]
    [InlineData("Hyper Speed-Gro", 0.33f)]
    [InlineData("", 0f)]
    [InlineData(null, 0f)]
    [InlineData("none", 0f)]
    [InlineData("quality_fertilizer", -1f)]
    public void Fertilizer_names(string? name, float expected)
    {
        Assert.Equal(expected, CropMath.FertilizerBoost(name));
    }

    [Fact]
    public void Agriculturist_adds_ten_percent()
    {
        Assert.Equal(0.1f, CropMath.GrowthBoost(0f, true));
        Assert.Equal(0.25f + 0.1f, CropMath.GrowthBoost(0.25f, true));
        Assert.Equal(0.33f, CropMath.GrowthBoost(0.33f, false));
    }

    [Fact]
    public void Speed_gro_takes_a_day_off_strawberries()
    {
        var phases = CropMath.SpeedUpPhases(Strawberry, 0.1f);
        Assert.Equal(new[] { 1, 0, 2, 2, 2 }, phases); // the first phase stays at 1 day; the next one goes to 0
        Assert.Equal(7, CropMath.GrowthDays(phases));
    }

    [Fact]
    public void Speed_gro_strawberries_planted_on_spring_13_give_three_harvests()
    {
        int growth = CropMath.GrowthDays(CropMath.SpeedUpPhases(Strawberry, 0.1f));
        var plan = CropMath.Plan(0, 13, SpringOnly, growth, 4);
        Assert.Equal(3, plan.Harvests); // spring 20, 24, 28
        Assert.Equal((0, 20), CropMath.Advance(0, 13, growth));

        var dayLater = CropMath.Plan(0, 14, SpringOnly, growth, 4);
        Assert.Equal(2, dayLater.Harvests); // spring 21, 25
    }

    [Fact]
    public void Hyper_speed_gro_with_agriculturist_halves_strawberries()
    {
        var phases = CropMath.SpeedUpPhases(Strawberry, CropMath.GrowthBoost(0.33f, true));
        Assert.Equal(new[] { 1, 0, 1, 1, 1 }, phases); // ceil(8 * 0.43) = 4 days cut
        Assert.Equal(4, CropMath.GrowthDays(phases));
    }

    [Fact]
    public void Cut_is_rounded_up_and_spread_over_the_phases()
    {
        Assert.Equal(10, CropMath.GrowthDays(CropMath.SpeedUpPhases(new[] { 1, 2, 4, 4, 1 }, 0.1f)));   // cauliflower 12: ceil(1.2) = 2
        Assert.Equal(21, CropMath.GrowthDays(CropMath.SpeedUpPhases(new[] { 2, 7, 7, 7, 5 }, 0.25f)));  // ancient fruit 28: 7 days
    }

    [Fact]
    public void Cut_stops_after_three_passes()
    {
        // 4 one-day phases, 80% cut = 4 days, but the first phase keeps 1 day and the others reach 0 in the first pass.
        var phases = CropMath.SpeedUpPhases(new[] { 1, 1, 1, 1 }, 0.8f);
        Assert.Equal(new[] { 1, 0, 0, 0 }, phases);
    }

    [Fact]
    public void No_boost_keeps_the_phases()
    {
        Assert.Equal(Strawberry, CropMath.SpeedUpPhases(Strawberry, 0f));
    }

    [Fact]
    public void Fruit_days_per_year()
    {
        Assert.Equal(28, CropMath.FruitDaysPerYear(1, false));
        Assert.Equal(112, CropMath.FruitDaysPerYear(1, true));
    }

    // ------------------------------------------------------------------ shop conditions

    [Fact]
    public void Season_and_year_condition()
    {
        var terms = ShopConditions.Parse("SEASON spring, YEAR 2");
        Assert.Equal(new[] { true, false, false, false }, ShopConditions.Seasons(terms));
        Assert.Equal("spring, year 2+", ShopConditions.Label(terms, true));
        Assert.Equal("year 2+", ShopConditions.Label(terms, false));
    }

    [Fact]
    public void Negated_conditions()
    {
        Assert.Equal(new[] { true, true, true, false }, ShopConditions.Seasons(ShopConditions.Parse("!SEASON winter")));
        Assert.Equal("year 1 only", ShopConditions.Label(ShopConditions.Parse("!YEAR 2"), true));
    }

    [Theory]
    [InlineData("SEASON fall winter, SYNCED_RANDOM day cart_coffee_bean 0.25", "fall/winter, sometimes")]
    [InlineData("DAY_OF_WEEK Monday", "Mon")]
    [InlineData("PLAYER_BASE_FISHING_LEVEL Current 6", "Fishing 6+")]
    [InlineData("MINE_LOWEST_LEVEL_REACHED 20", "after mine floor 20")]
    [InlineData("!PLAYER_HAS_CRAFTING_RECIPE Current Dehydrator", "if you lack the recipe Dehydrator")]
    [InlineData("WORLD_STATE_FIELD TimesFedRaccoons 2", "if WORLD_STATE_FIELD TimesFedRaccoons 2")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Condition_labels(string? condition, string expected)
    {
        Assert.Equal(expected, ShopConditions.Label(ShopConditions.Parse(condition), true));
    }

    [Fact]
    public void No_season_term_means_any_season()
    {
        Assert.Null(ShopConditions.Seasons(ShopConditions.Parse("YEAR 2")));
    }

    [Fact]
    public void Quoted_commas_do_not_split()
    {
        var terms = ShopConditions.Parse("PLAYER_HAS_MAIL Current \"a,b\", YEAR 2");
        Assert.Equal(2, terms.Count);
        Assert.Equal("a,b", terms[0].Args[1]);
        Assert.Equal("YEAR", terms[1].Key);
    }

    [Theory]
    [InlineData("SeedShop", "Pierre's (SeedShop)")]
    [InlineData("Festival_EggFestival_Pierre", "Egg Festival, spring 13")]
    [InlineData("DesertFestival_Maru", "Desert Festival (Maru), spring 15-17")]
    [InlineData("SomeModShop", "SomeModShop")]
    public void Shop_labels(string id, string expected)
    {
        Assert.Equal(expected, ShopLabels.For(id));
    }

    // ------------------------------------------------------------------ prices

    private static PriceOffer Gold(string shop, int price, string condition = "", bool unlocked = true) =>
        new(shop, price, "g", "", 0, false, false, unlocked, condition);

    [Fact]
    public void Cheapest_prefers_offers_sold_in_the_season()
    {
        var offers = new List<PriceOffer>
        {
            Gold("SeedShop", 150, "SEASON summer"),
            Gold("Joja", 187, "SEASON fall"),
            Gold("Festival_NightMarket_MagicBoat_Day2", 140, "")
        };
        // the Night Market stall (no SEASON condition) sells only in winter
        Assert.Equal(150, ShopPricing.Cheapest(offers, 1)!.Price);
        Assert.Equal(187, ShopPricing.Cheapest(offers, 2)!.Price);
        Assert.Equal(140, ShopPricing.Cheapest(offers, 3)!.Price);
        Assert.Equal(150, ShopPricing.Cheapest(offers.Take(2), 1)!.Price);
        Assert.False(ShopPricing.SoldIn(offers[2], 1));
        Assert.True(ShopPricing.SoldIn(offers[2], -1));
    }

    [Fact]
    public void Festival_offers_are_over_after_the_festival_day()
    {
        var strawberry = new[] { Gold("Festival_EggFestival_Pierre", 100) };
        Assert.Equal(100, ShopPricing.Cheapest(strawberry, 0, 1)!.Price);
        Assert.Equal(100, ShopPricing.Cheapest(strawberry, 0, 13)!.Price);
        Assert.Null(ShopPricing.Cheapest(strawberry, 0, 20));
        Assert.True(ShopPricing.Passed(strawberry[0], 0, 20));
        Assert.False(ShopPricing.Passed(strawberry[0], -1, 20)); // Greenhouse plan: any time of year
        Assert.Equal(100, ShopPricing.Cheapest(strawberry, -1)!.Price);
        Assert.Equal(new ShopLabels.FestivalWindow("Desert Festival (Maru)", 0, 15, 17), ShopLabels.Festival("DesertFestival_Maru"));
        Assert.Null(ShopLabels.Festival("SeedShop"));
    }

    [Fact]
    public void Cheapest_buys_ahead_when_nothing_is_sold_in_season()
    {
        var rare = new[] { Gold("Traveler", 1000, "SEASON spring summer") };
        Assert.Equal(1000, ShopPricing.Cheapest(rare, 2)!.Price);
    }

    [Fact]
    public void Locked_trade_and_random_offers_are_not_a_price()
    {
        var offers = new List<PriceOffer>
        {
            Gold("SeedShop", 60, "SEASON spring, YEAR 2", unlocked: false),
            new("Raccoon", 0, "g", "Carrot", 1, false, false, true, ""),
            new("Traveler", 0, "g", "", 0, false, true, true, ""),
            new("Festival_StardewValleyFair_StarTokens", 100, " star tokens", "", 0, false, false, true, "")
        };
        Assert.Null(ShopPricing.Cheapest(offers, 0));
        Assert.Null(ShopPricing.Cheapest(new List<PriceOffer>(), 0));
    }

    [Fact]
    public void Offer_text()
    {
        Assert.Equal("Pierre's (SeedShop) 60g (spring, year 2+, not unlocked yet)", ShopPricing.Text(Gold("SeedShop", 60, "SEASON spring, YEAR 2", false), true));
        Assert.Equal("Pierre's (SeedShop) 20g", ShopPricing.Text(Gold("SeedShop", 20, "SEASON spring"), false));
        Assert.Equal("Desert Festival egg shop, spring 15-17 5 Calico Egg (Mon)",
            ShopPricing.Text(new PriceOffer("DesertFestival_EggShop", 0, "g", "Calico Egg", 5, false, false, true, "DAY_OF_WEEK Monday"), true));
        Assert.Equal("Traveling Cart (Fri/Sun) random price", ShopPricing.Text(new PriceOffer("Traveler", 0, "g", "", 0, false, true, true, ""), true));
    }

    // ------------------------------------------------------------------ recipes

    [Theory]
    [InlineData("388 2/Field/322/false/default/", false, "default", "", 0)]
    [InlineData("390 2/Field/323/false/s Farming 2/", false, "skill", "Farming", 2)]
    [InlineData("335 1/Field/324 10/false/Farming 4/", false, "skill", "Farming", 4)]
    [InlineData("20 1 22 1 419 1/25 5/196/f Emily 3/", true, "friendship", "Emily", 3)]
    [InlineData("-5 1 -6 1/1 10/195/l 10/", true, "other", "", 0)]
    [InlineData("1 1/x/2/null/", true, "other", "", 0)]
    [InlineData("", true, "other", "", 0)]
    public void Recipe_unlocks(string recipe, bool cooking, string kind, string who, int level)
    {
        Assert.Equal(new ItemFacts.Unlock(kind, who, level), ItemFacts.RecipeUnlock(recipe, cooking));
    }

    [Fact]
    public void Friendship_unlocks_are_shown_only_when_omniscient()
    {
        var u = ItemFacts.RecipeUnlock("20 1/25 5/196/f Emily 3/", true);
        Assert.Equal("a letter from Emily at 3 hearts", ItemFacts.UnlockText(u, true));
        Assert.Equal("", ItemFacts.UnlockText(u, false));
        Assert.Equal("Farming level 2", ItemFacts.UnlockText(ItemFacts.RecipeUnlock("390 2/Field/323/false/s Farming 2/", false), false));
    }

    [Fact]
    public void Recipe_outputs()
    {
        var (ids, count, big) = RecipeMath.Outputs("335 1/Field/324 10/false/s Farming 4/", false);
        Assert.Equal(new[] { "324" }, ids);
        Assert.Equal(10, count);
        Assert.False(big);

        var keg = RecipeMath.Outputs("388 30 334 1 335 1 724 1/Home/12/true/s Farming 8/", false);
        Assert.True(keg.bigCraftable);
        Assert.Equal(1, keg.count);

        Assert.False(RecipeMath.Outputs("-5 1/10 10/194/true/", true).bigCraftable); // field 3 is the unlock for cooking
    }

    [Theory]
    [InlineData(1, 1, 0, 7)]
    [InlineData(4, 1, 0, 28)]
    [InlineData(5, 1, 1, 7)]
    [InlineData(16, 1, 3, 28)]
    [InlineData(17, 2, 0, 7)]
    [InlineData(32, 2, 3, 28)]
    public void Queen_of_sauce_weeks(int week, int year, int season, int day)
    {
        Assert.Equal((year, season, day), ItemFacts.TvWeekDate(week));
    }

    // ------------------------------------------------------------------ buffs and items

    [Theory]
    [InlineData("FarmingLevel", 2f, "Farming +2")]
    [InlineData("Speed", -1f, "Speed -1")]
    [InlineData("MaxStamina", 50f, "Max Energy +50")]
    [InlineData("AttackMultiplier", 0.1f, "Attack +10%")]
    [InlineData("Defense", 0f, "")]
    public void Buff_effects(string field, float value, string expected)
    {
        Assert.Equal(expected, ItemFacts.BuffEffect(field, value));
    }

    [Theory]
    [InlineData(600, "10h")]
    [InlineData(90, "1h 30m")]
    [InlineData(30, "30m")]
    [InlineData(-2, "the rest of the day")]
    [InlineData(0, "")]
    public void Buff_durations(int minutes, string expected)
    {
        Assert.Equal(expected, ItemFacts.GameMinutes(minutes));
    }

    [Fact]
    public void Buff_lines()
    {
        Assert.Equal("Farming +2, Max Energy +50 for 10h game time", ItemFacts.BuffLine("", new[] { "Farming +2", "Max Energy +50" }, "10h game time", false));
        Assert.Equal("Tipsy (debuff): Speed -1 for 42m game time", ItemFacts.BuffLine("Tipsy", new[] { "Speed -1" }, "42m game time", true));
    }

    [Fact]
    public void Weapon_types_and_tool_bars()
    {
        Assert.Equal("club", ItemFacts.WeaponType(2));
        Assert.Equal("sword", ItemFacts.WeaponType(0)); // omitted Type in Data/Weapons (Galaxy Sword)
        Assert.Equal("sword", ItemFacts.WeaponType(3));
        Assert.Equal("slingshot", ItemFacts.WeaponType(4));
        Assert.Equal("(O)334", ItemFacts.ToolUpgradeBarId(1));
        Assert.Equal("(O)337", ItemFacts.ToolUpgradeBarId(4));
        Assert.Equal("steel", ItemFacts.ToolLevelName(2));
    }

    [Theory]
    [InlineData("Axe", "CopperAxe", 1, "axe", "copper")]
    [InlineData("WateringCan", "WateringCan", 0, "watering can", "basic")]
    [InlineData("FishingRod", "IridiumRod", 3, "fishing rod", "")]
    [InlineData("GenericTool", "GoldTrashCan", 3, "trash can", "gold")]
    [InlineData("Wand", "ReturnScepter", -1, "return scepter", "")]
    [InlineData("MilkPail", "MilkPail", -1, "milk pail", "")]
    public void Tool_kind_and_level(string className, string id, int level, string kind, string levelText)
    {
        Assert.Equal(kind, ItemFacts.ToolKind(className, id));
        Assert.Equal(levelText, ItemFacts.ToolLevelText(kind, level));
    }

    [Fact]
    public void Trash_can_condition_label()
    {
        Assert.Equal("when your trash can is basic", ShopConditions.Label(ShopConditions.Parse("PLAYER_HAS_TRASH_CAN_LEVEL Current 0 0"), true));
    }

    [Fact]
    public void Special_buff_descriptions()
    {
        Assert.Equal("You are immune to debuffs", ItemFacts.BuffDescription("28", "You are immune to debuffs."));
        Assert.StartsWith("Your skin exudes a pungent aroma; no monsters spawn", ItemFacts.BuffDescription("23", "Your skin exudes\na pungent aroma"));
        Assert.Equal("Squid Ink Ravioli: You are immune to debuffs for 3h game time",
            ItemFacts.BuffLine("Squid Ink Ravioli", new[] { ItemFacts.BuffDescription("28", "You are immune to debuffs.") }, "3h game time", false));
    }
}
