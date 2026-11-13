# Migrating external clients to the semantic tools

Status: active
Started: 2026-11-13
Companion to the plan in [README.md](README.md) (the ledger lives there).

## What changes for clients

Nothing is removed. Every legacy tool keeps its old name, schema and behavior, and stays
registered for MCP and HTTP clients (`AgentTools.All()` is still the compatibility catalog).
What changed is which tools the **built-in AI** receives: `AgentTools.Preferred()` serves
`All()` minus the Legacy marks, so the built-in AI sees only the semantic replacements.
External clients may keep calling legacy tools indefinitely, or migrate row by row using the
table below.

A tool is marked `legacy: true` in `GET /api/contract` (`ToolEntry.Legacy`), so a client can
discover the marks at runtime instead of hard-coding this table. `menu` and `interact` are
**not** Legacy: they stay in the preferred catalog as the generic fallback for unknown menus
and unmatched tile actions, and nothing replaces them.

## API contract (v2)

`ApiVersion` is 2. v2 keeps the v1 timing flags `instant` and `instantFor` and adds the
execution-lane triple (all on `ToolEntry`):

- `readOnly` — the tool never writes game state.
- `zeroTime` — the tool never advances game time (same meaning as v1 `instant`).
- `serialized` — the tool always runs in the serialized action queue. Zero-time state
  mutations (`pay`, `give`, `name`) are `zeroTime` but not `readOnly`, and stay `serialized`
  so they keep their order with the seat's actions.

A tool whose lane depends on its arguments (e.g. `menu` reads with no arguments and acts with
`menu(action=...)`) has all three lane flags false; check `instantFor` for that case.
The v2 result contract also adds a `changed` flag, the `no_change` reason code and
`data.validValues` on rejected calls.

## Legacy tool → semantic replacements

The legacy list is `ToolCatalog.SupersededBySemantic` — 16 tools. Replacements follow one
rule: a read only reads (and is `zeroTime`, no `action` parameter), an action does one named
job and carries a required `reason`.

| Legacy tool | Semantic replacements | What changes |
| --- | --- | --- |
| `quests` | `list_quests` (instant) · `claim_quest_reward(quest?)` | Listing is a pure frozen-time observation; reward money is claimed by its own explicit call. |
| `museum` | `list_museum_donations` (instant) · `donate_museum(item)` · `claim_museum_rewards` | Donating and collecting rewards are separate explicit actions; `item="all"` stays explicit. |
| `special_orders` | `list_special_orders` (walks to the board) · `active_special_orders` (instant) · `accept_special_order(index)` | Reading the board and accepting an order are separate calls; no mode argument. |
| `craft` | `list_recipes` (instant) · `craft_item(recipe, quantity?)` | The mode switch is gone: browse, then craft by exact recipe name. |
| `cook` | `list_dishes` (instant) · `cook_dish(recipe, quantity?)` | Same split for the farmhouse kitchen. |
| `crack_geode` | `list_geodes` (instant) · `crack_geodes(item, quantity?)` | Geode inventory is its own observation; cracking names the geode kind. |
| `buy_animal` | `list_animals_for_sale` (instant) · `purchase_animal(type, building?, name?)` | Browsing Marnie's stock is free; buying is one explicit call. |
| `animals` | `list_animals` (instant) · `pet_animals(name?)` · `collect_animal_products(name?)` · `feed_animals` | The care mode switch becomes three named jobs. |
| `build` | `list_buildings` (instant) · `suggest_building_site(building)` (instant) · `construct_building(building, x, y)` · `upgrade_building(building, x?, y?)` | Listing, site suggestion, new construction and upgrades are separate; upgrades no longer depend on cached names. `upgrade_house` is unchanged. |
| `chest` | `list_chest` (instant) · `list_containers` (instant) · `open_container(x, y)` · `store_items(item/items, x?, y?)` · `take_items(item/items, x?, y?)` | Opening, reading and moving items are explicit; transfers report what did not fit. No chest is crafted or placed implicitly anymore — craft and place a Chest item first. |
| `talk_to` | `talk_to_villager(name)` · `give_gift(name, item)` | Talking never silently gifts; a gift is its own call with a required item. |
| `plant` | `plant_item(item, exactly one of tiles/area/all_empty)` | The implicit whole-map scope is gone; `all_empty=true` additionally requires `confirm=true`. |
| `close_menu` | `dismiss_menu` · `accept_letter_quest` · `claim_menu_items` | Closing never accepts a quest or takes items; each decision is its own call. |
| `go_to_bed` | `sleep(confirm_early?)` | No journal gate; before 17:00 with energy left it refuses without `confirm_early=true`; it never decides an open menu on the way to bed. |
| `travel_to` | `travel_to_location(location)` · `travel_to_shop(shop)` | One intent per call: a map place name, or a shop/keeper (stops at the counter). Villagers are reached with `talk_to_villager` / `give_gift`, not by travel. |
| `wear` | `equip` | Clothing moves into the unified equip tool (hats, shirts, pants, rings, boots, rod tackle). |

## Not legacy

- `menu` and `interact` stay in the preferred catalog as fallbacks: `menu` for menus no
  semantic tool covers, `interact` for tile actions no named tool covers.
- `upgrade_house`, `accept_quest`, `bundles`, `donate_bundle`, `joja`, `name`, `sell`,
  `shop_list` and the rest of the catalog were never mode-switch tools and are unchanged.

## Migration steps for a client

1. Read `ApiVersion` and the `legacy` marks from `/api/contract` at startup instead of
   hard-coding the table.
2. Replace each legacy call with the semantic call(s) of the same job; reads need no `reason`,
   actions require one.
3. Expect v2 result shapes: `changed`, `no_change`, partial results with completed/remaining
   counts, and `data.validValues` when a value is rejected.
4. Keep the legacy names working as a fallback path until the semantic replacements are
   verified in your own integration; both names answer for the same runtime behavior.
