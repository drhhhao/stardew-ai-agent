using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using StardewMCP.Agent;

namespace StardewMCP.Tests;

public class BundlePlanTests
{
    private static string Qualify(string id) => id.StartsWith("(") ? id : "(O)" + id;

    private static BundleInfo Parse(string key, string value, params bool[] flags) =>
        BundlePlan.Parse(key, value, _ => flags, Qualify)!;

    // ------------------------------------------------------------------ parsing

    [Fact]
    public void Parses_a_bundle_entry_with_donated_flags()
    {
        var b = Parse("Pantry/0", "Spring Crops/O 465 20/24 1 0 188 1 0 190 1 0 192 1 0/0///Spring Crops", true, false, false, false);
        Assert.Equal(0, b.Index);
        Assert.Equal("Pantry", b.Room);
        Assert.Equal(0, b.Area);
        Assert.Equal("Spring Crops", b.Name);
        Assert.Equal("O 465 20", b.Reward);
        Assert.Equal(4, b.Needs.Count);
        Assert.Equal(4, b.Required);
        Assert.Equal(1, b.Filled);
        Assert.Equal(3, b.Remaining);
        Assert.Equal("(O)24", b.Needs[0].ItemId);
        Assert.True(b.Needs[0].Done);
        Assert.Equal(new[] { "(O)188", "(O)190", "(O)192" }, b.Open.Select(n => n.ItemId));
    }

    [Fact]
    public void Parses_the_required_count_and_gold_and_category_slots()
    {
        var animal = Parse("Pantry/4", "Animal/BO 16 1/186 1 0 182 1 0 174 1 0 438 1 0 440 1 0 442 1 0/4/5//Animal");
        Assert.Equal(5, animal.Required);
        Assert.Equal(6, animal.Open.Count());

        var vault = Parse("Vault/23", "2,500g/O 220 3/-1 2500 2500/4///2,500g");
        Assert.True(vault.Needs[0].IsGold);
        Assert.Null(vault.Needs[0].ItemId);

        var fish = Parse("Fish Tank/99", "Any Fish/O 1 1/-4 3 0/0///Any Fish");
        Assert.Equal(-4, fish.Needs[0].Category);
        Assert.Null(fish.Needs[0].ItemId);
        Assert.Equal(3, fish.Needs[0].Count);
    }

    [Fact]
    public void Rooms_outside_the_six_have_no_area_and_bad_entries_are_skipped()
    {
        Assert.Equal(-1, Parse("Abandoned Joja Mart/36", "The Missing/O 1 1/(O)348 1 1/0/5//The Missing").Area);
        Assert.Null(BundlePlan.Parse("Pantry", "x/y/z", _ => Array.Empty<bool>(), Qualify));
        Assert.Null(BundlePlan.Parse("Pantry/1", "only a name", _ => Array.Empty<bool>(), Qualify));
    }

    [Fact]
    public void A_complete_bundle_has_no_open_slots_even_with_unfilled_options()
    {
        var b = Parse("Pantry/4", "Animal/BO 16 1/186 1 0 182 1 0 174 1 0 438 1 0 440 1 0 442 1 0/4/5//Animal", true, true, true, true, true, false);
        Assert.True(b.Complete);
        Assert.Empty(b.Open);
    }

    // ------------------------------------------------------------------ matching

    [Fact]
    public void Item_category_gold_and_dinosaur_egg_matching()
    {
        var parsnip = new BundleNeed(0, "24", 5, 2, false) { ItemId = "(O)24" };
        Assert.True(BundlePlan.Accepts(parsnip, "(O)24", -75));
        Assert.False(BundlePlan.Accepts(parsnip, "(O)188", -75));
        Assert.False(BundlePlan.Fits(parsnip, "(O)24", -75, 1));
        Assert.True(BundlePlan.Fits(parsnip, "(O)24", -75, 2));
        Assert.False(BundlePlan.Fits(parsnip with { Done = true }, "(O)24", -75, 4));

        var egg = new BundleNeed(0, "-5", 1, 0, false);
        Assert.True(BundlePlan.Accepts(egg, "(O)176", -5));
        Assert.True(BundlePlan.Accepts(egg, "(O)107", 0));
        Assert.False(BundlePlan.Accepts(egg, "(O)24", -75));

        Assert.False(BundlePlan.Accepts(new BundleNeed(0, "-1", 2500, 2500, false), "(O)24", -75));
        Assert.True(BundlePlan.Accepts(new BundleNeed(0, "(BC)10", 1, 0, false), "(BC)10", 0));
    }

    [Fact]
    public void Slot_text_names_quality_and_the_pick()
    {
        var b = Parse("Pantry/3", "Quality Crops/BO 15 1/24 5 2 254 5 2 276 5 2 270 5 2/6/3//Quality Crops");
        Assert.Equal("Quality Crops (Pantry): 5, gold or better (any 3 of 4 still open)", BundlePlan.SlotText(b, b.Needs[0]));
        var s = Parse("Pantry/0", "Spring Crops/O 465 20/24 1 0 188 1 0/0///Spring Crops");
        Assert.Equal("Spring Crops (Pantry): 1, any quality", BundlePlan.SlotText(s, s.Needs[1]));
    }

    // ------------------------------------------------------------------ have

    [Fact]
    public void Have_text_counts_pack_and_places()
    {
        Assert.Equal("have 0", BundlePlan.HaveText(1, 0, new List<(string, int)>()));
        Assert.Equal("have 1 pack + 3 Farm chest (64,15)", BundlePlan.HaveText(1, 1, new List<(string, int)> { ("Farm chest (64,15)", 3) }));
        Assert.Equal("have 2 pack (short 3)", BundlePlan.HaveText(5, 2, new List<(string, int)>()));
        Assert.Equal("have 4 Shed chest (1,1) + 1 Farm chest (2,2) + 2 in 2 more",
            BundlePlan.HaveText(5, 0, new List<(string, int)> { ("Farm chest (2,2)", 1), ("Shed chest (1,1)", 4), ("a", 1), ("b", 1) }, maxPlaces: 2));
    }

    // ------------------------------------------------------------------ seasons

    [Theory]
    [InlineData("spring", 23, "season ends in 5 days")]
    [InlineData("spring", 27, "season ends in 1 day")]
    [InlineData("spring", 28, "last day of its season")]
    [InlineData("summer", 3, "out of season (spring)")]
    public void Season_note_for_a_spring_only_item(string season, int day, string expected)
    {
        Assert.Equal(expected, BundlePlan.SeasonNote(new[] { "forage: Forest, spring", "crop: spring, 4 days" }, season, day));
    }

    [Fact]
    public void No_season_note_when_a_source_is_not_seasonal_or_the_next_season_has_it()
    {
        Assert.Equal("", BundlePlan.SeasonNote(new[] { "forage: Forest, spring", "shop: Pierre 50g" }, "spring", 20));
        Assert.Equal("", BundlePlan.SeasonNote(new[] { "fish: Beach, spring/summer, 6:00-19:00" }, "spring", 20));
        Assert.Equal("season ends in 8 days", BundlePlan.SeasonNote(new[] { "fish: Beach, spring/summer, 6:00-19:00" }, "summer", 20));
        Assert.Equal("", BundlePlan.SeasonNote(Array.Empty<string>(), "spring", 20));
        Assert.Equal("", BundlePlan.SeasonNote(new[] { "spring summer fall winter" }, "spring", 20));
        Assert.Equal("out of season (fall, winter)", BundlePlan.SeasonNote(new[] { "fish: Lake, Fall, rain", "fish: Lake, winter" }, "spring", 20));
        Assert.Equal("", BundlePlan.SeasonNote(new[] { "forage: Waterfall" }, "spring", 1));
    }

    // ------------------------------------------------------------------ keep allocation

    [Fact]
    public void Allocation_fills_quality_slots_first_and_uses_chests_before_the_pack()
    {
        var needs = new List<BundleNeed>
        {
            new(0, "24", 1, 0, false) { ItemId = "(O)24" }, // Spring Crops: 1 any
            new(0, "24", 5, 2, false) { ItemId = "(O)24" }  // Quality Crops: 5 gold
        };
        var stacks = new List<KeepStack>
        {
            new(0, 1, InPack: false), // chest: 1 normal
            new(2, 3, InPack: false), // chest: 3 gold
            new(2, 10, InPack: true), // pack: 10 gold
            new(0, 7, InPack: true)   // pack: 7 normal
        };
        var alloc = BundlePlan.Allocate(needs, stacks, (s, n) => stacks[s].Quality >= needs[n].Quality);
        Assert.Equal(new[] { (0, 1) }, alloc[0]);
        Assert.Equal(new[] { (1, 3) }, alloc[1]);
        Assert.Equal(new[] { (1, 2) }, alloc[2]);
        Assert.Empty(alloc[3]);
    }

    [Fact]
    public void Allocation_spreads_a_need_over_several_stacks()
    {
        var needs = new List<BundleNeed> { new(0, "388", 99, 0, false) { ItemId = "(O)388" } };
        var stacks = new List<KeepStack> { new(0, 60, true), new(0, 60, true) };
        var alloc = BundlePlan.Allocate(needs, stacks, (_, _) => true);
        Assert.Equal(new[] { (0, 60) }, alloc[0]);
        Assert.Equal(new[] { (0, 39) }, alloc[1]);
    }

    [Fact]
    public void An_n_of_m_bundle_keeps_only_the_slots_it_still_needs_preferring_chest_cover()
    {
        // Animal bundle, 5 of 6: 4 filled, Large Egg and Wool open; only 1 more needed.
        var animal = Parse("Pantry/4", "Animal/BO 16 1/186 1 0 182 1 0 174 1 0 438 1 0 440 1 0 442 1 0/4/5", true, true, false, true, false, true);
        Assert.Equal(1, animal.Remaining);
        // Spring Crops: 2 open, both still needed.
        var spring = Parse("Pantry/0", "Spring Crops/O 465 20/24 1 0 188 1 0 190 1 0 192 1 0/0///Spring Crops", true, true, false, false);
        var slots = animal.Open.Select(n => (animal, n)).Concat(spring.Open.Select(n => (spring, n))).ToList();
        Assert.Equal(new[] { "(O)174", "(O)440", "(O)190", "(O)192" }, slots.Select(x => x.n.ItemId));
        var stacks = new List<KeepStack> { new(0, 3, InPack: true), new(0, 1, InPack: false) }; // pack Wool, chest Large Egg
        var items = new[] { "(O)440", "(O)174" };
        var picked = BundlePlan.PickSlots(slots, stacks, (s, n) => slots[n].n.ItemId == items[s]);
        Assert.Equal(new[] { 0, 2, 3 }, picked); // the chest-covered Large Egg, not the Wool in the pack

        // Nothing covered: the smaller slot wins, then the first.
        var none = BundlePlan.PickSlots(slots, Array.Empty<KeepStack>(), (_, _) => false);
        Assert.Equal(new[] { 0, 2, 3 }, none);
    }

    [Theory]
    [InlineData(-74, "Seeds", true)]
    [InlineData(-19, "Basic", true)]
    [InlineData(-21, "Basic", true)]
    [InlineData(-22, "Basic", true)]
    [InlineData(-8, "Crafting", true)]
    [InlineData(0, "Crafting", true)]
    [InlineData(-16, "Basic", true)]
    [InlineData(-15, "Basic", true)]
    [InlineData(-7, "Cooking", true)]
    [InlineData(-75, "Basic", false)]
    [InlineData(-4, "Fish", false)]
    [InlineData(-26, "Basic", false)]
    [InlineData(-2, "Minerals", false)]
    public void Supplies_are_seeds_gear_materials_and_food(int category, string type, bool supply) =>
        Assert.Equal(supply, BundlePlan.IsSupply(category, type));

    // ------------------------------------------------------------------ keep report

    [Fact]
    public void Keep_report_lists_reasons_and_what_is_safe_to_ship()
    {
        var rows = new List<KeepRow>
        {
            new("Parsnip (gold)", 15, new[] { "bundle Quality Crops (Pantry) 5" }, 5, true),
            new("Amethyst", 1, new[] { "museum-new" }, 1, true),
            new("Blueberry", 30, Array.Empty<string>(), 0, true, "Preserves Jar -> Blueberry Jelly 150g, 3 days"),
            new("Sap", 30, Array.Empty<string>(), 0, true),
            new("Lost Book", 1, Array.Empty<string>(), 0, false),
            new("Parsnip Seeds", 15, Array.Empty<string>(), 0, true, Supply: true),
            new("Wood", 150, new[] { "bundle Construction (Crafts Room) 99" }, 99, true, Supply: true)
        };
        var text = BundlePlan.KeepReport(new[] { "Ship or keep?" }, rows);
        Assert.Equal(
            "Ship or keep?\n" +
            "- Parsnip (gold) x15: bundle Quality Crops (Pantry) 5 | keep 5, ship 10\n" +
            "- Amethyst x1: museum-new | keep all\n" +
            "- Blueberry x30: best: Preserves Jar -> Blueberry Jelly 150g, 3 days\n" +
            "- Wood x150: bundle Construction (Crafts Room) 99 | keep 99 for this\n" +
            "Supplies kept (seeds, gear, materials, food): Parsnip Seeds x15, Wood x51.\n" +
            "Safe to ship: Parsnip (gold) x10, Blueberry x30, Sap x30.",
            text.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Keep_report_for_an_empty_pack_or_nothing_to_ship()
    {
        Assert.Equal("Your pack holds nothing to ship or keep.", BundlePlan.KeepReport(Array.Empty<string>(), Array.Empty<KeepRow>()));
        var text = BundlePlan.KeepReport(Array.Empty<string>(), new[] { new KeepRow("Amethyst", 1, new[] { "museum-new" }, 1, true) });
        Assert.EndsWith("Safe to ship: nothing.", text);
    }
}
