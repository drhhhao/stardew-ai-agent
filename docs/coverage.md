# Game coverage

How the AI farmers reach every part of Stardew Valley, and where the gaps are.
Status is for `main`, with open PRs noted. Update a row when a PR changes its coverage.

✅ works · 🟡 reachable but untested or partial · ❌ missing

## Principle

A human plays the whole game with six inputs. The AI gets each input in a form it can use
(a tile, a name or a label instead of pixels), and every input calls the game's own handler.
Every system behind that handler is then covered without new code.

| Input | Human | AI tool | Game entry point | Status |
|---|---|---|---|---|
| Move | WASD | `walk_to`, `travel_to` | pathfinder, warps, doors; `WalkController` (a `PathFindController` that does not stop a tick on each tile) and no `Game1.UpdateControlInput` on that screen while it walks, so the game's input step does not halt the farmer every tick. A walk that stands still for 90 running ticks re-plans around where villagers stand (up to 3 times); a message popping up mid-walk is a locked door: closed, `no_path`. A farmer walled in on all four sides (a villager walked through it into a wall) steps out to an open diagonal tile. A villager standing in the only way (a doorway) does not fail the plan: the walk goes on and the game lets the farmer past. A blocked/unreachable target moves to the nearest open tile and returns a partial result naming the obstacle and the right clearing tool; known shop trips are refused before opening, after closing, on closed days/festivals and within 30 minutes of closing (HHMM converted to real minutes) | ✅ `travel_to` also takes shop owners and villagers (#9); Phase 4B splits the preferred travel into `travel_to_location` (place names only) and `travel_to_shop` (shops and their owners only; villagers are reached by `talk_to_villager`/`give_gift`), new and untested in game; `travel_to` stays as the legacy entry; the step out of a wall (smoke `wall_step_out`), a villager in the only doorway (smoke `doorway_villager`) and a walk frozen while another AI thinks checked in game (#77); 🟡 the smooth stride; the locked-door message never showed in game, because the plan already avoids locked doors |
| Action button | right-click | `interact(x, y, holding?)` | `Game1.pressActionButton`: the held item's use action or eating, else `tryToCheckAt` → `GameLocation.checkAction` | ✅ `holding` picks the item; handlers that wait for a fresh press (`didPlayerJustRightClick`: chests, shipping bin, fish ponds) see it |
| Use button | left-click | `use_tool`, `plant_item`, `place` | `Farmer.BeginUsingTool`, `Utility.tryToPlaceItem` | 🟡 `place` puts items on free ground (smoke `place`, not yet run in game); `plant_item` plants only on tilled soil, with an explicit `tiles` / `area` / `all_empty` scope (`all_empty` also needs `confirm`); `plant` stays as the legacy entry |
| Dialogue answer | click an option | `respond(option)` | `DialogueBox` → `answerDialogueAction` or the question's callback | ✅ |
| Menu | click, type | `menu`; `read_menu`, `choose_mine_floor`, `choose_menu_option`, `set_menu_amount`, `confirm_menu`; `dismiss_menu`, `accept_letter_quest`, `claim_menu_items`; `shop_list`, `buy`, `close_menu` | the menu's own click and handler methods | 🟡 reader, fallback, `take`/`put`, `choose`, `amount`, `type`, `ok`/`cancel`; Phase 3 adds the semantic wrappers (new, not yet run in game) while `menu` stays as the fallback for unknown menus; Phase 4A adds `dismiss_menu` (close only, no quest accept, no item take), `accept_letter_quest` and `claim_menu_items`, new and untested; `close_menu` stays as the legacy entry; `buy`/`craft` adapters to come; selling is the `sell` tool (#11) |
| Minigame | timing | `fish` (BobberBar only) | `BobberBar`, `IMinigame` | 🟡 fishing only (not yet run in game), other `IMinigame`s ❌ |

Rules:

- **Call the game's entry point; never re-implement it.** The old `buy` (#7) did the purchase
  itself and skipped recipe learning, chat messages and stock sync.
- **Bespoke tools are macros.** They batch primitives (`use_tool` on 40 tiles) or play things that
  need timing (fishing, combat). Nothing may be reachable only through a macro.
- **The mod does the mechanics; the AI makes every choice.** A screen that asks something goes to
  the AI. Only screens with no choice are clicked automatically.
- **Check the decompiled game before planning a system.** Find its tile action, dialogue answer or
  menu class and put it in the row. #2 planned the backpack as a shop item; it is the `BuyBackpack`
  tile action.

### Target tools

- `interact(x, y, holding?)`: hold an item, then press the action button. `checkAction` then covers
  loading machines (every machine in `Data/Machines`), gifts and quest items for NPCs, gifts and
  proposals to other farmers, fish ponds and festival pots. Without a tile it uses the held item
  in place (`performUseAction`): totems, books. Phase 3 splits the intents where a wrong
  right-click has consequences into `load_machine`, `use_held_item` and `give_gift`; `interact`
  stays as the generic fallback, not a legacy entry.
- `place(item, tiles)` (#3): `plant` without the tilled-soil check, same `Utility.tryToPlaceItem`. It covers machines,
  chests, sprinklers, fences, paths, furniture, bombs, mine staircases and tappers.
- `craft(recipe, quantity)` and `chest(x, y, action, item, quality)` (#3) are macros over the game's own code:
  `CraftingRecipe` (the recipe the crafting page uses) and `interact` + `menu take/put` (the chest's `ItemGrabMenu`).
  `menu take/put` also takes `quality`, so the primitive stays complete. Phase 2 splits each list from its make:
  `list_recipes` ⚡ / `craft_item` and `list_dishes` ⚡ / `cook_dish`; `craft` and `cook` stay as the legacy entries.
  Phase 3 splits the chest macro the same way: `open_container` + `list_chest` ⚡ / `store_items` / `take_items` and
  the whole-farm ledger `list_containers` ⚡; `chest` stays as the legacy entry (its `stash` and `create` modes have
  no semantic replacement yet).
- `read_menu()` reads the open menu: its type, title, items with counts or prices, and the actions it
  allows. `menu(action, ...)` acts through a small adapter per menu family:
  - `take` / `put`: chests, fridge, fishing treasure, Lost and Found, shipping bin
  - `choose`: list menus, mine elevator, professions, proposals
  - `amount`: number entry (send money)
  - `type`: naming
  - `buy` / `sell`, `craft`, `ok` / `cancel`
  - A menu without an adapter falls back to listing its `allClickableComponents` by label and
    clicking one, so unknown menus stay usable.

  Phase 3 turns the frequent operations into named tools (`choose_mine_floor`, `choose_menu_option`,
  `set_menu_amount`, `confirm_menu`) on top of the instant `read_menu`, each with a required `reason`
  and no mode-switch `action`; `menu` stays as the fallback for menus without one.

  Phase 4A turns closing a menu into `dismiss_menu` (close only — it never accepts a quest or takes items);
  accepting a letter's quest and taking offered items are the explicit `accept_letter_quest` and `claim_menu_items`.

  `shop_list`, `buy`, `close_menu` (legacy; its closing/accept/claim behavior is unchanged) and `choose_profession`
  (PR #47) can become adapters later.
  Renaming them is not needed now.

## Coverage matrix

### Farm and home

| System | How a human does it | Covered by | Status | Issue |
|---|---|---|---|---|
| Clear, till, water, chop, mine | tool on a tile | `use_tool`; `clear_area`, `till_area` for rectangles | ✅ single tiles; 🟡 `clear_area`/`till_area` untested (smoke `farm_batch`) | #15 |
| Plant seeds, fertilizer | item on soil | `plant_item` (`tiles`, `area`, or `all_empty: true` with `confirm: true`) | ✅ tiles (via the legacy `plant`); 🟡 `plant_item` area mode untested (smoke `farm_batch`); the whole-map scope now needs an explicit confirm, untested; `plant` stays as the legacy entry | #15 |
| Harvest crops, forage | action on a tile | `harvest`; `harvest_all` for the whole map; `walk_to`, `travel_to` and `talk_to` list the forage, artifact and seed spots, berry bushes and dropped items passed within 6 tiles of the route (`Sightings`) | ✅ tiles; ✅ `harvest_all` and the on-the-way list (dropped items, #77); loose quartz in the mines is picked up with `harvest` | #15 |
| Whole-farm routine | look at every plot, water, collect | `farm_summary`, `water_all` (`whose: mine` keeps to your own crops; the result says whose were watered), `harvest_all` | 🟡 untested (smoke `farm_batch` covers summary and water; refill and rain need manual play) | #15 |
| Place machines, chests, sprinklers, fences, furniture | use button with the item | `place` | 🟡 smoke `place`, `craft_place_chest` written, not yet run in game | #3 |
| Load a machine | action while holding the input | `load_machine`; `interact(holding)` | ✅ smoke `furnace` | #3 |
| Collect from a machine | action | `interact` | ✅ smoke `furnace` | #3 |
| Chests, fridge | action → `ItemGrabMenu` | `open_container` then `list_chest` ⚡ / `store_items` / `take_items`; `list_containers` ⚡ for the whole-farm ledger; `interact`, then `menu take/put`; `chest` does it in one call (by name and quality, several items, "everything"; `stash` finds or crafts/places a Chest with 50 Wood and stores the pack, `create` places one) | ✅ chests (smoke `chest`); 🟡 `chest` (smoke `chest_tool`) and fridge untested; owners and theft: #52; 🟡 `ChestLedger` (`chest(action=all)`, changes since your last use, `chests.jsonl`) untested in game; ✅ waiting for a chest another farmer has open (smoke `chest_wait`, #77); a chest lock held with no menu open is released after a second (`ChestLocks`). Phase 3 adds `list_chest` ⚡, `list_containers` ⚡, `open_container`, `store_items` and `take_items`, new and untested; `chest` stays as the legacy entry | #3 |
| Ship | shipping bin → `ItemGrabMenu` (`Farm.shipItem`) | `ship` | ✅ next morning's briefing lists what was sold per item (smoke `briefing`) | #29 |
| Eat | action holding food → "Eat?" | `eat` | ✅ eats the chosen slot (the game's `eatHeldObject` eats the last item picked up; a 0 stack it left is dropped at day start) | |
| Sleep | bed → `Sleep_Yes` → ready check `sleep` | `sleep`: no journal required first (writing the diary is the runner's policy, not a game rule); wide awake before 17:00 with plenty of energy it warns once and wants `confirm_early: true`; the legacy `journal`-then-`go_to_bed` path is unchanged (`go_to_bed` refuses until today's journal is written; wide awake before 17:00 it says so once). While it waits in bed for the others an event (@mention, gift, arrival) gets the farmer up with the ready check's own Cancel, never on its walk home. A client with `?reply_within=N` (agy) is told its day is over while it waits; calls it sends after that are refused, and a question in its night gets the game's last answer (a naming box a random name) | ✅ one player via `go_to_bed` (smoke `journal`); 🟡 `sleep` (Phase 4B) new and untested in game; 🟡 untested with 4 seats; wake-up untested in game; an unknown menu open for 10 s is closed (#32) | #38, #21, #32 |
| Rest in bed | a multiplayer farmer on a bed tile regains +1 energy and health every 0.5 s of game time | `rest(minutes?)`: walk to your bed and lie there until energy is full, the minutes pass or an event stops it (once in bed); the bed's own "Go to sleep?" question is closed; refused with no other farmer online (no regeneration then) | 🟡 untested in game | |
| Pass out (2:00, health 0, exhaustion) | automatic; the day ends without a bed | `pass_out` record in `/api/state` and the log (cause, place, running action, money, items lost); `POST /api/release_freeze` lets the clock run out; next morning the runner asks for `journal(day=N)` first | 🟡 smoke `pass_out` (2:00 AM) written, not run yet; health 0 in the mines untested | #22 |
| TV: weather, luck, recipes, tips, fishing | action on the TV → question | `watch_tv(channel)` (macro over `interact` + `respond`: picks the channel, reads every page, reports the learned recipe): weather is tomorrow's forecast (festivals and island), fortune is today's luck, Queen of Sauce teaches on Sunday and reruns Wednesday, Livin' Off The Land gives Monday/Thursday tips, fishing (after Pam's letter) gives the season's fish/hours/weather/places. Weather and luck also come free in `status`, dates and shop hours in `calendar` | 🟡 watch_tv weather/fortune exercised in game (day 6); Queen/tips/fishing still untested; no smoke case | #14, #11 |
| Backpack upgrade | `BuyBackpack` tile → `Backpack_Purchase` | `look` labels the tile ("Backpack upgrade to 24 slots (2000g…)"), then `interact` + `respond` | 🟡 smoke `backpack` added, not yet run | #2 |
| Totems, books | action button while holding the item (`performUseAction`) | `use_held_item`; `interact(holding)` without a tile | ✅ totems (smoke `totem`); 🟡 books untested | |
| Animal care | pet (action), milk/shear (tool), hay (hopper) | `animals` (pet / collect / feed: `FarmAnimal.pet`, the Milk Pail or Shears swing, the hopper tile); `interact` and `use_tool` also work. Phase 2 adds `list_animals` ⚡, `pet_animals`, `collect_animal_products` and `feed_animals`, new and untested; `animals` stays as the legacy entry | 🟡 untested in game (PR for #10) | #10 |
| Horse | stable, name, mount (action) | `interact`, `mount`, `dismount` (horse `checkAction`) | 🟡 riding untested; smoke `mount` self-skips without a horse | #10, #54 |

### Shops, crafting, upgrades

| System | How a human does it | Covered by | Status | Issue |
|---|---|---|---|---|
| Buy | shop counter → `ShopMenu` | `shop_list`, `buy` | ✅ (PR #45: `tryToPurchaseItem`, recipes, trade items) | #7 |
| Sell to a shop | click an inventory item in `ShopMenu` | `sell(item, quantity)`: clicks the inventory slot, the shop decides what it buys | 🟡 untested; smoke `sell_to_shop`; right-click selling one item is assumed | #11 |
| Tool upgrades | `Blacksmith` → `Blacksmith_Upgrade` → `ShopMenu` | `buy` (trade items, PR #45) | 🟡 smoke `tool_upgrade` added, not yet run; the two-day wait and pickup are untested | #2 |
| Geodes | `Blacksmith_Process` → `GeodeMenu` | `crack_geode` (macro: pays Clint 25g and draws `Utility.getTreasureFromGeode`; no `GeodeMenu` clicks, so the `menu` adapter is still missing). Phase 2 adds `list_geodes` ⚡ and `crack_geodes`, new and untested; `crack_geode` stays as the legacy entry | 🟡 PR for #14, not yet run in game (smoke `crack_geode`) | #14 |
| Crafting | crafting tab or `Craft` tile → `CraftingPage` | `craft` (macro over `CraftingRecipe`: list, make N). Phase 2 adds `list_recipes` ⚡ and `craft_item` (the named recipe is required; no empty call crafts), new and untested; `craft` stays as the legacy entry | 🟡 smoke `craft`, `craft_place_chest` written, not yet run in game; the page itself has no `menu` adapter | #3 |
| Cooking | `kitchen` tile → `CraftingPage` (cooking) | `cook` (macro over `CraftingRecipe(key, true)`: pack plus fridge ingredients; the page itself has no `menu` adapter). Phase 2 adds `list_dishes` ⚡ and `cook_dish` (the named recipe is required; no empty call cooks), new and untested; `cook` stays as the legacy entry | 🟡 PR for #14, not yet run in game (smoke `cook`) | #14 |
| Buildings, house upgrade, renovation | `Carpenter` → `carpenter_*` → `CarpenterMenu`, `RenovateMenu` | `build`, `upgrade_house`: `Farm.buildStructure`, the menu's upgrade step (`upgradeName`, `daysUntilUpgrade`, `MarkUnderConstruction`) and the `upgrade_Yes` answer, costs from `Game1.buildingData`. Phase 2 adds `list_buildings` ⚡, `suggest_building_site` ⚡, `construct_building` (building and tile required) and `upgrade_building` (the safety guard stays and its queue choice no longer comes from cached building names), new and untested; `build` stays as the legacy entry | 🟡 untested in game (PR for #10); building upgrades (Big Coop...) added, untested in game; renovation is missing | #10 |
| Buy animals | `AnimalShop` → `Marnie_Purchase` → `PurchaseAnimalsMenu` → `NamingMenu` | `buy_animal`: creates the `FarmAnimal`, `AnimalHouse.adoptAnimal`; prices from `Game1.farmAnimalData`. Phase 2 adds `list_animals_for_sale` ⚡ and `purchase_animal` (the animal type is required; no empty call buys), new and untested; `buy_animal` stays as the legacy entry | 🟡 untested in game (PR for #10); the random brown chicken or cow colour is not handled | #10 |
| Rings, boots, hats, shirts, pants | equipment slots in `InventoryPage` | `equip` (`Farmer.Equip`, else the slot field plus `onEquip`/`onUnequip`; Phase 4B adds shirts and pants to `equip`, new and untested in game); `status` and `inventory` show them | 🟡 untested | #27, #54 |
| Bait, tackle | drop onto the rod in `InventoryPage` | `equip` (the rod's own `attach`); `inventory` shows what is on the rod | 🟡 untested | #27 |
| Clothes (hats, shirts, pants) | equipment slots in `InventoryPage` | `equip` (Phase 4B, new and untested), `wardrobe`; the legacy `wear` entry keeps its old behavior | 🟡 untested; smoke `wear` | #54 |
| Tailoring, dye, forge | `Tailoring`, `DyePot`, `Forge` tiles | menu adapter | ❌ | |
| Trash cans | `Garbage` tile | `search_trash` (presses the tile; `trash_dive` event with `seen_by`) | 🟡 smoke `search_trash` | #14, #54 |
| Trash an item | inventory trash can → `Utility.trashItem` | `trash(item, quantity, confirm)` (never tools; valuables need `confirm`) | 🟡 PR for #14, not yet run in game (smoke `trash`) | #14 |

### People

| System | How a human does it | Covered by | Status | Issue |
|---|---|---|---|---|
| Know the villagers | social page | `villagers` (location, hearts, counters, birthdays, tastes found out; `item=X` says who loves or hates X; with `RevealGiftTastes` or `GuideKnowledge` every taste from `NPC.getGiftTasteForThisItem`); farmhand screens read where everyone is from the host's world (`NpcInfo.TrackHost`), since a farmhand only keeps the maps a farmer stands on up to date | 🟡 untested in game | #9 |
| Talk | action on an NPC | `talk_to` (finds the villager, travels across maps, follows a villager who walks on and talks from any of the 8 tiles around them, as `tryToCheckAt` allows; reports points before/after); a fellow farmer's name or `Nicknames` walks up to them on the same map and points at `say`. Phase 3 adds `talk_to_villager`, new and untested; `talk_to` stays as the legacy entry | ✅ villagers, and the farmer walk-up by name and nickname (#77) | #9 |
| Gifts | action holding the gift | `give_gift`: the named carried item through the game's gift path, so reaction and taste are learned and recorded; `talk_to(gift_item)` on the legacy entry; `interact(holding)` gifts are not recorded. Phase 3 adds `give_gift`, new and untested | 🟡 untested in game | #9 |
| Heart events | cutscenes with questions | `PlayHeartEvents` (off): story events play, stop at questions for `respond`; other events are skipped | 🟡 opt-in, untested; in split-screen the event only moves while no other AI is thinking | #9 |
| Dating, marriage | give a bouquet (8 hearts), later a Mermaid Pendant (10 hearts, upgraded house) | `bouquet`, `mermaid_pendant` (the game's gift path through `talk_to`; requirements shown in `villagers`); the wedding is the game's own, watched and logged | 🟡 untested in game | #53 |
| Children | night question (`DialogueBox`), then `NamingMenu` | `go_to_bed` hands the question back; `respond` answers it, `name` names the child | 🟡 untested in game; the AI is not forced to want a child | #53 |
| Naming pets, animals, horse | `NamingMenu` | `name(text)`; status shows "Naming: ..."; an unanswered box gets a random name at bedtime or at 1:00 AM | 🟡 untested in game | #53 |
| Mail | mailbox → `LetterViewerMenu` | `interact` + `close_menu` | ✅ attachments are claimed; the morning briefing and the footer (`📬 N letters`) say when mail is waiting and where the farmer's own mailbox is (a farmhand's is at its cabin; smoke `briefing`); 🟡 `close_menu` also accepts the letter's quest (`AcceptQuest`), untested. Phase 4A splits this: `dismiss_menu` closes only, `accept_letter_quest` accepts the quest, `claim_menu_items` takes the attachment — new and untested; `close_menu` stays as the legacy entry | #29, #2 |
| Help Wanted, quest log | `Billboard` tile, `QuestLog` | `quests` (journal, board, who took it, Special Orders read-only), `accept_quest` (macro: walks to the board, presses Accept); `quests(action=claim_reward)` opens `QuestLog` and clicks a finished quest's reward box, the only place the game pays it (🟡 untested in game); events `quest_accepted`/`quest_completed`/`quest_failed`/`quest_taken_by_other` go to the player log feed. Phase 1 adds the semantic pair `list_quests` ⚡ / `claim_quest_reward`, new and untested; `quests` stays as the legacy entry | 🟡 PR for #14, not yet run in game (smoke `quests`, `accept_quest`); turning in a quest: `talk_to` with the item | #14 |
| Special Orders | `SpecialOrders` tile → `SpecialOrdersBoard`, its Accept button | `special_orders` (`list`, `accept`, `active`); `ActiveSpecialOrdersText()` is for `quests` (#14). Phase 1 adds `list_special_orders` (walks to the board), `active_special_orders` ⚡ and `accept_special_order`, new and untested; `special_orders` stays as the legacy entry | 🟡 untested | #27 |
| Community Center | `JunimoNoteMenu` | `bundles` (reads `Bundles`/`BundleData`; counts each missing item in the pack and every chest and fridge, says where it comes from; `action=keep` checks the pack for what to keep or ship; `GuideKnowledge` previews the bundles before the Community Center opens), `donate_bundle` (opens the room's `JunimoNoteMenu` and clicks the bundle, the inventory slot and an ingredient slot; success is read back from `Bundles`) | 🟡 untested, needs the CC open | #13 |
| Joja | Morris's membership question, `JojaCDMenu` | `joja`: `projects` lists the five projects and their state from the mail flags; `membership` shows a warning and buys nothing unless `confirm=true`, then answers Morris's yes/no dialogue (global, irreversible: closes the bundles for everyone, announced in chat, recorded on the log and as the `joja_membership` event); `buy_project` opens the game's own `JojaCDMenu` and clicks the project's box, found by its price, so the game takes the gold and sets its own flags (`joja_project_bought` event) | 🟡 untested, JojaMart + dialogue and form members late-bound | #26 |
| Museum | Gunther → `MuseumMenu` | `museum`: `list` via `isItemSuitableForDonation`; `donate` writes `museumPieces` on the next free display tile (not the `MuseumMenu` UI, which needs pointer coordinates); `rewards` answers Gunther's "Collect" and takes the grab menu. Phase 1 adds `list_museum_donations` ⚡, `donate_museum` and `claim_museum_rewards`, new and untested; `museum` stays as the legacy entry | 🟡 untested | #13 |

### Adventure

| System | How a human does it | Covered by | Status | Issue |
|---|---|---|---|---|
| Fishing | cast, then `BobberBar` | `fish(tries)`: shore spot, cast power, bite, BobberBar driver, treasure. A chest is chosen once when it appears, and only when it is close enough to the fish for the bar to cover both; otherwise it is ignored, so the bar cannot ping-pong between fish and chest | 🟡 written, not yet run in game (smoke `fish`); what bites where is `fish_info` (see [Knowledge](#knowledge)); bait/tackle `attach` still open | #4 |
| Crab pots | place, then action holding bait | `place`, `interact(holding)` | ❌ | #4 |
| Mine ladders, shafts | action on a tile | `interact` | ✅ | |
| Mine elevator | `MineElevator` → `MineElevatorMenu` | `choose_mine_floor`; `menu choose` | ✅ smoke `mine_elevator` | #2 |
| Combat | weapon swings, aim | `fight`: best weapon, type-specific attack tiles and hit-and-run for chargers/ranged enemies, support-target priority, `Farmer.BeginUsingTool` only with clear line of sight (blocked lines reposition; `seconds` is optional, default 10); `eat_below` eats through the `eat` tool's path (`Farmer.eatHeldObject`); `monsters()` lists every living monster on the map with HP, damage, distance and a one-line tactic, and `monsters(name)` reads the codex (base stats from Data/Monsters + `MonsterRules`); a `RockCrab` in its shell is hit with the Pickaxe (`RockCrab.hitWithTool`), an armored `Bug`, a `Mummy` (bomb/Crusader needed), an invulnerable `Spiker` and a monster whose health does not move in 8 swings are left alone | 🟡 `monsters` live list and codex exercised in game (day 6); `fight` not yet smoke-tested | #5, #6 |
| Mine progression | gather resources, reveal down ladder, stop at next multiple of 5 | `mine_floor`: loose items and valuable nodes first, fight only threats or an infested-floor requirement, plain stones to reveal the ladder, stop at the next elevator floor | 🟡 new and untested in game |
| Mine survival | watch health, eat, climb out | runtime interrupt when a monster within 3 tiles hurts the farmer (every action except `fight`, `eat`, `leave_mine`); `fight` stops at 30% HP; self-defense between actions (a monster within 2 tiles is hit with the best weapon, the pickaxe for a shelled rock crab, and the farmer eats at `SelfDefenseEatPercent` — no pause, see [configuration.md](configuration.md#time-modes)); `leave_mine` walks to the up ladder (tile 115), answers Leave, and the game exits to the Mine (Skull Cavern: the Skull Cave entrance); fallback warp to the same; the footer shows HP and warns from 11 PM | 🟡 self-defense swings, eating, damage lines and the no-retreat fallback exercised in game (day 6); no smoke case yet; smoke `skull_cavern` covers the Skull Cavern exit | #6, #55 |
| Festivals | arrive on time → ready check `festivalStart` → actions, shops, games | `calendar` (dates, hours), `travel_to`, `festival(start/leave)`, `talk_to` reaches festival villagers and booths, `interact(holding)` for the Luau soup | 🟡 untested; the Fair and ice-fishing games are not covered | #11 |
| Egg hunt (spring 13) | after Lewis's countdown the `eggHunt` sequence turns the festival map's `fest` Paths tiles into solid `Prop`s in `Event.festivalProps` (52 s `festivalTimer`); the action button on an egg (`GameLocation.checkAction` → `Event.checkAction`) adds to `festivalScore`; `eggHuntWinner` picks the most eggs (at least 9/6/5/4 for 1/2/3/4+ players) | `look` shows eggs as `E` (Paths tile rule plus a prop still colliding), `status` shows time left, eggs left and every farmer's count; `festival(eggs, max?)` advances the countdown lines, then walks to the nearest egg and presses it until the hunt ends; `interact` on an egg works too. The timer stops with the host pause, so thinking costs no hunt time (split-screen) | 🟡 builds, not yet run in game; that a pick clears the egg on the other screens too (`FarmerTeam.festivalPropRemovalEvent`) is the game's multiplayer path, untested with 3 seats | |
| Night events: fairy, witch, meteorite, Qi plane | automatic (`Game1.farmEvent`) | `NightEvents` reports them as an `Overnight:` line and a `night_event` log entry | 🟡 random, no smoke case | #54 |
| Arcade, darts, crane, casino minigames | `IMinigame`, `CalicoJack`, `Slots` | drivers (optional) | ❌ | |
| Bus to Calico Desert | Bus Stop ticket machine → "Buy a ticket to the desert?" (500g; Pam at the wheel) | `travel_to Desert`: the game's `BusStop.checkAction`, the Yes answer and the ride (no fare re-implemented) | ✅ smoke `desert_and_bus` | #55 |
| Desert, Oasis, Casino | bus, Oasis door, back room (Club Card) | `travel_to Desert`, `Oasis`, `Casino`; shops through `shop_list`/`buy` (Qi coins print as "Qi coins") | ✅ smoke `desert_and_bus` | #55 |
| Skull Cavern | Skull door → `Game1.enterMine(121)`; up ladders; no elevator | `travel_to Skull Cavern` (needs the Skull Key and the bus); `status`/`look` say "Skull Cavern floor N"; `leave_mine` exits to the Skull Cave entrance | ✅ smoke `skull_cavern` | #55 |
| Ginger Island | Willy's back room → donate 5 batteries, 200 hardwood and 5 iridium bars → boat ticket (1,000g) | repair: `interact(holding)` on the three donation tiles + `respond`; `travel_to Ginger Island` after `willyBoatFixed`; `status` counts `Golden Walnuts N/130`; island doors through `travel_to` | ✅ smoke `ginger_island` | #55 |
| Movie theater | Town door → box office shop → ticket, then the entrance question | `travel_to MovieTheater` (needs `ccMovieTheater`); `interact` the box office, then `shop_list`/`buy` | ✅ smoke `movie_theater` | #55 |
| Secret notes, journal scraps | use the note in the inventory (the magnifying glass finds them) | `interact(holding="Secret Note")` → `LetterViewerMenu`; `dismiss_menu` (or the legacy `close_menu`) | ✅ smoke `secret_note` | #55 |

### Knowledge

Reference tools read the game's own data (`Game1.cropData`, `Game1.objectData`, `Data/Shops`, `Data/Locations`, `Data/Fish`, `Data/Machines`, the recipe files...), so they are always right and cover modded content. They are instant observations: no game time. `item_info`, `fish_info` and `bundles` share one reverse index of where each item comes from (`KnowledgeSources`: crops, fruit trees, animals, fish, crab pots, forage, artifact spots, geodes, monsters, machines, buildings, shops), built once per save. What normal play leaves to discovery (all gift tastes, bundles before the Community Center opens, legendary fish below their Fishing level, friendship recipe sources) is shown only with `GuideKnowledge` (see [configuration.md](configuration.md#all-settings)).

| System | How a human does it | Covered by | Status | Issue |
|---|---|---|---|---|
| Crop planning: seasons, growth, prices | the wiki, the seed shop | `crops(season?, fertilizer?)`: last day to plant, harvests that fit, seed prices from every shop and festival, sell prices, gold per tile per day, fruit trees; `season=greenhouse`; speed fertilizers and Agriculturist cut growth days like `HoeDirt.applySpeedIncreases` | 🟡 smoke `knowledge`, not yet run in game; greenhouse and fertilizer untested in game | #16 |
| Item facts: sell price, food, shops, sources, uses | the wiki, the collections tab | `item_info(name)`: objects, big craftables, weapons, tools, boots and hats; prices with professions, food and buffs, shops, where it is found, what keeps it (bundles, Museum, quests, Special Orders), best machine outputs, recipe unlocks and uses | 🟡 smoke `knowledge`, not yet run in game; the non-object items untested in game | #16 |
| Fishing spots: what bites where and when | the wiki, the fish tab of the collections | `fish_info(location?, fish?, season?, weather?, time?)`: `Data/Locations` and `Data/Fish` read for season, hours, weather and Fishing level (other conditions are left to the game), mine floor fish (`MineShaft.getFish`), crab pot catches, bundle fish; spawn chances are not shown | 🟡 untested in game | #4 |
| Machines: what to process, what is running | the wiki; walking past each machine | `machines(item? / machine?)`: `Data/Machines` outputs with prices, times and gain per machine-day; no arguments lists every machine on the farm, its buildings and the farmhouse as ready / working / empty. `farm_summary` adds the count, `look` the time left per machine | 🟡 untested in game | |
| Progress: skills, tools, mines, museum | the skills page, Clint, the Guild, Gunther | `progress`: skill XP and the recipes and professions ahead, the next tool upgrade (`Data/Tools`) and bars on hand, deepest Mines and Skull Cavern floors, Monster Slayer goals (`Data/MonsterSlayerQuests`), museum rewards (`Data/MuseumRewards`) | 🟡 untested in game | |
| Strategy: what to do when | the wiki, guides, experience | `guide(topic?)`: the mod's own pages in `assets/guide/*.md` (year roadmap, seasons, money, Community Center, mines, fishing, relationships, progression, animals), only with `GuideKnowledge` | 🟡 untested in game | |

### Multi-AI

Use the game's multiplayer features first. Add mod rules only where the game has nothing.

| System | How the game does it | Covered by | Status | Issue |
|---|---|---|---|---|
| Separate wallets | host reads the ledger in Lewis's house (`LedgerBook`); it switches overnight | `interact` + `respond` | 🟡 untested | |
| Send money | ledger → `SendMoney` → recipient → `DigitEntryMenu`; chat shows "X sent Y" | `interact` + `respond` + `menu amount` | ✅ smoke `send_money` (split-screen) | |
| Talking | chat box, every line reaches every player (`ChatBox.receiveChatMessage`) | `say` writes the line only into the chat box of the screens that show the speaker (same map, inside the viewport: `ScreenState.Sees`) and above the speaker's head; no whisper | 🟡 smoke `chat` written, not run yet | #39 |
| Loans, debts | no mechanic | a promise made face to face, paid with Send money | story | #39, #48 |
| Give an item to a player | action holding the item → `SendProposal(Gift)`: one item, the receiver may decline | `give` (PR #50 moves the stack directly, no decline). Phase 5: `pay`, `give` and `name` are zero-time serialized mutations — no game time, but they write state one call at a time instead of running as pure observations | ✅ different path; 🟡 Phase 5 serialization new and untested in game | #39 |
| Marriage between players | `SendProposal(Marriage)` → `PendingProposalDialog` | `propose(to)` and `answer_proposal(accept)`; the other player is told in their next tool result | 🟡 untested in game | #53 |
| Dance, baby between players | `SendProposal(Dance/Baby)` → `PendingProposalDialog` | `interact(holding)` + `menu choose` | ❌ | |
| Owned plots and chests | no mechanic | mod rule (`Ownership`): chests and machines carry `Object.owner`, crops record their planter in `HoeDirt.modData`; taking from or harvesting another seat's claim logs a `theft` entry | ✅ smoke `ownership` | #52, #40 |
| Seeing what others do, being interrupted | no mechanic (the game draws the other farmers and nothing more) | mod rule (`Perception`): `look` and `status` list the other AI players in view with the tool they run, its reason and how long, next to the last `say` of theirs this player heard. An @mention, gift, a player coming into view or entering your house, a theft (`Ownership.Theft`), rain starting and crows on the farm add an `Events:` block to the next tool result and stop a running `wait`, `walk_to`, `travel_to` or farm action (`AgentRuntime.Interrupt`). P4 neither sees nor is seen | 🟡 written, smoke `caught_slacking` not run yet; damage and monster interrupts wait for #6 | #40 |
| Sleep, festival start/end | `ReadyCheckDialog` waits for every online farmer, P4 included | AI seats wait inside their action: `travel_to_location` (the legacy `travel_to`) into a festival, `festival(start)` and `festival(leave)` (through `Event.TryStartEndFestivalDialogue`) and `sleep` in bed wait up to 60 s of running world time, tell the other AIs, then cancel with `not_ready`; a ready check is not a yes/no box for `answer_proposal`; P4 accepts automatically; `Bedtime` warns late seats; `SleepWaitWatch` closes a message box left open while another farmer waits in bed | ✅ smoke `bedtime`, `sleep_together`; 🟡 festival waits and `SleepWaitWatch` untested in game | #52, #38, #11 |

### Context control (#30)

What the AI reads and how long a session lasts. These are not game systems; the rows say where the
text size comes from.

| System | How it works | Covered by | Status | Issue |
|---|---|---|---|---|
| Map size | `WorldText.Describe` scan of a square window | `look` default radius 8 (17x17), legend limited to the symbols in view | 🟡 untested in game | #30 |
| Object list filter | the "Things in view" list of the same scan | `look(kinds=[...])` | 🟡 untested in game | #30 |
| What changed | per-seat snapshot of the last scan on that map | `look(diff=true)`; falls back to the full view when most of the window is new | 🟡 untested in game | #30 |
| Result footer | `AgentTools.Footer()` (time, place, energy, money, menu, professions) | joined into one line in `AgentRuntime.Finish`; Phase 5 keeps it on action results and drops it from pure observations (only a relevant change or event is added) | 🟡 untested in game; the Phase 5 change is new and untested in game | #30 |
| Tool result contract | text tags (`[partial · reason]`, `[no_path]`...) | MCP `structuredContent`: `ok`, `reason`, `partial` + `stoppedBecause`, `changed` with explicit `no_change`, batch `done`/`remaining`/`skipped`, optional `validValues` for a wrong argument; an explicit `no_change` is not loop progress | 🟡 Phase 5 in source, new and untested in game | #31 |
| Session break | none (the game has no such thing); `SessionBreaks.cs` adds a `Session break:` line at set times or after N calls / M characters, then refuses actions | opt-in with `/mcp?breaks=1`; `think`, observations and `sleep` still work (the legacy `go_to_bed` too); `run_days.py` starts the next part with a handoff | 🟡 untested in game | #30 |
| Token log | `claude -p` stream-json `result` event | `runner/usage_log.py` writes `runs/usage.csv`, one row per part | 🟡 tested on fake transcripts only | #30 |

## Choice policy

What `AgentRuntime.AutoHandleOverlays` does with screens that appear by themselves.

| Screen | Today | Target |
|---|---|---|
| `ShippingMenu`, `LevelUpMenu` with no choice | clicks OK | keep |
| `LevelUpMenu` profession | picks the left one | the AI chooses (#8, PR #47) |
| Letter with a quest | the legacy `close_menu` accepts the quest; Phase 4A adds `dismiss_menu` (close only), `accept_letter_quest` and `claim_menu_items` (new, not yet run in game) | the AI chooses |
| `ItemGrabMenu` that is not a chest (fishing treasure, gift, fish pond output) | `close_menu` first takes what fits and names what is left; Phase 4A adds the explicit `claim_menu_items` (new, not yet run in game) | keep; `menu take` picks single items |
| `NamingMenu` | the AI answers with `name(text)`; a box nobody answers gets a random name at bedtime, at 1:00 AM, with no AI on the seat, or after the thinking timeout plus a minute | keep |
| Question in a cutscene | skips the event, then answers option 0; with `PlayHeartEvents`, heart events wait for `respond` (option 0 after the think timeout) | the AI answers with `respond` for every event |
| `PendingProposalDialog` | the AI accepts or declines with `answer_proposal`; nothing answers for it | keep |
| `ReadyCheckDialog` | not handled | AI seats wait; P4 accepts automatically |
| Any other menu | "Menu open: X"; `menu` lists its labelled buttons (`click`, `ok`, `cancel`) | keep |
| Unknown menu during `sleep`'s wait for the new day (the legacy `go_to_bed` the same; not `ShippingMenu`, `LevelUpMenu`, `SaveGameMenu`, `ReadyCheckDialog`) | waited 5 min, then the call failed | after 10 s: log its type, `exitThisMenu` (#32) |

## Build order

1. ✅ `interact(holding)`; `menu` with the reader, the fallback, `take`/`put`, `choose`, `amount` and
   `type`; fix the chest label. This unlocks machines, chests, the fridge, simple menus and Send money.
2. `place`: `plant` for every placeable item. (#3 adds `place`, `craft` and `chest`; waiting for a local test.)
3. Choice policy: naming, cutscene questions, proposals, P4 ready checks.
4. Menu adapters, most useful for the videos first: craft/cook, sell, quests and Special Orders,
   animals and buildings, bundles and museum, geodes, equipment.
5. `look` labels: readable names for tile actions, plus animals and other farmers.
6. Drivers: fishing, combat, festival games, casino.
7. ✅ Late game (#55): the bus, Willy's boat, Skull Cavern, movie theater, and secret notes are fully supported and verified by in-game smoke tests.

## Keeping it complete

- A PR that adds coverage updates its row and adds a smoke case (`runner/smoke.py` in the studio repo, which drives `/api/debug/*`). A 🟡 row needs a
  smoke case before it becomes ✅.
- Where to look for gaps in the decompiled game:
  - `GameLocation.performAction`: about 120 tile actions. The `performAction` overrides in
    `StardewValley.Locations` add a few dozen more (for example `LedgerBook`, `Concessions`).
  - `GameLocation.answerDialogueAction`: about 90 answers, plus overrides.
  - `StardewValley.Menus`: about 100 classes, about 30 used during play. `StardewValley.Minigames`:
    about 20.
  - `ProposalType` (Gift, Marriage, Dance, Baby); night events in `Utility.pickFarmEvent` and
    `pickPersonalFarmEvent`.
