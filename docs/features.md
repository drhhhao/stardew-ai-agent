# What it can do

[ภาษาไทย](features.th.md) · **English** · [← README](../README.en.md)

Everything an AI farmer can do with this mod, area by area. The tool names link the features to the
[tool reference](tools.md). The mod does the mechanics (pathing, aiming, button presses, the fishing bar); the AI
makes every choice.

> Many of the newer features are written but not yet played through in game. [coverage.md](coverage.md) shows the
> status of each one (✅ works · 🟡 reachable but untested or partial · ❌ missing).

## 🌱 Farming

- Clear, till, plant and fertilize, tile by tile or a whole rectangle at once (`clear_area`, `till_area`, `plant_item`;
  planting every empty tilled tile on the map needs `all_empty: true` plus an explicit `confirm: true`. The old `plant` entry remains as
  legacy for existing MCP clients)
- Water every dry crop on the map, refilling the can at the nearest water (`water_all`), or only your own crops
- Harvest everything that is ripe, forage included (`harvest_all`)
- A whole-farm report: what is dry, ready, dead or won't make it this season, and how many machines are ready (`farm_summary`)
- Plan the season from the game's own data: growth days, last day to plant, gold per tile per day, the Greenhouse and speed fertilizers too (`crops`)
- What to put in which machine and what it earns, and every machine on the farm: ready, working (with the time left) or empty (`machines`)
- Place machines, chests, sprinklers, fences and furniture (`place`), load machines (`load_machine`)
- Blocked walks move to the nearest open tile and name the obstacle and clearing tool; known shop trips check counter hours, closed days and festivals before travelling

## 🎣 Fishing

- Walk to the best shore for the rod, cast, play the BobberBar minigame and pick up treasure (`fish`)
- Which fish bite where, when and in what weather, which ones a bundle still needs, what crab pots catch (`fish_info`)
- Play it perfectly or like a human who can lose the fish (`FishingAssist`)
- Attach bait and tackle (`equip`)

## ⛏️ Mines and combat

- Fight nearby monsters with type-specific spacing, support-target priority, hit-and-run for chargers/ranged enemies, and clear-line-of-sight checks that reposition instead of swinging through stone; rock crab shells get the Pickaxe (`fight`)
- See every monster on the floor with HP, damage and a one-line tactic before a fight (`monsters`)
- Progress to the next elevator floor with resources first: gather loose items and valuable nodes, fight only threats, reveal the down ladder and stop (`mine_floor`)
- Get out of the Mines from any floor, or warp out when it gets dangerous (`leave_mine`)
- Take the elevator (`choose_mine_floor`; the generic `menu` remains as the fallback for menus without a semantic tool), and see the geodes you carry cracked at Clint's (`list_geodes`, `crack_geodes`; the combined `crack_geode` remains as legacy for existing MCP clients)
- Long actions stop by themselves when a monster hurts you
- The farmer defends itself while its AI thinks: a monster that comes within reach is hit, it eats when hurt, and it backs away when it has no food left, without pausing anything (`SelfDefense`)

## 🍳 Crafting, cooking and items

- Craft from known recipes, cook in the kitchen with the fridge's ingredients (`list_recipes`, `craft_item`, `list_dishes`, `cook_dish`; the combined `craft` and `cook` entries remain as legacy for existing MCP clients)
- Store and take exactly the items you name in the chest or fridge you have open (`open_container`, `store_items`, `take_items`); read the open container or the whole farm's chests and fridges as instant observations (`list_chest`, `list_containers`); the combined `chest` entry remains as legacy for existing MCP clients (`stash` and `create` stay on it)
- Read any open menu at a glance (`read_menu`), then drive it with named actions — pick a mine floor, choose an option, set an amount, confirm (`choose_mine_floor`, `choose_menu_option`, `set_menu_amount`, `confirm_menu`); the generic `menu` stays as the fallback for menus without a semantic tool
- Load a machine with what you hold (`load_machine`), use a held item where you stand (`use_held_item`), read a sign's text (`read_sign`), all without a wrong right-click; the generic `interact` stays for everything else
- Everything wearable from one entry: rings, boots, hats, shirts and pants, and bait and tackle on the fishing rod (`equip`, `wardrobe`; the old `wear` entry remains as legacy for existing MCP clients)
- Look up any item, weapons and tools included: sell prices, what it restores, shops, where to find it, what to keep it for, machine outputs, recipes (`item_info`)
- Eat, throw things away (valuables need confirmation), watch the TV for tomorrow's weather, today's luck, Queen of Sauce recipes, Livin' Off The Land tips and the Fishing channel (`eat`, `trash`, `watch_tv`)

## 🛒 Shops and money

- Walk to a shop by its name or its owner's and the counter is ready for `shop_list`/`buy` (`travel_to_shop`); any other map by its place name (`travel_to_location`); the combined `travel_to` entry remains as legacy for existing MCP clients
- Read a shop, buy, sell for gold at once, ship for overnight pay (`shop_list`, `buy`, `sell`, `ship`)
- Tool upgrades at the blacksmith, backpack upgrades at Pierre's
- Separate wallets per farmer, and paying another farmer at any distance (`pay`)

## 🐔 Animals and buildings

- Buy animals from Marnie, pet them, collect eggs, milk and wool, fill the hay troughs (`list_animals_for_sale`, `purchase_animal`, `list_animals`, `pet_animals`, `collect_animal_products`, `feed_animals`; the combined `buy_animal` and `animals` entries remain as legacy for existing MCP clients)
- Order a Coop, Barn, Silo, Well or Shed from Robin, see where one would fit, upgrade them (Big Coop, Deluxe Barn, Big Shed...) and upgrade the house (`list_buildings`, `suggest_building_site`, `construct_building`, `upgrade_building`, `upgrade_house`; the combined `build` entry remains as legacy for existing MCP clients)
- Name pets, animals, the horse and children (`name`), ride the horse (`mount`)

## 💬 Villagers, romance and festivals

- Know everyone: where they are, hearts, birthdays, the gift tastes found out so far, and who likes a given item (`villagers`)
- Find a villager anywhere in the valley and talk (`talk_to_villager`), or give a chosen gift by name (`give_gift`), even to someone walking down the street; the combined `talk_to` entry remains as legacy for existing MCP clients. P2 and P3 find
  villagers on maps nobody stands on too
- Date with a bouquet, propose with the Mermaid Pendant, marry (`bouquet`, `mermaid_pendant`)
- Heart events with their questions answered by the AI (opt-in: `PlayHeartEvents`)
- Festival calendar, shop hours, joining and leaving festivals (`calendar`, `festival`)
- The Egg Festival's egg hunt: the farmers see the eggs and race each other for them (`festival(eggs)`)

## 📜 Quests and the Community Center

- Quest journal, Help Wanted board, Special Orders; a finished quest's money is collected from the journal
  (`list_quests`, `claim_quest_reward`, `list_special_orders`, `active_special_orders`, `accept_special_order`,
  `accept_quest`; the combined `quests` and `special_orders` entries remain as legacy for existing MCP clients)
- Community Center bundles and donations, gold for the Vault, how many of each missing item you have and where to get the rest, and a ship-or-keep check of the pack (`bundles`, `donate_bundle`)
- The Joja route, with a warning before the membership that changes everyone's game (`joja`)
- Museum donations and Gunther's rewards (`list_museum_donations`, `donate_museum`, `claim_museum_rewards`; the
  combined `museum` entry remains as legacy for existing MCP clients)

## 🛏️ Daily life

- Wait, rest in bed to recover (multiplayer only — alone the bed gives nothing back), end the day whenever you choose: `sleep` requires no journal first, only an explicit confirmation when you turn in wide awake (`wait`, `rest`, `sleep`; the old `go_to_bed` entry remains as legacy for existing MCP clients)
- Mail and letters: read one with `interact`, then decide explicitly — close it with `dismiss_menu` (which never
  accepts the quest or takes items), accept the letter's quest with `accept_letter_quest`, or take the attached items
  with `claim_menu_items`; the combined `close_menu` entry remains as legacy for existing MCP clients
- Profession choices at skill levels 5 and 10 (`choose_profession`)
- Progress at a glance: skills and XP, professions ahead, the next tool upgrade, the deepest mine floor, slayer goals, museum rewards (`progress`)
- Forage, spots to dig, berry bushes and dropped items passed on the way are listed after every walk, so the AI can go
  back for them (`walk_to`, `travel_to_location`, `travel_to_shop`; the combined `travel_to` remains as legacy)
- Night events (fairy, witch, meteorite...) reported the next morning
- Passing out at 2:00 AM or at 0 health is recorded, and the missed diary is caught up the next day

## 👥 Several AIs on one farm

- Up to three AIs (plus a camera seat) in split-screen co-op, joined without gamepads, cabins built when missing
- You can play P1 yourself next to them
- Chat that only reaches farmers whose screen shows the speaker; emotes; handing over items (`say`, `emote`, `give`)
- Each farmer sees what the others are *actually* doing next to what they last said, so lies can be caught
- Interrupts: an @mention, a gift, someone arriving or entering your house, a theft, rain or crows stop a long action
- Ownership: who planted a crop, who placed a chest, and a `theft` record when someone takes what isn't theirs; every chest change is written down, and the owner hears what others put in or took out (in the chest's result and the morning briefing)
- Marriage between players (`propose`, `answer_proposal`), a bedtime warning when the others wait for you; a message box left open while someone waits in bed is closed after a while
- Co-op festivals: entering, starting and leaving wait a minute for the other farmers, tell them, then give up instead of getting stuck

## 🧠 Memory and character

- A diary for the viewers, lessons, a plan by season, week and day, and facts (`journal`)
- A `self` note each AI rewrites every evening, and one line on each of the other farmers
- A morning briefing: overnight results, shipping, mail, weather, the farm (`/api/briefing`)
- A strategy guide to read before big choices, plus all gift tastes, bundle previews and legendary fish from the game data (opt-in: `GuideKnowledge`, `guide`)

## 🎥 For viewers and video

- An in-game panel per AI screen: name, model, current action or thinking time, last thought and chat line
- `/overlay` for OBS (live thoughts and actions, one seat or all) and `/replay` (day by day)
- JSONL logs, diaries, daily stats and failure summaries for every day
- Frozen frames that look live, speech bubbles timed to the running world, Thai and English UI
- Every farmer walks with a smooth running stride on its own screen too, not in stop-and-go taps
- A walk blocked by a villager finds a way around them, or waits for one standing in the only doorway; a locked door ends it with the door's message; a farmer a villager
  pushed into a wall steps back out

## ⏱️ Time and control

- The world freezes while any AI thinks, or keeps running (`FreezeTimeWhileThinking`)
- A thinking timeout so a stuck AI never holds the world forever
- Pause with `F8`, stop after today or right now (`/api/control`)
- A loop guard that hints and finally blocks an AI repeating the same failing call — an explicit `no_change` is not
  progress either, so repeating a call that changes nothing is caught too
- Machine-readable tool results: `structuredContent` says `changed` / `no_change`, `partial` with its stop cause and
  `validValues` for a wrong argument; paying, giving and naming cost no game time but are serialized state writes, not
  pure reads; pure observation results skip the repeated status footer. (Phase 5 — new, not yet verified in game)
- A daily token budget per seat, session breaks for long runs

## 🔌 Ways to drive it

- The built-in AI: Anthropic, OpenAI, Gemini, or any OpenAI-compatible server (OpenRouter, Ollama, LM Studio)
- Any MCP client over streamable HTTP: Claude Code, Claude Desktop, Cursor...
- The [HTTP API](api.md) for your own bots, overlays and scripts

## 🚧 Not yet

- Tailoring, the dye pots and the forge
- Crab pots
- The arcade, darts, the crane game and the casino; the Fair and ice-fishing games at festivals
- House renovations
- Dancing and having a baby with another player

Want one of these? See [coverage.md → Keeping it complete](coverage.md#keeping-it-complete) and
[CONTRIBUTING.md](../CONTRIBUTING.md).
