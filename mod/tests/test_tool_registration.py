"""Every group of tools is registered: `AgentTools.All()` is a hand-kept list of the tool groups (`CraftingToolDefs()`,
`SocialTools.All()`...), and a group missing from it would leave its tools silently unreachable on the MCP endpoint
(and absent from /api/contract). The real list needs the game to build, so this reads the source.
"""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
AGENT_DIR = ROOT / "mod" / "StardewMCP" / "Agent"

# `private static List<ToolDef> CraftingToolDefs() => ...`, `public static List<ToolDef> All() => ...`, also IEnumerable / arrays
GROUP_RE = re.compile(
    r"static\s+(?:List<ToolDef>|IEnumerable<ToolDef>|ToolDef\[\])\s+(\w+)\s*\(\s*\)")
CLASS_RE = re.compile(r"\bclass\s+(\w+)")


def groups() -> list[tuple[str, str, str]]:
    """(file, class, method) of every tool group in the top-level Agent files, AgentTools.All itself left out."""
    out = []
    for f in sorted(AGENT_DIR.glob("*.cs")):
        src = f.read_text(encoding="utf-8")
        for m in GROUP_RE.finditer(src):
            classes = CLASS_RE.findall(src[: m.start()])
            cls = classes[-1] if classes else ""
            if (cls, m.group(1)) not in {("AgentTools", "All"), ("AgentTools", "Preferred")}:  # Preferred is a catalog filter, not a tool group
                out.append((f.name, cls, m.group(1)))
    return out


def test_finds_the_groups():
    names = {m for _, _, m in groups()}
    assert {"Core", "CraftingToolDefs", "HorseToolDefs"} <= names


def test_every_group_is_called_from_another_group():
    """Called by name somewhere other than its own definition: from AgentTools.All() or a group it reaches (QolToolDefs)."""
    sources = {f.name: f.read_text(encoding="utf-8") for f in AGENT_DIR.glob("*.cs")}
    everything = "\n".join(sources.values())
    missing = []
    for file, cls, method in groups():
        call = rf"\b{cls}\.{method}\s*\(\s*\)" if method == "All" else rf"(?<![\w.]){method}\s*\(\s*\)|\b{cls}\.{method}\s*\(\s*\)"
        uses = [m for m in re.finditer(call, everything)
                if not re.search(r"static\s+\S+\s+$", everything[max(0, m.start() - 60): m.start()])]
        if not uses:
            missing.append(f"{file}: {cls}.{method}()")
    assert not missing, "tool groups never registered in AgentTools.All(): " + ", ".join(missing)


def test_semantic_group_is_registered():
    """The phase-1 semantic tools join the compatibility catalog; without this line they are unreachable."""
    assert re.search(r"\bSemanticToolDefs\s*\(\s*\)", (AGENT_DIR / "AgentTools.cs").read_text(encoding="utf-8"))


def test_builtin_brain_uses_the_preferred_catalog():
    """MCP/HTTP keep AgentTools.All(); the built-in AI must play with the non-legacy catalog only."""
    src = (AGENT_DIR / "Brain" / "Brains.cs").read_text(encoding="utf-8")
    assert "AgentTools.Preferred()" in src


def test_semantic_folder_contains_only_implementations():
    """ToolDef registrations stay in the top-level shim, never in a second hidden registry."""
    import re

    registration = re.compile(r'^\s*Name\s*=\s*"', re.MULTILINE)
    for path in (AGENT_DIR / "Semantic").glob("*.cs"):
        assert not registration.search(path.read_text(encoding="utf-8")), \
            f"unexpected ToolDef registration in {path.name}"


def test_semantic_tools_have_no_mode_switch_argument():
    """Each preferred semantic tool has one intent instead of an action=list/write mode; the count pins the roster."""
    from tool_source_parser import read_tools

    tools = read_tools(AGENT_DIR / "SemanticTools.cs")
    names = {t.name for t in tools}
    expected = {
        # phase 1: quests, museum, special orders
        "list_quests", "claim_quest_reward", "list_museum_donations", "donate_museum", "claim_museum_rewards",
        "list_special_orders", "active_special_orders", "accept_special_order",
        # phase 2: crafting, cooking, geodes, animals, buildings
        "list_recipes", "craft_item", "list_dishes", "cook_dish", "list_geodes", "crack_geodes",
        "list_animals_for_sale", "purchase_animal", "list_animals", "pet_animals", "collect_animal_products",
        "feed_animals", "list_buildings", "suggest_building_site", "construct_building", "upgrade_building",
        # phase 3: containers, menus, one-tile actions, villagers
        "list_chest", "list_containers", "store_items", "take_items", "read_menu", "choose_mine_floor",
        "choose_menu_option", "set_menu_amount", "confirm_menu", "open_container", "load_machine",
        "use_held_item", "read_sign", "talk_to_villager", "give_gift",
        # phase 4A: planting with an explicit scope; menu closing without hidden decisions
        "plant_item", "dismiss_menu", "accept_letter_quest", "claim_menu_items",
        # resource-first progression to the next elevator floor
        "mine_floor",
    }
    assert names == expected, f"semantic roster drifted: missing {sorted(expected - names)}, extra {sorted(names - expected)}"
    assert len(tools) == 44  # 24 from phases 1-2 + 15 from phase 3 + 4 from phase 4A + mine_floor
    assert all("action" not in tool.properties for tool in tools)


# --------------------------------------------------------------------------- phase 4B: sleep and travel

def test_sleep_travel_group_is_registered():
    """The phase-4B semantic tools join the compatibility catalog; without this line they are unreachable."""
    assert re.search(r"\bSleepTravelToolDefs\s*\(\s*\)", (AGENT_DIR / "AgentTools.cs").read_text(encoding="utf-8"))


def test_sleep_travel_roster():
    """sleep, travel_to_location and travel_to_shop: one intent each, registered exactly once (43 + 3 = 46 semantic tools)."""
    from tool_source_parser import read_all

    tools = [t for t in read_all(AGENT_DIR) if t.name in {"sleep", "travel_to_location", "travel_to_shop"}]
    assert sorted((t.name, t.file) for t in tools) == [
        ("sleep", "AgentTools.cs"), ("travel_to_location", "AgentTools.cs"), ("travel_to_shop", "AgentTools.cs")]
    assert all(not t.problem for t in tools), [t.problem for t in tools]
    assert all("action" not in t.properties for t in tools)


def test_sleep_confirm_early_is_optional():
    """sleep requires only reason; confirm_early is the explicit opt-out of the wide-awake refusal."""
    from tool_source_parser import read_tools

    tools = {t.name: t for t in read_tools(AGENT_DIR / "AgentTools.cs")}
    assert "reason" in tools["sleep"].required
    assert "confirm_early" not in tools["sleep"].required
    assert tools["sleep"].properties["confirm_early"] == "boolean"


def test_travel_semantics_take_their_own_argument():
    """travel_to_location takes location, travel_to_shop takes shop; both keep reason required."""
    from tool_source_parser import read_tools

    tools = {t.name: t for t in read_tools(AGENT_DIR / "AgentTools.cs")}
    for name, arg in [("travel_to_location", "location"), ("travel_to_shop", "shop")]:
        assert tools[name].required == [arg, "reason"], f"{name}: required is {tools[name].required}"
