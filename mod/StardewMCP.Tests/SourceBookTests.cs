using System.Linq;
using Xunit;
using StardewMCP.Agent;

namespace StardewMCP.Tests;

public class SourceBookTests
{
    [Fact]
    public void Groups_places_by_season_and_detail()
    {
        var book = new SourceBook();
        book.AddPlace("(O)16", SourceKind.Forage, "Mountain", SeasonMask.Spring);
        book.AddPlace("(O)16", SourceKind.Forage, "Forest", SeasonMask.Spring);
        book.AddPlace("(O)16", SourceKind.Forage, "Forest", SeasonMask.Spring);
        Assert.Equal(new[] { "forage: Mountain, Forest (spring)" }, book.Lines("(O)16"));
    }

    [Fact]
    public void Fish_line_with_detail_and_unknown_items_are_empty()
    {
        var book = new SourceBook();
        book.AddPlace("(O)143", SourceKind.Fish, "Town river", SeasonMask.Spring | SeasonMask.Fall | SeasonMask.Winter, "6:00-24:00, rain");
        Assert.Equal("fish: Town river (spring/fall/winter), 6:00-24:00, rain", book.Lines("(O)143").Single());
        Assert.Empty(book.Lines("(O)999"));
    }

    [Fact]
    public void Kinds_come_in_a_fixed_order()
    {
        var book = new SourceBook();
        book.AddText("(O)24", SourceKind.Shop, "Pierre's 35g");
        book.AddText("(O)24", SourceKind.Crop, "from Parsnip Seeds", SeasonMask.Spring);
        var lines = book.Lines("(O)24");
        Assert.Equal("crop: from Parsnip Seeds (spring)", lines[0]);
        Assert.Equal("shop: Pierre's 35g", lines[1]);
    }

    [Fact]
    public void Texts_rank_and_cap()
    {
        var book = new SourceBook();
        for (int i = 0; i < 7; i++) book.AddText("(O)766", SourceKind.Monster, $"M{i}", rank: i);
        Assert.Equal("monster drop: M6, M5, M4, M3, and 3 more", book.Lines("(O)766").Single());
    }

    [Fact]
    public void Legendary_places_need_permission()
    {
        var book = new SourceBook();
        book.AddPlace("(O)163", SourceKind.Fish, "Mountain", SeasonMask.Spring, "rain", bossLevel: 10);
        Assert.Empty(book.Lines("(O)163", lvl => lvl <= 5));
        Assert.Single(book.Lines("(O)163", lvl => lvl <= 10));
    }

    [Fact]
    public void Lines_are_capped()
    {
        var book = new SourceBook();
        foreach (SourceKind k in System.Enum.GetValues(typeof(SourceKind))) book.AddText("x", k, "y");
        Assert.Equal(SourceBook.MaxLines, book.Lines("x").Count);
        Assert.Equal(3, book.Lines("x", null, 3).Count);
    }

    [Fact]
    public void Ignores_empty_and_seasonless_facts()
    {
        var book = new SourceBook();
        book.AddPlace("a", SourceKind.Forage, "Town", SeasonMask.None);
        book.AddText("a", SourceKind.Shop, "");
        Assert.Equal(0, book.Count);
    }

    [Theory]
    [InlineData("SeedShop", "Pierre's")]
    [InlineData("Traveler", "Traveling Cart (Fri/Sun, random stock)")]
    [InlineData("Festival_EggFestival_Pierre", "Egg Festival")]
    [InlineData("Festival_NightMarket_MagicBoat_Day1", "Night Market")]
    [InlineData("DesertFestival_Gus", "Desert Festival")]
    [InlineData("SomeModShop", "Some Mod Shop")]
    public void Shop_labels(string id, string label)
    {
        Assert.Equal(label, SourceBook.ShopLabel(id));
    }

    [Fact]
    public void Catalogues_are_not_shops()
    {
        Assert.True(SourceBook.SkipShop("JunimoFurnitureCatalogue"));
        Assert.False(SourceBook.SkipShop("Sandy"));
    }

    [Fact]
    public void Once_only_places_drop_out_after_the_catch()
    {
        var book = new SourceBook();
        book.AddPlace("(O)163", SourceKind.Fish, "Mountain", SeasonMask.Spring, "rain, once only", bossLevel: 10, catchLimit: 1);
        book.AddPlace("(O)163", SourceKind.Fish, "Somewhere", SeasonMask.Spring, "rain");
        Assert.Contains("Mountain", book.Lines("(O)163", caught: 0).Single());
        Assert.DoesNotContain("Mountain", book.Lines("(O)163", caught: 1).Single());
        var only = new SourceBook();
        only.AddPlace("(O)160", SourceKind.Fish, "Beach", SeasonMask.Summer, "once only", catchLimit: 1);
        Assert.Empty(only.Lines("(O)160", caught: 1));
    }

    [Fact]
    public void Mystery_boxes_follow_getTreasureFromGeode()
    {
        var box = SourceBook.MysteryBoxDrops("(O)MysteryBox");
        Assert.Contains("(O)MixedFlowerSeeds", box);
        Assert.Contains("(O)525", box);           // the ring roll of the common table
        Assert.Contains("(O)532", box);
        Assert.Contains("(O)SkillBook_4", box);   // rare table after 10 boxes
        Assert.DoesNotContain("(O)217", box);     // excluded from the 194-239 dish range
        Assert.DoesNotContain("(O)GoldenAnimalCracker", box);
        var golden = SourceBook.MysteryBoxDrops("(O)GoldenMysteryBox");
        Assert.Contains("(O)GoldenAnimalCracker", golden);
        Assert.Contains("(BC)272", golden);       // Auto-Petter
        Assert.DoesNotContain("(O)MixedFlowerSeeds", golden); // the golden box never reaches the common table
        Assert.Empty(SourceBook.MysteryBoxDrops("(O)535"));
    }

    [Fact]
    public void Geode_fallback_follows_getTreasureFromGeode()
    {
        var geode = SourceBook.GeodeFallback("(O)535");
        Assert.Contains("(O)86", geode);      // Earth Crystal
        Assert.DoesNotContain("(O)384", geode); // no gold ore from a plain Geode
        var frozen = SourceBook.GeodeFallback("(O)536");
        Assert.Contains("(O)84", frozen);     // Frozen Tear
        Assert.Contains("(O)384", frozen);
        Assert.DoesNotContain("(O)386", frozen);
        var magma = SourceBook.GeodeFallback("(O)537");
        Assert.Contains("(O)82", magma);      // Fire Quartz
        Assert.Contains("(O)386", magma);     // iridium ore
        var omni = SourceBook.GeodeFallback("(O)749");
        Assert.Contains("(O)84", omni);
        Assert.Contains("(O)86", omni);
        Assert.Contains("(O)330", omni);      // clay
    }

    [Theory]
    [InlineData("id_o_262", "(O)262")]
    [InlineData("id_bc_12", "(BC)12")]
    [InlineData("category_fruits", null)]
    [InlineData("id_o_", null)]
    public void Tag_item_ids(string tag, string? id)
    {
        Assert.Equal(id, SourceBook.TagItemId(tag));
    }

    [Fact]
    public void Tag_text()
    {
        Assert.Equal("fruits", SourceBook.TagText("category_fruits"));
        Assert.Equal("keg wine", SourceBook.TagText("keg_wine"));
    }
}
