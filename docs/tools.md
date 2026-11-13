# Tools

[ภาษาไทย](tools.th.md) · **English** · [← README](../README.en.md)

Every tool the AI can call, grouped by job. Each farmer's MCP endpoint (`/mcp`, `/mcp/p1` … `/mcp/p3`) offers all of them.

- ⚡ = costs no game time — that is all it promises, it no longer means "only reads". A ⚡ tool can still write game
  state: `pay`, `give` and `name` are zero-time mutations that run one at a time (serialized), not pure reads. Pure
  observations (⚡ and read-only) run in the observation lane: they answer while an action runs, and their result
  skips the repeated status footer. Every tool without ⚡ is an **action**: it runs in game
  time, one at a time per farmer, and takes a `reason` string that viewers see on the overlay (left out of the tables below).
- 🔁 = legacy compatibility: kept callable for existing MCP clients, but the built-in AI gets the newer, split tools
  instead; `/api/contract` marks these `legacy: true` so external clients can migrate deliberately.
- Arguments in *italics* are optional.
- The definitions live in the top-level files of [`mod/StardewMCP/Agent/`](../mod/StardewMCP/Agent/); what each reply
  looks like is in [HTTP API → Tool results](api.md#tool-results).

## Look and think

| Tool | Arguments | What it does |
|---|---|---|
| `status` ⚡ | | Day, time, weather (and tomorrow's), luck, place, energy, money, held item (a weapon shows its damage), what is worn, pending choices and any open dialogue or menu |
| `look` ⚡ | *`radius`*, *`kinds`*, *`diff`* | The surroundings as a tile map (17×17 by default, radius 8) with a legend and a list of things in view, other players included. `kinds` (e.g. `["crops","debris"]`) limits the list; `diff=true` lists only the tiles that changed since the last look (a machine's clock counting down is not a change). Ore and gem nodes are named (Copper Node, Gem Node...); a machine shows what it holds and the time left |
| `inventory` ⚡ | | Items in the pack, what is worn and what is on the fishing rod; weapons show their damage |
| `farm_summary` ⚡ | | Whole-map farm report: empty tilled soil, crops by type with days to harvest, unwatered, ready and dead crops, crops that won't mature this season, forage, debris, watering can and nearest water, and how many machines on the whole farm are ready, working or empty (`machines` for detail) |
| `think` ⚡ | `text` | A private thought, shown on the overlay. Nobody in the game sees it |

## Move

| Tool | Arguments | What it does |
|---|---|---|
| `walk_to` | `x`, `y` | Pathfind to a clear tile on the current map. If the exact target is blocked or unreachable, it moves to the nearest open tile instead and returns a partial `no_path` result naming the obstacle and the right clearing tool (Pickaxe for stones, Axe for trees/twigs, Scythe for weeds) or an adjacent tile to choose next. The result also lists useful finds passed on the way. Villager replanning, locked-door and wedged-in-wall handling stay in place |
| `travel_to_location` | `location` | Go to another map — a place name only (Town, Beach, Mountain, Ginger Island...). Known shop trips are refused before opening, after closing, on closed days/festivals and within 30 minutes of closing; check `calendar`. Other closed doors say when they open. Lists what was seen on the way, like `walk_to`. In co-op the game lets farmers into a festival only together: it waits up to a minute of running time for the others (they are told), then gives up with `not_ready` instead of leaving you stuck in the box. When the nearest way out to the next map can't be reached (a locked bedroom door in the way), it tries the other ways out. A shop or its owner goes with `travel_to_shop`; a villager is reached by `talk_to_villager` or `give_gift`, not by travel |
| `travel_to_shop` | `shop` | Go to a shop, named by the shop or its owner (Pierre, Clint, Marnie, Willy, Gus, Robin...), and stop at the counter so `shop_list` and `buy` work on it. Known shop trips are refused before opening, after closing, on closed days/festivals and within 30 minutes of closing, with advice to do something else first; check `calendar`. What was seen on the way is listed, like `walk_to`. A villager's name is not a shop — to talk or give a gift, use `talk_to_villager` / `give_gift` |
| `travel_to` 🔁 | `location` | (Legacy; the built-in AI uses `travel_to_location` and `travel_to_shop` instead.) Go to another map. A shop owner's or villager's name also works; a closed door says when it opens. Lists what was seen on the way, like `walk_to`. In co-op the game lets farmers into a festival only together: it waits up to a minute of running time for the others (they are told), then gives up with `not_ready` instead of leaving you stuck in the box. When the nearest way out to the next map can't be reached (a locked bedroom door in the way), it tries the other ways out |
| `mount`, `dismount` | | Ride the horse (needs a Stable) and get off |

## Farm

| Tool | Arguments | What it does |
|---|---|---|
| `use_tool` | `tool`, `tiles`, *`max_hits`* | Use a tool on one or more tiles (hoe, watering can, axe, pickaxe, scythe...) |
| `plant` 🔁 | `item`, *`tiles`* or *`area`* | (Legacy; the built-in AI uses `plant_item` instead.) Plant seeds or fertilizer on tilled soil: listed tiles, an `[x, y, w, h]` area, or no tiles at all = every empty tilled tile on the map |
| `plant_item` | `item`, one of `tiles` / `area` / `all_empty` + `confirm` | (action) Plant seeds or fertilizer on tilled soil with an explicit scope, exactly one of: listed `tiles`, an `[x, y, w, h]` `area`, or `all_empty: true` with `confirm: true` for every empty tilled tile on the map. With none of the three — or with `all_empty` but no `confirm` — nothing is planted |
| `harvest` | `tiles` | Harvest crops or forage on the given tiles |
| `water_all` | *`whose`* | Water every dry crop on the map (`whose: "mine"` = only your own); refills the can at the nearest water it can reach and says whose crops it watered |
| `harvest_all` | | Collect every ready crop and forage item on the map |
| `clear_area` | `x`, `y`, `w`, `h` | Clear a rectangle (up to 100 tiles) with the right tool; large stumps and boulders are skipped and reported |
| `till_area` | `x`, `y`, `w`, `h` | Till a rectangle with the hoe |
| `place` | `item`, `tiles` | Put items, machines, chests, sprinklers, fences or furniture on the ground |

The batch tools (`water_all`, `harvest_all`, `clear_area`, `till_area`, `plant_item` area mode) stop with a reason (low
energy, a menu, a late hour, a full pack) and say what is left. `use_tool`, `plant_item` (and the legacy `plant`) and
`harvest` always report `done`, `remaining` and `skipped` tiles.

`tiles` takes `[[x,y],...]` and also the shapes models send instead: `[{"x":1,"y":2}]`, `["1,2"]`, one string
`"[(1, 2), (3, 4)]"` or a flat `[x1,y1,x2,y2]`. A single `tile`, or `x` and `y`, works when `tiles` is missing. Numbers may
be numeric strings (`"12"`) or whole decimals (`12.0`), in every tool.

## Game knowledge ⚡

Read from the game's own data (the guide from the mod's own pages), costs no game time.

| Tool | Arguments | What it does |
|---|---|---|
| `crops` ⚡ | *`season`*, *`fertilizer`* | Season planner: growth and regrow days, last day to plant, harvests that fit, seed prices from every shop and festival, sell prices (normal quality), gold per tile per day; marks the seeds you carry, warns which crops die at the season change and lists the fruit trees. `season=greenhouse` plans the Greenhouse; `fertilizer` (`speed_gro`, `deluxe_speed_gro`, `hyper_speed_gro`) shortens growth as the game does (Agriculturist counted when you have it) |
| `item_info` ⚡ | `name` | Any item: objects, machines and other big craftables, weapons, tools, boots and hats. Sell prices by quality (also with your professions), what eating it does, shops that sell it and for how much, where it is found (crops, trees, animals, fishing spots, forage, geodes, monsters, machines), what to keep it for (open bundles, Museum, quests, Special Orders), the best machine outputs, how to learn its recipe and the recipes that use it. Weapons show damage, speed and crit; tools their upgrade cost |
| `fish_info` ⚡ | *`location`*, *`fish`*, *`season`*, *`weather`*, *`time`* | Which fish bite where and when. No arguments: the fish that can bite here now (map, season, time, weather, your Fishing level) with price, hours, weather, difficulty, whether you caught it and whether a bundle needs it; then this map's fish at other times or seasons, what crab pots catch here and bundle fish biting elsewhere now. `location` shows another map, `fish` every map and hour for one fish; `season`, `weather` (`rain` or `sun`) and `time` change the conditions checked. Legendary fish show once your Fishing level reaches theirs, or always with `GuideKnowledge` |
| `machines` ⚡ | *`item`*, *`machine`* | `item=X`: every machine that takes X, with the output, its sell price (with your professions), the time, the gain per item and per machine-day, and the best pick. `machine=X`: what it takes, makes and needs as fuel. No arguments: every machine on the farm, in its buildings and in the farmhouse, grouped ready / working (with the ready time) / empty, with tiles |
| `progress` ⚡ | | Your progress in one view: each skill's level, XP to the next level and the recipes it unlocks, your professions and the choices ahead, the next tool upgrade at Clint's with its price and the bars you have, a tool being upgraded, the deepest Mines and Skull Cavern floors and the elevator, the Adventurer's Guild slayer goals, and the museum donations with the next rewards |
| `calendar` ⚡ | *`days`* | Today and the next days: festivals, birthdays, the next festival, the main shops' hours and closed days |
| `villagers` ⚡ | *`name`*, *`item`* | Everyone: where they are, hearts, talked and gifted today and this week, birthdays. With a name: what a date or a proposal still needs, your last gift, their gift tastes and what they would make of the items you carry. `item=X`: who loves, likes, dislikes or hates X (with `name`, just that villager). Only the tastes found out so far (gifts, hints), unless `RevealGiftTastes` or `GuideKnowledge` shows all of them from the game data. A fellow farmer's name says to talk with `say` |
| `quests` ⚡ 🔁 | *`action`*, *`quest`* | (Legacy; the built-in AI uses `list_quests` + `claim_quest_reward` instead.) Quest journal with story, giver, progress and reward; finished quests whose money is not collected yet; today's Help Wanted request and who took it; Special Orders. `action: "claim_reward"` (an action, not ⚡) opens the journal and collects a finished quest's money: `quest` is its title, or leave it out for the first one |
| `guide` ⚡ | *`topic`* | The strategy guide, offered only with [`GuideKnowledge`](configuration.md#all-settings) on: no topic gives the year roadmap and the topic list; a topic (`spring`, `summer`, `fall`, `winter`, `year2`, `festivals`, `money`, `community_center`, `mines`, `fishing`, `relationships`, `progression`, `animals`, or a word like `cc` or `gifts`) gives that page. Exact numbers come from the tools above and `bundles` |

## Items, crafting and cooking

| Tool | Arguments | What it does |
|---|---|---|
| `craft` 🔁 | *`recipe`*, *`quantity`* | (Legacy; the built-in AI uses `list_recipes` + `craft_item` instead.) Craft from the recipes you know; `craft()` lists them |
| `list_recipes` ⚡ | | The crafting recipes you know, with ingredients and how many of each you can make right now; an instant observation |
| `craft_item` | `recipe`, *`quantity`* | (action) Craft the named recipe; `recipe` is required — nothing is crafted without one |
| `cook` 🔁 | *`recipe`*, *`quantity`* | (Legacy; the built-in AI uses `list_dishes` + `cook_dish` instead.) Cook the dishes you know in the farmhouse kitchen, from the pack and the fridge; `cook()` lists them |
| `list_dishes` ⚡ | | The cooking recipes you know, with ingredients counted from the pack and the fridge and what you can make right now; an instant observation |
| `cook_dish` | `recipe`, *`quantity`* | (action) Cook the named dish in the farmhouse kitchen; `recipe` is required — nothing is cooked without one |
| `chest` 🔁 | *`x`*, *`y`*, *`action`*, *`item`*, *`items`*, *`quality`*, *`quantity`* | (Legacy; the built-in AI uses `list_chest`, `list_containers`, `open_container`, `store_items` and `take_items` instead. `stash` and `create` remain available on this legacy entry.) Store or take items in a chest or the fridge in one call: by name and quality, several items, or "everything". `action=stash` finds the nearest chest (or crafts and places a Chest with 50 Wood) and stores everything except tools, seeds and cooked dishes; `action=create` only makes/places one. The result adds what others put in or took out since you last used that chest. `action=all` lists every chest and fridge on the farm with whose it is, what it holds and the recent changes (no walking). A chest another farmer has open is waited for (up to 10 s of running time) and opened again |
| `list_chest` ⚡ | *`item`*, *`items`*, *`quality`* | What the currently open chest, fridge or grab menu holds, as an instant observation: the item stacks with qualities and the free slots, optionally filtered. It only reads a container that is already open — open one with `open_container` first; never unfreezes time |
| `list_containers` ⚡ | | The whole farm's containers as one instant ledger: every chest and fridge with whose it is, what it holds and what others put in or took out since you last used each one. No walking, game time stays frozen |
| `open_container` | `x`, `y` | (action) Walk next to the chest or fridge on tile `x`, `y` and open it; then `list_chest`, `store_items` and `take_items` work on it. A chest another farmer has open is waited for (up to 10 s of running time) and opened again |
| `store_items` | `item`/`items`, *`quality`*, *`quantity`* | (action) Store items from the pack into the open container: one named item, several names, or `item: "everything"`. `quality` limits the move to one quality; `quantity` caps how many of each (default: all). Reports what moved and what was skipped or left over — nothing is silently truncated |
| `take_items` | `item`/`items`, *`quality`*, *`quantity`* | (action) Take items from the open container into the pack: one named item, several names, or `item: "everything"`. `quality` limits the move to one quality; `quantity` caps how many of each (default: all). Reports what moved and what did not fit — nothing is silently truncated |
| `equip` | `item`, *`slot`*, *`count`* | The one entry for everything wearable: put on a hat, shirt, pants, a ring or boots (the old one goes back), or attach bait and tackle to the fishing rod |
| `wear` 🔁 | `item` | (Legacy; the built-in AI uses `equip` instead.) Put on a hat, shirt or pants from the pack |
| `wardrobe` ⚡ | | What you wear and the clothes you carry |
| `eat` | `item` | Eat or drink something |
| `trash` | `item`, *`quantity`*, *`quality`*, *`confirm`* | Throw items away (never tools; valuables need `confirm`) |
| `crack_geode` 🔁 | *`item`*, *`quantity`* | (Legacy; the built-in AI uses `list_geodes` + `crack_geodes` instead.) Clint cracks geodes for 25g each |
| `list_geodes` ⚡ | | The geodes you carry, by type and count; an instant observation |
| `crack_geodes` | `item` | (action) Clint cracks the named geodes for 25g each; `item` is required — nothing is cracked without one |

## Shops and money

| Tool | Arguments | What it does |
|---|---|---|
| `shop_list` ⚡ | | What the open shop sells, with prices |
| `buy` | `item`, `quantity` | Buy from the open shop (also tool upgrades that trade items) |
| `sell` | `item`, *`quantity`* | Sell to the open shop for gold at once; a shop trip, so use `ship` at home for routine produce and fish |
| `ship` | `item`, *`quantity`* | Put items in the shipping bin at the farm; same base price as a shop and no trip, paid overnight (the default way to sell) |
| `pay` ⚡ | `to`, `amount` | Send money to another player at any distance: a loan, a debt, a payment for work. Zero game time, but a serialized state write, not a pure read — the transfer lands one at a time |

## Animals and buildings

| Tool | Arguments | What it does |
|---|---|---|
| `buy_animal` 🔁 | *`type`*, *`building`*, *`name`* | (Legacy; the built-in AI uses `list_animals_for_sale` + `purchase_animal` instead.) Marnie's animals; `type=list` shows them with prices |
| `list_animals_for_sale` ⚡ | | The animals Marnie sells, with prices and which building each one needs; an instant observation |
| `purchase_animal` | `type`, *`building`*, *`name`* | (action) Buy one animal of the named type from Marnie, into the named building (asked when left out); `type` is required — nothing is bought without one |
| `animals` 🔁 | *`action`*, *`name`* | (Legacy; the built-in AI uses `list_animals`, `pet_animals`, `collect_animal_products` and `feed_animals` instead.) Your animals; `pet`, `collect` (eggs, milk, wool) and `feed` (hay troughs) |
| `list_animals` ⚡ | | Your animals: each one's type, home building, hearts and produce ready; an instant observation |
| `pet_animals` | *`name`* | (action) Pet your animals — the one named animal, or all of them when `name` is left out |
| `collect_animal_products` | *`name`* | (action) Collect eggs, milk and wool — from the one named animal, or everything ready when `name` is left out |
| `feed_animals` | | (action) Fill the hay troughs of the coop and barn |
| `build` 🔁 | `building`, *`x`*, *`y`* | (Legacy; the built-in AI uses `list_buildings`, `suggest_building_site`, `construct_building` and `upgrade_building` instead.) Order a Coop, Barn, Silo, Well or Shed from Robin, or upgrade one you have (Big Coop, Deluxe Barn, Big Shed...); `building=list` shows the costs |
| `list_buildings` ⚡ | | What Robin can build and which upgrades your buildings can take, with costs, materials and sizes; an instant observation |
| `suggest_building_site` ⚡ | `building` | Where a building of the named type could stand on the farm: the free rectangles that fit it, with their sizes; an instant observation |
| `construct_building` | `building`, `x`, `y` | (action) Order Robin to build the named building at tile `x`, `y`; `building` is required — nothing is ordered without one |
| `upgrade_building` | `building` | (action) Upgrade the named building (Big Coop, Deluxe Barn, Big Shed...); the safety guard stays: a building that already has an upgrade queued or is under construction is refused |
| `upgrade_house` | | Buy the next house level from Robin |
| `name` ⚡ | `text` | Name the new pet, farm animal, horse or child the open naming box asks for. Zero game time, but a serialized state write, not a pure read |

Buildings and animals are paid from the caller's own money and items.

## Villagers, romance and festivals

| Tool | Arguments | What it does |
|---|---|---|
| `talk_to` 🔁 | `name`, *`gift_item`*, *`go`* | (Legacy; the built-in AI uses `talk_to_villager` and `give_gift` instead.) Find a villager (travels across maps unless `go=false`), talk, optionally give a gift; reports friendship points before and after and their reaction. A villager who is walking is followed and talked to from any tile next to them, corners included. A fellow farmer's name or nickname (`Seats[].Nicknames`): walks up to them when they are on your map and tells you to use `say`; on another map it fails without saying where they are |
| `talk_to_villager` | `name`, *`go`* | (action) Find a villager (travels across maps unless `go=false`) and talk to them — never gifts. A villager who is walking is followed and talked to from any tile next to them, corners included. Reports where they were, your friendship points before and after, and their first line. Talking counts once a day |
| `give_gift` | `name`, `item`, *`go`* | (action) Find a villager (travels across maps unless `go=false`) and give them the named carried item as a gift, the game's own gift path, so the reaction and taste are learned and recorded. At most 1 gift per day and 2 per week per villager |
| `bouquet` | `name`, *`go`* | Ask a villager to date you (8 hearts) |
| `mermaid_pendant` | `name`, *`go`* | Propose to a villager (10 hearts, upgraded house) |
| `festival` | `action`, *`to`*, *`max`* | On a festival day: `start` talks to Lewis and begins the main event, `leave` leaves the festival (alone it confirms; in co-op every farmer must leave together). In co-op both wait up to a minute of running time for the other farmers, tell them, then cancel the wait with `not_ready`. `eggs` picks up eggs during the Egg Festival's egg hunt (nearest first, until the hunt ends or `max` are picked; `look` shows the eggs as `E`) |

## Quests and the Community Center

| Tool | Arguments | What it does |
|---|---|---|
| `accept_quest` | | Take today's Help Wanted request from the board |
| `list_quests` ⚡ | | The quest journal as an instant observation: each quest's story, giver, progress and reward, finished quests whose money is not collected yet, and today's Help Wanted request with who took it |
| `claim_quest_reward` | *`quest`* | (action) Open the journal and collect a finished quest's money: `quest` is its title, or leave it out for the first one |
| `special_orders` 🔁 | *`action`*, *`index`* | (Legacy; the built-in AI uses `list_special_orders`, `active_special_orders` and `accept_special_order` instead.) The Special Orders board in Town: `list`, `accept` one by index, `active` with progress |
| `list_special_orders` | | (action) Walk to the Special Orders board and read what it offers |
| `active_special_orders` ⚡ | | The Special Orders you accepted, with their progress; an instant observation |
| `accept_special_order` | `index` | (action) Accept one board entry by its index from `list_special_orders` |
| `bundles` ⚡ | *`room`*, *`missing_only`*, *`action`* | Community Center bundles room by room: what each room unlocks, what each bundle needs, what is donated and by whom, the reward, and for each missing item how many you have (pack and every chest and fridge on the farm) and where it comes from. `missing_only=true` keeps only unfinished bundles. Needs the Community Center open; with `GuideKnowledge` it previews the bundles before. `action=keep`: ship or keep, for every item in your pack (does a bundle, the Museum or a quest still want it, its best machine), ending with what is safe to ship |
| `donate_bundle` | `item`, *`bundle`* | Donate an item (or gold, for the Vault) to the first bundle that needs it, through the game's own bundle page |
| `joja` | `action`, *`project`*, *`confirm`* | The Joja route: `projects` lists the five projects, `membership` buys the 5,000g membership only with `confirm=true` (without it: a warning of what every player loses and gains), `buy_project` pays one |
| `museum` 🔁 | *`action`*, *`item`* | (Legacy; the built-in AI uses `list_museum_donations`, `donate_museum` and `claim_museum_rewards` instead.) `list` what the Museum still accepts, `donate` one item or all, collect `rewards` from Gunther |
| `list_museum_donations` ⚡ | | What the Museum still accepts and what is already donated; an instant observation |
| `donate_museum` | `item` | (action) Donate one carried item to the Museum; `item=all` donates everything suitable, and nothing is donated without an explicit item |
| `claim_museum_rewards` | | (action) Collect the Museum's rewards from Gunther |

## Adventure

| Tool | Arguments | What it does |
|---|---|---|
| `fish` | *`tries`* | Walk to the best shore, cast, play the BobberBar minigame and pick up treasure; one line per cast. Needs a rod; `fish_info` says what bites where |
| `monsters` ⚡ | *`name`*, *`radius`* | What is on this map and how to fight it. No arguments: every living monster on the map, nearest first, with current/max HP, damage, defense, distance and a one-line tactic; the held weapon's damage is shown too. `name=X`: the codex for one monster or family (base HP, damage, defense, behaviour, what works and what to avoid). Use it on a new mine floor or a crowded room. Costs no game time |
| `fight` | *`seconds`*, *`eat_below`* | Fight nearby monsters for up to `seconds` (default 10) with type-specific spacing: support casters are prioritised, chargers and ranged monsters use hit-and-run, flyers are engaged in reach, and ranged styles prefer cover after a swing. Attacks require a clear line of sight: blocked lines reposition instead of swinging through stone. `eat_below` (HP %) eats healing food. A rock crab in its shell gets the Pickaxe; what a weapon can't hurt is left alone and named. `monsters()` says what is on the floor and which tactic each kind needs |
| `mine_floor` | | Resource-first progression to the next elevator floor (next multiple of 5): collect loose items and valuable nodes, fight only threats or an infested-floor requirement, mine plain stones to reveal the down ladder, then stop. Repeat when ready |
| `leave_mine` | | Get out of the Mines from any floor: climbs the up ladders, or warps out when health is low, it is 11 PM or the ladder can't be reached |
| `search_trash` | *`x`*, *`y`* | Dig through a trash can; a villager who sees it likes you less |
| `watch_tv` | *`channel`* | Watch a channel: weather (tomorrow), fortune (today's luck), Queen of Sauce (Sun new recipe, Wed rerun; learns it), Livin' Off The Land (Mon/Thu tips), fishing (after Pam's letter) |

In the Mines the footer shows HP, and any long action except `fight`, `eat` and `leave_mine` is cut short with
`Interrupted:` when a monster within 3 tiles hurts you.

## Menus and dialogue

| Tool | Arguments | What it does |
|---|---|---|
| `interact` | *`x`*, *`y`*, *`holding`* | The action button on a tile: doors, objects, machines, NPCs, signs. `holding` holds an item first (load a machine, use a totem, a gift). A sign or dialogue that opens is quoted in the result; `look` labels a sign as `A Sign: ...`. (Kept as the generic fallback for tiles without a semantic tool — loading a machine, using a held item and gifting have their own tools now — not a legacy entry) |
| `respond` | *`option`* | Answer the open dialogue question |
| `close_menu` 🔁 | | (Legacy; the built-in AI uses `dismiss_menu`, `accept_letter_quest` and `claim_menu_items` instead.) Close the open menu; accepts a letter's quest and takes treasure or gift items that fit. On the box that waits for a proposal's answer it withdraws the proposal |
| `dismiss_menu` | | (action) Close the open menu and nothing more: no quest is accepted and no items are taken. On the box that waits for a proposal's answer it withdraws the proposal |
| `accept_letter_quest` | | (action) Accept the quest attached to the open letter |
| `claim_menu_items` | | (action) Take the items an open menu offers (a letter's attachment, fishing treasure, a gift): what fits goes into the pack, what is left over is named in the result |
| `menu` | *`action`*, *`item`*, *`count`*, *`quality`*, *`option`*, *`value`*, *`text`* | Read any open menu, then use it: take or put (chests), choose (mine elevator), amount (send money), type (names), ok or cancel, or click a listed button. (Kept as the generic fallback for menus the semantic tools below don't cover — not a legacy entry; the built-in AI still receives it) |
| `read_menu` ⚡ | | What the open menu is: its type, title, items with counts or prices, and the options or buttons that work on it. A pure instant observation — game time stays frozen |
| `choose_mine_floor` | `floor` | (action) Choose a floor in the open mine elevator menu, e.g. `floor: 25`; the list of reachable floors comes from `read_menu` |
| `choose_menu_option` | `option` | (action) Choose one option in an open list menu (a song, a recipe list entry...), named exactly as `read_menu` lists it |
| `set_menu_amount` | `value` | (action) Type a number into an open number-entry menu (e.g. how much money to send) and confirm it |
| `confirm_menu` | | (action) Press the open menu's OK / confirm button (the yes in a yes/no box) |
| `read_sign` | `x`, `y` | Walk next to the sign at tile `x`, `y` and read its text, quoted in the result; an action, not an instant observation. Signs in view are also labelled by `look` as `A Sign: ...` |
| `load_machine` | `item`, `x`, `y` | (action) Hold the named item and press the action button on the machine at tile `x`, `y` — Copper Ore into a Furnace, bait onto a crab pot. One named job: nothing else on that tile is triggered |
| `use_held_item` | `item` | (action) Hold the named item and use it where you stand (`performUseAction`): Warp Totems, books, secret notes. No tile is pressed, so nothing nearby is triggered by accident |
| `choose_profession` | `option`, *`skill`* | Make a pending profession choice (skill level 5 and 10). Permanent |

## Time, sleep and memory

| Tool | Arguments | What it does |
|---|---|---|
| `wait` | `minutes` | Let time pass |
| `rest` | *`minutes`* | Lie down on your own bed to get energy and health back while time passes (multiplayer only — alone in the game the bed gives nothing back, so it is refused); ends when energy is full, after the minutes, or when someone calls you |
| `journal` ⚡ | `diary`, `lessons`, `plan`, *`notes`*, *`self`*, *`people`*, *`day`* | The AI's memory across days: a diary for the viewers, lessons (appended), a plan with Season / Week / Today sections, optional facts, a `self` note (who it is, overwritten each evening) and one line per other farmer in `people`. `day` catches up a missed day |
| `sleep` | *`confirm_early`* | End the day. No journal is required first — writing the diary is the runner's policy, not a game rule. Wide awake early in the day (before 17:00 with plenty of energy left) it refuses once with a warning, unless `confirm_early: true` says you really mean to sleep now. In co-op it lies in bed and waits for the other farmers (they are told); night events come back as an `Overnight:` line |
| `go_to_bed` 🔁 | | (Legacy; the built-in AI uses `sleep` instead.) End the day. Refuses until today's `journal` is written; wide awake before 17:00 it says so once, then lets a second call through. Night events come back as an `Overnight:` line |

## Together (split-screen)

| Tool | Arguments | What it does |
|---|---|---|
| `say` ⚡ | `text` | Chat: the line shows in the chat box and above your head, heard only by the players whose screen shows you. `@name` gets someone's attention |
| `emote` ⚡ | `name` | Show an emote bubble above your head |
| `give` ⚡ | `to`, `item`, *`count`* | Hand an item to a nearby player. Zero game time, but a serialized state write, not a pure read |
| `pay` ⚡ | `to`, `amount` | Send money (see *Shops and money*; zero game time, serialized like `give`) |
| `propose` | `to` | Propose marriage to another player |
| `answer_proposal` | `accept` | Answer another player's proposal |

What the farmers see of each other: `look` and `status` list the other players in view with the tool each is
*actually* running, its reason and how long, next to the last thing you heard them say. An @mention, a gift, a
player coming into view or into your house, a theft from your chest or plot, rain starting or crows on the farm
come back as `Events:` in the next tool result and stop a running `wait`, `walk_to`, `travel_to` or farm action.

## For scripts

| Tool | Arguments | What it does |
|---|---|---|
| `relationships_report` ⚡ | | This farmer's friendship data as JSON, for scripts (the same as [`/api/relationships`](api.md#endpoints) for one seat) |
