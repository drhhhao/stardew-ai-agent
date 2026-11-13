using System;
using System.IO;
using System.Linq;
using StardewMCP.Agent;
using Xunit;

namespace StardewMCP.Tests;

/// <summary>The guide tool without the game: topic ids and aliases, the summary line, and reading a folder of guide files.</summary>
public class GuideTopicsTests : IDisposable
{
    private static readonly string[] Ids =
        { "overview", "spring", "summer", "fall", "winter", "year2", "festivals", "money", "community_center",
          "mines", "fishing", "relationships", "progression", "animals" };

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "guide_test_" + Guid.NewGuid().ToString("N"));

    public GuideTopicsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private void WriteTopic(string id, string summary, string body) =>
        File.WriteAllText(Path.Combine(_dir, id + ".md"), $"<!-- summary: {summary} -->\n{body}\n");

    [Theory]
    [InlineData("spring", "spring")]
    [InlineData("SPRING", "spring")]
    [InlineData("  Money ", "money")]
    [InlineData("community_center", "community_center")]
    [InlineData("Community Center", "community_center")]
    [InlineData("community-center", "community_center")]
    [InlineData("cc", "community_center")]
    [InlineData("Bundles", "community_center")]
    [InlineData("joja", "community_center")]
    [InlineData("mining", "mines")]
    [InlineData("combat", "mines")]
    [InlineData("skull_cavern", "mines")]
    [InlineData("Skull Cavern", "mines")]
    [InlineData("gifts", "relationships")]
    [InlineData("marriage", "relationships")]
    [InlineData("friendship", "relationships")]
    [InlineData("skills", "progression")]
    [InlineData("professions", "progression")]
    [InlineData("tools", "progression")]
    [InlineData("buildings", "progression")]
    [InlineData("house", "progression")]
    [InlineData("greenhouse", "year2")]
    [InlineData("late", "year2")]
    [InlineData("Year 2", "year2")]
    [InlineData("crops", "money")]
    [InlineData("processing", "money")]
    [InlineData("kegs", "money")]
    [InlineData("spring1", "spring")]
    [InlineData("festival", "festivals")]
    [InlineData("animal", "animals")]
    [InlineData("mine", "mines")]
    [InlineData("relationship", "relationships")]
    [InlineData("'fishing'", "fishing")]
    [InlineData("money.md", "money")]
    [InlineData("overview", "overview")]
    public void Resolves_ids_and_aliases(string topic, string expected) =>
        Assert.Equal(expected, GuideTopics.Resolve(topic, Ids));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("dragons")]
    [InlineData("s")]
    public void Unknown_topics_resolve_to_nothing(string? topic) =>
        Assert.Null(GuideTopics.Resolve(topic, Ids));

    [Fact]
    public void An_alias_needs_its_file()
    {
        Assert.Null(GuideTopics.Resolve("cc", new[] { "overview", "spring" }));
        Assert.Equal("spring", GuideTopics.Resolve("spring1", new[] { "overview", "spring" }));
    }

    [Fact]
    public void Splits_the_summary_line_off()
    {
        var (summary, body) = GuideTopics.Split("\uFEFF<!-- summary: Plant early, sell late. -->\r\n\r\n## When this matters\r\nSpring 1.\r\n");
        Assert.Equal("Plant early, sell late.", summary);
        Assert.Equal("## When this matters\nSpring 1.", body);
    }

    [Fact]
    public void A_file_without_a_summary_keeps_its_text()
    {
        Assert.Equal(("", "## Priorities\n1. Water."), GuideTopics.Split("## Priorities\n1. Water.\n"));
        Assert.Equal(("Only a summary", ""), GuideTopics.Split("<!-- summary: Only a summary -->"));
    }

    [Fact]
    public void Index_is_in_reading_order_without_the_overview()
    {
        var ids = GuideTopics.Sort(new[] { "zz_extra", "money", "overview", "spring", "animals", "aa_extra" });
        Assert.Equal(new[] { "spring", "money", "animals", "aa_extra", "overview", "zz_extra" }, ids);
        var index = GuideTopics.Index(new[] { ("overview", "The year."), ("spring", "First season."), ("money", "") });
        Assert.Equal("Topics: spring - First season | money", index);
    }

    [Fact]
    public void Book_without_a_topic_gives_the_overview_then_the_topics()
    {
        WriteTopic("overview", "Roadmap.", "# Year one\nDo the farm.");
        WriteTopic("spring", "Spring plan.", "## Priorities\n1. Parsnips.");
        WriteTopic("money", "How to earn.", "## Priorities\n1. Kegs.");
        var book = new GuideBook(_dir);

        var (ok, text) = book.Answer(null);
        Assert.True(ok);
        Assert.Equal("# Year one\nDo the farm.\n\nTopics: spring - Spring plan | money - How to earn", text);
        Assert.DoesNotContain("<!--", text);
        Assert.Equal(text, book.Answer("Overview").Text);
    }

    [Fact]
    public void Book_gives_a_topic_by_alias_without_its_summary()
    {
        WriteTopic("overview", "Roadmap.", "Overview text.");
        WriteTopic("money", "How to earn.", "## Priorities\n1. Kegs.");
        var (ok, text) = new GuideBook(_dir).Answer("Kegs");
        Assert.True(ok);
        Assert.Equal("## Priorities\n1. Kegs.", text);
    }

    [Fact]
    public void Book_lists_topics_for_an_unknown_one()
    {
        WriteTopic("overview", "Roadmap.", "Overview text.");
        WriteTopic("fishing", "Rods and spots.", "Fish.");
        var (ok, text) = new GuideBook(_dir).Answer("dragons");
        Assert.False(ok);
        Assert.Equal("No guide topic \"dragons\". Topics: fishing - Rods and spots", text);
    }

    [Fact]
    public void Book_rereads_a_changed_file_and_forgets_a_removed_one()
    {
        WriteTopic("overview", "Roadmap.", "Old text.");
        WriteTopic("fishing", "Rods.", "Fish.");
        var book = new GuideBook(_dir);
        Assert.StartsWith("Old text.", book.Answer(null).Text);

        var overview = Path.Combine(_dir, "overview.md");
        WriteTopic("overview", "Roadmap.", "New text.");
        File.SetLastWriteTimeUtc(overview, File.GetLastWriteTimeUtc(overview).AddSeconds(5));
        File.Delete(Path.Combine(_dir, "fishing.md"));

        var (ok, text) = book.Answer(null);
        Assert.True(ok);
        Assert.Equal("New text.\n\nTopics: (none)", text);
        Assert.False(book.Answer("fishing").Ok);
    }

    [Fact]
    public void Book_fails_clearly_without_files()
    {
        var (ok, text) = new GuideBook(Path.Combine(_dir, "missing")).Answer(null);
        Assert.False(ok);
        Assert.Contains("guide files are missing", text);
    }

    [Fact]
    public void Book_without_an_overview_still_lists_the_topics()
    {
        WriteTopic("spring", "Spring plan.", "Spring.");
        var (ok, text) = new GuideBook(_dir).Answer("");
        Assert.True(ok);
        Assert.Equal("Topics: spring - Spring plan", text);
    }

    [Fact]
    public void Shipped_guide_files_start_with_a_summary()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "StardewMCP", "assets")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var guide = Path.Combine(dir!.FullName, "StardewMCP", "assets", "guide");
        Assert.True(File.Exists(Path.Combine(guide, "overview.md")), "assets/guide/overview.md is missing");
        foreach (var file in Directory.GetFiles(guide, "*.md"))
        {
            var (summary, _) = GuideTopics.Split(File.ReadAllText(file));
            Assert.False(summary.Length == 0, $"{Path.GetFileName(file)}: line 1 must be <!-- summary: ... -->");
        }
        var ids = Directory.GetFiles(guide, "*.md").Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
        Assert.All(ids, id => Assert.Equal(id, id.ToLowerInvariant()));
    }
}
