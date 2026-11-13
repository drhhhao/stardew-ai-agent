<!-- summary: The Year 1 roadmap: daily loop, key dates per season, end-of-Year-1 goals, spending rules and which tool answers which question. -->
# Overview: how to play Year 1

## When this matters
- Read on Spring 1 and at each season start; read the matching topic before any big choice (a purchase over 1,000g, a profession, a festival, a first trip somewhere new).
- Year 1 builds the money engine (sprinklers, Jars, Kegs, greenhouse) and the Community Center that unlocks it.

## Priorities
1. **Daily loop.**
   1. `status` (tomorrow's weather, today's luck, a pending profession).
   2. Water, harvest, refill machines from `machines()`.
   3. Talk to every villager you pass.
   4. Spend remaining energy on the bottleneck: clear only what you plant this week, mine on spare days, fish rain days for missing bundle fish.
   5. Run `bundles(action=keep)` before shipping or selling.
   6. Be in bed before 24:00.
2. **Year 1 at a glance.** Spring 1 is a Monday; Pierre closes Wednesdays until the Community Center is done.
   - **Spring:** Day 1 buy 1 Cauliflower and 1 Bean Starter, max 15 crops until the Scarecrow; Day 5 Mines open; first dry day of Days 5-11 open the Community Center; Day 13 Egg Festival Strawberries, Spring Crops Bundle that night, plant on its Speed-Gro; floor 40.
   - **Summer:** Days 1-2 Blueberries on every waterable tile; Day 11 Luau (gold food, 160g+ base); Jars at Farming 4, Quality Sprinklers at Farming 6; Days 20-21 Trout Derby; floor 80; no optional spending from Day 20.
   - **Fall:** Day 1 Cranberries and Pumpkins (replant Pumpkins Day 14); Day 15 last shopping; Day 16 Fair (Stardrop); Day 27 Spirit's Eve (Golden Pumpkin); Silo and Hay, Tappers on Oaks; floor 120.
   - **Winter:** Mines to 120 (Skull Key) if not done; a tool at Clint every 2 days, Watering Can first; winter forage; Days 15-17 Night Market seeds; Day 25 Winter Star (recipient named Day 18); Days 26-28 sprinklers and fields set for Spring 1.
3. **End-of-Year-1 goals, most important first.**
   1. 20+ Quality Sprinklers covering every field.
   2. Jars for every crop, Kegs from Farming 8 (Oak Resin is the limit), Artisan at Farming 10 before scaling Kegs.
   3. Community Center: Pantry (greenhouse) as far as it goes, Vault paid (bus), other rooms as items come.
   4. Mines floor 120 and the Skull Key.
   5. First house upgrade (kitchen).
   6. Silo, then a Coop of chickens and a Barn of cows, each with its machine.
   7. At least 25,000g cash entering Year 2.
   - Year 2: greenhouse Ancient Fruit, Skull Cavern iridium, Iridium Sprinklers, more Kegs, then Ginger Island.
4. **Spending rules.**
   1. Spend only on the current bottleneck: seeds while waterable tiles sit empty (size plantings to the tiles you can water daily, not to cash), sprinklers once watering limits you, Jars and Kegs once crops outrun machines.
   2. Never pay for what you can craft: Crab Pots, Bombs, Deluxe Bait, Basic Fertilizer.
   3. Leave the 10,000g backpack, furniture, decor and early animals for later; the 2,000g Large Pack is the first big buy.
   4. Keep a reserve for each season's Day-1 seeds: about 80g per waterable tile for Summer 1, 240g per Cranberry tile for Fall 1.

## Keep / Don't sell
- The first copy of anything a bundle, the Museum or a quest needs.
- Wood, Stone, Hardwood, Coal, Oak Resin and bars.

## Avoid
- The Joja membership: it ends the bundles for good.
- Planting more than you can water, or anything that cannot ripen by Day 28.
- Passing out or a Mines knockout: both cost gold.
- Animals bought with seed money or before the Silo.

## Look it up
- `crops(season, fertilizer)`: what to plant, seed prices, last planting day, profit per tile.
- `item_info(name)`: price, sources, keep reasons, machine outputs, recipe.
- `fish_info()`: what bites here now; add `fish`, `season`, `weather` or `location` for more.
- `machines(item=X)`: best machine for an item; `machines()`: which machines are ready or empty.
- `bundles(missing_only=true)`, `bundles(action=keep)`: what bundles need, what is safe to ship.
- `progress()`: skills and unlocks, tool upgrade costs, mine depth, museum count.
- `villagers(name)`: gift tastes and hearts; `calendar`: festivals and birthdays.
