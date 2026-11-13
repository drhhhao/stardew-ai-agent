using System;
using System.Linq;
using StardewMCP.Agent;
using Xunit;

namespace StardewMCP.Tests;

/// <summary>
/// The decoupled catalogs (docs/tool-decoupling/README.md): AgentTools.All() keeps every tool including the
/// legacy compatibility entries; AgentTools.Preferred() — the built-in AI's catalog — hides tools marked Legacy.
/// The filter is game-free (ToolCatalog), so it runs here on fake tools; the mod applies the Legacy mark
/// centrally in AgentTools.All() and logs the real contract check at startup.
/// </summary>
public class ToolCatalogTests
{
    private static ToolDef Tool(string name, bool legacy = false, bool instant = true, bool mutates = false,
        bool serialized = false, Func<System.Text.Json.JsonElement, bool>? instantFor = null) => new()
    {
        Name = name,
        Description = "Does " + name + ".",
        Schema = AgentTools.Obj(new { }),
        Instant = instant,
        InstantFor = instantFor,
        Mutates = mutates,
        Serialized = serialized,
        Legacy = legacy,
        Run = (a, c) => Array.Empty<object>().GetEnumerator()
    };

    // ------------------------------------------------------------------ ReadOnly / Mutates / Serialized

    [Fact]
    public void ReadOnly_is_true_only_for_a_pure_zero_time_observation()
    {
        Assert.True(Tool("look").ReadOnly);                       // Instant, no InstantFor, no side effect
        Assert.False(Tool("walk_to", instant: false).ReadOnly);   // takes game time
        Assert.False(Tool("menu", instantFor: _ => true).ReadOnly); // Instant only for some arguments
        Assert.False(Tool("say", mutates: true).ReadOnly);        // mutates despite zero game time
        Assert.False(Tool("give", mutates: true, serialized: true).ReadOnly);
    }

    [Fact]
    public void Serialized_says_zero_time_but_stays_in_the_action_lane()
    {
        // pay / give / name: no game time passes, yet they must stay ordered with the seat's actions.
        var pay = Tool("pay", mutates: true, serialized: true);
        Assert.False(pay.ReadOnly);
        Assert.False(ToolCatalog.UseObservationLane(pay, instant: true, debug: false));
    }

    [Theory]
    // instant, serialized, debug -> observation lane?
    [InlineData(true, false, false, true)]   // look: zero time, not serialized -> observes
    [InlineData(true, false, true, false)]   // a debug call never takes the observation lane
    [InlineData(true, true, false, false)]   // serialized zero-time tool (pay/give/name) -> action lane
    [InlineData(false, false, false, false)] // a plain action -> action lane
    [InlineData(false, true, false, false)]  // serialized without zero time makes no sense; still the action lane
    public void UseObservationLane_routes_only_non_debug_zero_time_unserialized_calls(bool instant, bool serialized, bool debug, bool observe)
    {
        var tool = Tool("x", mutates: true, serialized: serialized);
        Assert.Equal(observe, ToolCatalog.UseObservationLane(tool, instant, debug));
    }

    [Fact]
    public void UseObservationLane_ignores_Mutates_for_the_lane_choice()
    {
        // think / say / emote / journal mutate (chat, bubbles, files) but are not Serialized: they stay zero-time
        // observation-lane tools. Only Serialized moves a zero-time call into the action lane.
        var say = Tool("say", mutates: true);
        Assert.True(ToolCatalog.UseObservationLane(say, instant: true, debug: false));
        var journal = Tool("journal", mutates: true);
        Assert.True(ToolCatalog.UseObservationLane(journal, instant: true, debug: false));
    }

    [Fact]
    public void Preferred_keeps_every_tool_without_the_legacy_mark()
    {
        var tools = new[] { Tool("list_quests"), Tool("walk_to"), Tool("accept_quest") };
        Assert.Equal(tools, ToolCatalog.Preferred(tools));
    }

    [Fact]
    public void Preferred_drops_legacy_tools()
    {
        var tools = new[] { Tool("quests", legacy: true), Tool("list_quests"), Tool("museum", legacy: true) };
        Assert.Equal(new[] { "list_quests" }, ToolCatalog.Preferred(tools).Select(t => t.Name));
    }

    [Fact]
    public void Preferred_preserves_order_and_instances()
    {
        var tools = new[] { Tool("b", legacy: true), Tool("a"), Tool("c") };
        var kept = ToolCatalog.Preferred(tools);
        Assert.Equal(new[] { "a", "c" }, kept.Select(t => t.Name));
        Assert.Same(tools[1], kept[0]);
    }

    [Theory]
    [InlineData("quests")]
    [InlineData("museum")]
    [InlineData("special_orders")]
    public void Phase1_mode_switch_tools_are_centrally_marked_legacy(string name)
    {
        // AgentTools.All() also sets Legacy centrally if a definition ever loses its own mark.
        Assert.Contains(name, ToolCatalog.SupersededBySemantic);
    }

    [Theory]
    [InlineData("craft")]
    [InlineData("cook")]
    [InlineData("crack_geode")]
    [InlineData("buy_animal")]
    [InlineData("animals")]
    [InlineData("build")]
    public void Phase2_mode_switch_tools_are_centrally_marked_legacy(string name)
    {
        Assert.Contains(name, ToolCatalog.SupersededBySemantic);
    }

    [Theory]
    [InlineData("chest")]
    [InlineData("talk_to")]
    public void Phase3_mode_switch_tools_are_centrally_marked_legacy(string name)
    {
        Assert.Contains(name, ToolCatalog.SupersededBySemantic);
    }

    [Theory]
    [InlineData("plant")]
    [InlineData("close_menu")]
    public void Phase4A_tools_with_dangerous_defaults_are_centrally_marked_legacy(string name)
    {
        Assert.Contains(name, ToolCatalog.SupersededBySemantic);
    }

    [Theory]
    [InlineData("go_to_bed")]
    [InlineData("travel_to")]
    [InlineData("wear")]
    public void Phase4B_sleep_travel_and_wear_are_centrally_marked_legacy(string name)
    {
        // go_to_bed -> sleep, travel_to -> travel_to_location / travel_to_shop, wear -> equip (unified clothing entry).
        Assert.Contains(name, ToolCatalog.SupersededBySemantic);
    }

    [Theory]
    [InlineData("go_to_bed")]
    [InlineData("sleep")]
    public void IsSleep_covers_both_sleep_tool_names(string name)
    {
        Assert.True(ToolCatalog.IsSleep(name));
    }

    [Theory]
    [InlineData("rest")]
    [InlineData("wait")]
    [InlineData("travel_to_location")]
    [InlineData(null)]
    public void IsSleep_rejects_everything_else(string? name)
    {
        Assert.False(ToolCatalog.IsSleep(name));
    }

    [Fact]
    public void Menu_and_interact_stay_unmarked_as_the_unknown_scene_fallback()
    {
        // Phase 3 keeps menu and interact in the preferred catalog: they cover menus and tiles no semantic tool names.
        Assert.DoesNotContain("menu", ToolCatalog.SupersededBySemantic);
        Assert.DoesNotContain("interact", ToolCatalog.SupersededBySemantic);
    }

    [Fact]
    public void Semantic_replacements_are_not_in_the_legacy_list()
    {
        var legacy = ToolCatalog.SupersededBySemantic;
        foreach (var name in new[] { "list_quests", "claim_quest_reward", "list_museum_donations", "donate_museum",
                     "claim_museum_rewards", "list_special_orders", "active_special_orders", "accept_special_order",
                     "list_recipes", "craft_item", "list_dishes", "cook_dish", "list_geodes", "crack_geodes",
                     "list_animals_for_sale", "purchase_animal", "list_animals", "pet_animals",
                     "collect_animal_products", "feed_animals", "list_buildings", "suggest_building_site",
                     "construct_building", "upgrade_building",
                     "list_chest", "list_containers", "store_items", "take_items", "read_menu", "choose_mine_floor",
                     "choose_menu_option", "set_menu_amount", "confirm_menu", "open_container", "load_machine",
                     "use_held_item", "read_sign", "talk_to_villager", "give_gift",
                     "plant_item", "dismiss_menu", "accept_letter_quest", "claim_menu_items",
                     "sleep", "travel_to_location", "travel_to_shop", "mine_floor" })
            Assert.DoesNotContain(name, legacy);
    }

    [Fact]
    public void Contract_entry_carries_the_legacy_flag()
    {
        var contract = ApiContract.Build("0.0.0", new[] { Tool("quests", legacy: true), Tool("status") },
            Array.Empty<string>(), new ApiContract.Timing(1, 2, 3));
        Assert.Equal(new[] { ("quests", true), ("status", false) },
            contract.Tools.Select(t => (t.Name, t.Legacy)).OrderBy(t => t.Name).ToArray());
    }
}
