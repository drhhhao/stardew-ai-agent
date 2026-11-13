using System.Collections.Generic;
using System.Linq;
using Xunit;
using StardewMCP.Agent;

namespace StardewMCP.Tests;

public class GiftTasteMathTests
{
    private const int Love = GiftTasteMath.Love, Like = GiftTasteMath.Like, Dislike = GiftTasteMath.Dislike, Hate = GiftTasteMath.Hate, Neutral = GiftTasteMath.Neutral;

    private static GiftItem Fish(string name, int taste, bool have = false) => new(name, "fish", taste, have);

    private static string Part(string summary, string label) => summary.Split(" | ").Single(p => p.StartsWith(label + ": "))[(label.Length + 2)..];

    // ------------------------------------------------------------------ codes

    [Theory]
    [InlineData(0, "loved")]
    [InlineData(2, "liked")]
    [InlineData(4, "disliked")]
    [InlineData(6, "hated")]
    [InlineData(7, "loved")] // Stardrop Tea's special code
    [InlineData(8, "neutral")]
    [InlineData(-1, "neutral")]
    public void Taste_words(int code, string word) => Assert.Equal(word, GiftTasteMath.Word(code));

    [Theory]
    [InlineData(-4, "Fish", "fish")]
    [InlineData(-75, "Basic", "vegetables")]
    [InlineData(-2, "Minerals", "gems")]
    [InlineData(-12, "Minerals", "minerals")]
    [InlineData(0, "Arch", "artifacts")]
    [InlineData(0, "Basic", "")]
    [InlineData(-17, "Basic", "")]
    [InlineData(-101, null, "trinkets")]
    public void Groups(int category, string? type, string group) => Assert.Equal(group, GiftTasteMath.GroupOf(category, type));

    [Theory]
    [InlineData("24", "Parsnip", "Basic", -75, true, true)]
    [InlineData("102", "Lost Book", "asdf", 0, true, false)] // a museum find, not a gift
    [InlineData("Book_Friendship", "Friendship 101", "asdf", -102, true, true)]
    [InlineData("SkillBook_0", "Stardew Valley Almanac", "asdf", -103, true, true)]
    [InlineData("0", "Weeds", "Litter", -999, true, false)]
    [InlineData("191", "Ornate Necklace", "Quest", 0, true, false)]
    [InlineData("516", "Small Glow Ring", "Ring", 0, true, false)]
    [InlineData("458", "Bouquet", "Basic", 0, true, false)]
    [InlineData("StardropTea", "Stardrop Tea", "Basic", 0, true, false)]
    [InlineData("CalicoEgg", "Calico Egg", "Basic", 0, false, false)] // CanBeGivenAsGift false
    [InlineData("Book_Artifact", "Book_Artifact", "asdf", -102, true, false)] // no readable name
    public void Listable(string id, string name, string type, int category, bool canGift, bool expected) =>
        Assert.Equal(expected, GiftTasteMath.Listable(id, name, type, category, canGift));

    // ------------------------------------------------------------------ summary

    [Fact]
    public void A_uniform_group_becomes_one_entry()
    {
        var items = new[] { Fish("Carp", Hate), Fish("Bream", Hate), Fish("Perch", Hate), Fish("Pike", Hate), new GiftItem("Diamond", "gems", Love) };
        Assert.Equal("Loves: Diamond | Likes: none | Dislikes: none | Hates: all fish", GiftTasteMath.Summary(items));
    }

    [Fact]
    public void A_mostly_uniform_group_names_its_exceptions_and_lists_them_under_their_own_taste()
    {
        var items = new[] { Fish("Carp", Dislike), Fish("Bream", Dislike), Fish("Perch", Dislike), Fish("Pike", Dislike), Fish("Sandfish", Love) };
        var s = GiftTasteMath.Summary(items);
        Assert.Equal("Sandfish", Part(s, "Loves"));
        Assert.Equal("most fish (not Sandfish)", Part(s, "Dislikes"));
    }

    [Fact]
    public void Many_exceptions_are_not_named_after_the_group()
    {
        var items = Enumerable.Range(0, 24).Select(i => Fish($"Fish{i:00}", Dislike))
            .Concat(Enumerable.Range(0, 6).Select(i => Fish($"Odd{i}", Neutral))).ToList();
        Assert.Equal("most fish", Part(GiftTasteMath.Summary(items), "Dislikes"));
    }

    [Fact]
    public void Small_mixed_or_neutral_groups_are_listed_by_name()
    {
        var small = new[] { Fish("Carp", Hate), Fish("Bream", Hate), Fish("Perch", Hate) }; // below MinGroup
        Assert.Equal("Bream, Carp, Perch", Part(GiftTasteMath.Summary(small), "Hates"));

        var mixed = new[] { Fish("Carp", Hate), Fish("Bream", Hate), Fish("Perch", Like), Fish("Pike", Like) };
        var s = GiftTasteMath.Summary(mixed);
        Assert.Equal("Perch, Pike", Part(s, "Likes"));
        Assert.Equal("Bream, Carp", Part(s, "Hates"));

        var neutral = new[] { Fish("Carp", Neutral), Fish("Bream", Neutral), Fish("Perch", Neutral), Fish("Pike", Neutral), Fish("Eel", Love) };
        Assert.Equal("Eel", Part(GiftTasteMath.Summary(neutral), "Loves"));
    }

    [Fact]
    public void Kept_items_come_first_and_are_marked()
    {
        var items = new[] { new GiftItem("Amethyst", "", Love), new GiftItem("Pumpkin", "", Love, Have: true), new GiftItem("Cake", "", Love) };
        Assert.Equal("Pumpkin [have], Amethyst, Cake", Part(GiftTasteMath.Summary(items), "Loves"));
    }

    [Fact]
    public void A_folded_group_names_the_kept_items_in_it()
    {
        var items = new[] { Fish("Carp", Hate, true), Fish("Bream", Hate, true), Fish("Perch", Hate, true), Fish("Pike", Hate, true), Fish("Eel", Hate) };
        Assert.Equal("all fish [have: Bream, Carp, Perch +1]", Part(GiftTasteMath.Summary(items), "Hates"));
    }

    [Fact]
    public void Long_lines_are_cut_with_a_count()
    {
        var items = Enumerable.Range(0, 20).Select(i => new GiftItem($"Junk{i:00}", "", Hate)).ToList();
        var hates = Part(GiftTasteMath.Summary(items, maxNamed: 30, maxNamedBad: 5), "Hates");
        Assert.Equal("Junk00, Junk01, Junk02, Junk03, Junk04, +15 more", hates);
    }

    [Fact]
    public void Same_name_twice_is_listed_once_and_special_tea_counts_as_love()
    {
        var items = new[] { new GiftItem("Egg", "", Like), new GiftItem("Egg", "", Like, Have: true), new GiftItem("Tea", "", GiftTasteMath.Special) };
        var s = GiftTasteMath.Summary(items);
        Assert.Equal("Egg [have]", Part(s, "Likes"));
        Assert.Equal("Tea", Part(s, "Loves"));
    }

    // ------------------------------------------------------------------ who likes an item

    [Fact]
    public void Who_with_every_villager()
    {
        var tastes = new List<(string, int)> { ("Penny", Love), ("Abigail", Love), ("Haley", Hate), ("Sam", Neutral), ("Alex", Like) };
        Assert.Equal("loved by Abigail, Penny | liked by Alex | hated by Haley | everyone else is neutral", GiftTasteMath.Who(tastes, complete: true));
        Assert.Equal("everyone is neutral about it", GiftTasteMath.Who(new List<(string, int)> { ("Sam", Neutral) }, complete: true));
    }

    [Fact]
    public void Who_with_what_the_farmer_knows()
    {
        var tastes = new List<(string, int)> { ("Penny", Dislike), ("Sam", Neutral) };
        Assert.Equal("disliked by Penny | neutral for Sam", GiftTasteMath.Who(tastes, complete: false));
    }

    // ------------------------------------------------------------------ finding the item

    private static readonly List<(string Id, string Display, string Name)> Objects = new()
    {
        ("(O)72", "Diamond", "Diamond"), ("(O)348", "Wine", "Wine"), ("(O)24", "Parsnip", "Parsnip"), ("(O)472", "Parsnip Seeds", "Parsnip Seeds"),
        ("(O)410", "Blackberry", "Blackberry"), ("(O)611", "Blackberry Cobbler", "Blackberry Cobbler"), ("(O)Book_Void", "Monster Compendium", "Book_Void"),
        ("(TR)FrogEgg", "Frog Egg", "FrogEgg")
    };

    [Theory]
    [InlineData("diamond", 0)]
    [InlineData(" Parsnip ", 2)] // exact before the longer "Parsnip Seeds"
    [InlineData("72", 0)]
    [InlineData("(O)348", 1)]
    [InlineData("book_void", 6)] // internal name
    [InlineData("seeds", 3)]
    [InlineData("berry", 4)] // the shortest name containing it
    [InlineData("frog egg", 7)] // a trinket
    [InlineData("(TR)FrogEgg", 7)]
    [InlineData("FrogEgg", 7)]
    [InlineData("(O)FrogEgg", -1)] // a qualified id names one type
    [InlineData("pumpkin", -1)]
    [InlineData("", -1)]
    public void Match(string query, int expected) => Assert.Equal(expected, GiftTasteMath.Match(Objects, query));

    [Theory]
    [InlineData("(O)72", "72")]
    [InlineData("(TR)FrogEgg", "FrogEgg")]
    [InlineData("72", "72")]
    public void Bare_ids(string id, string bare) => Assert.Equal(bare, GiftTasteMath.Bare(id));

    [Theory]
    [InlineData("(O)809", true)]  // Movie Ticket: a movie invitation
    [InlineData("(O)277", true)]  // Wilted Bouquet: ends dating
    [InlineData("(O)897", true)]  // Pierre's Missing Stocklist
    [InlineData("(O)72", false)]
    [InlineData("(TR)FrogEgg", false)]
    [InlineData("809", false)]    // only qualified ids
    public void Items_the_game_never_takes_as_gifts(string qid, bool special) => Assert.Equal(special, GiftTasteMath.NotAGiftUse(qid) != null);
}
