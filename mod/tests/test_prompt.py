"""Prompt regressions for the built-in AI (Phase 6): the shipped system prompt and the day messages
(Prompts.Morning / Prompts.Nudge) must recommend only the preferred semantic tools, keep the daily
cadence, and keep the language/memory placeholders. Reads the sources directly — no game, no build."""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SYSTEM_PROMPT = ROOT / "mod" / "StardewMCP" / "assets" / "system_prompt.md"
PROMPTS_CS = ROOT / "mod" / "StardewMCP" / "Agent" / "Brain" / "Prompts.cs"

# Tool names with a semantic replacement: the built-in AI never sees them (ToolCatalog.Legacy), so a
# prompt that recommends one sends the model after a tool that is not in its catalog.
LEGACY_NAMES = [
    "go_to_bed", "plant", "craft", "cook", "crack_geode", "buy_animal", "animals", "build",
    "chest", "talk_to", "close_menu", "quests", "museum", "special_orders", "travel_to", "wear",
]

# The preferred names the prompt must keep recommending (backticked, as a recommendation looks).
SEMANTIC_NAMES = [
    "sleep", "plant_item", "list_recipes", "craft_item", "list_dishes", "cook_dish",
    "store_items", "take_items", "list_containers", "open_container", "talk_to_villager", "give_gift",
    "list_buildings", "construct_building", "upgrade_building", "purchase_animal",
    "pet_animals", "collect_animal_products", "feed_animals", "list_quests", "claim_quest_reward",
    "list_museum_donations", "donate_museum", "dismiss_menu", "accept_letter_quest",
    "claim_menu_items", "travel_to_location", "travel_to_shop", "read_sign", "load_machine",
]

CADENCE_PHRASES = [
    "1–3 goals",                                    # few morning goals
    "finish one goal before starting the next",     # one goal at a time
    "Prepare before you travel",                    # pack items, check hours and route before leaving
    "Sightings are a bonus, not a detour",          # pick up only on the route, serving a goal
    "Re-observe after a failure or an interruption; a successful action needs no re-check",
    "the only other reason to re-observe",          # ... or when coordinates are missing
    "Start wrapping up about 22:00",                # evening wrap-up
    "`sleep` never refuses over a missing journal",  # journal stays the memory habit; sleep does not gate
    "`menu` and `interact` are only for menus and tiles none of them covers",
]

PLACEHOLDERS = ["{MODEL_NAME}", "{PLAYER_NAME}", "{LANGUAGE}", "{TIME_MODE}", "{OTHERS}", "{GUIDE}",
                "{GIFT_RULE}", "{SELF}", "{PEOPLE}", "{LESSONS}", "{PLAN}", "{NOTES}"]


def test_system_prompt_recommends_only_preferred_tools():
    text = SYSTEM_PROMPT.read_text(encoding="utf-8")
    for legacy in LEGACY_NAMES:
        assert f"`{legacy}`" not in text, f"system prompt still recommends legacy tool `{legacy}`"


def test_system_prompt_recommends_the_semantic_replacements():
    text = SYSTEM_PROMPT.read_text(encoding="utf-8")
    for name in SEMANTIC_NAMES:
        assert f"`{name}`" in text or f"`{name}(" in text, f"system prompt lost the preferred tool `{name}`"


def test_system_prompt_keeps_the_daily_cadence():
    text = SYSTEM_PROMPT.read_text(encoding="utf-8")
    for phrase in CADENCE_PHRASES:
        assert phrase in text, f"daily cadence regressed, missing: {phrase!r}"


def test_system_prompt_keeps_the_placeholders_and_protocol():
    text = SYSTEM_PROMPT.read_text(encoding="utf-8")
    for ph in PLACEHOLDERS:
        assert ph in text, f"placeholder {ph} lost"
    # Session-break protocol and the journal's plan sections survive the tool renames.
    assert "`Session break:`" in text
    assert "`## Season`, `## Week`, `## Today`" in text
    # The memory habit stays even though sleep no longer demands it.
    assert "Call `journal` (no game time) at the latest before `sleep`" in text


def test_prompts_cs_day_messages_end_the_day_with_sleep():
    src = PROMPTS_CS.read_text(encoding="utf-8")
    assert "go_to_bed" not in src, "Prompts.cs still mentions the legacy sleep tool"
    for method in ("Morning", "Nudge"):
        body = re.search(rf"public static string {method}\(.*?(?=\n    (?:public static|///))", src, re.DOTALL)
        assert body, f"Prompts.{method} not found"
        assert "`sleep`" in body.group(0), f"Prompts.{method} does not end the day with `sleep`"
    morning = re.search(r"public static string Morning\(.*?(?=\n    (?:public static|///))", src, re.DOTALL).group(0)
    assert "1–3 goals" in morning, "the morning message no longer sets the day's goal count"
