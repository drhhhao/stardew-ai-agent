<!-- summary: The Mines and combat: when to go, depth targets, floor bands, ladders, weapons and rings, healing, knockouts, bombs, geodes and what to keep. -->
# Mines and combat

## When this matters
- The Mines open on Spring 5 of Year 1. Go on rainy days (no watering), on spare afternoons after farm work, and on days the TV fortune is good.
- Floor 120 gives the Skull Key, which leads to Skull Cavern and iridium. Skull Cavern plan: see `guide('year2')`.

## Priorities
1. **Leave only from a floor that is a multiple of 5.** The elevator saves only those floors; leaving on 33 means replaying 31-33. If the next elevator floor is 1-2 floors away and you still have energy, HP and time, push on. Otherwise `leave_mine` from the last elevator floor.
- **Use `mine_floor` for routine progress.** It gathers loose resources and valuable nodes first, fights only threats or an infested-floor requirement, then mines plain stones to reveal the down ladder and stops at the next elevator floor. Repeat it when ready rather than clearing every monster for its own sake.
2. **Depth targets for Year 1:**
   - Spring: floor 40. The Steel Smallsword chest is on 20; iron starts on 40.
   - Summer: floor 80 (gold).
   - Fall: floor 120 (Skull Key).
   - If you fall behind, catch up in Winter (see `guide('winter')`).
   - Reach floor 5 early. The next day's letter starts "Initiation": kill 10 slimes to open the Guild shop. Slimes killed before the quest arrives do not count.
3. **Mine in the right band.** These bands are hard-coded in the game:

   | Floors | Ore | Geode type | Notes |
   |---|---|---|---|
   | 1-39 | Copper | Geode | Easy monsters. |
   | 40-79 | Iron, some copper, gold from 60 | Frozen Geode | Dust Sprites on 40-69 drop Coal half the time. Skeletons on 70-79. |
   | 80-120 | Gold, some iron and copper | Magma Geode | Strongest monsters. |

   - Floors that are multiples of 5 have no ore veins.
   - Iridium ore barely appears in the Mines. Skull Cavern is the iridium source.
   - Go back for ore when a bar is your bottleneck. Copper and iron for basic Sprinklers and early tools, iron and gold for Quality Sprinklers, gold for later upgrades and Iridium Sprinklers.
   - The Furnace burns 1 Coal per bar, so grab Coal wherever you see it.
4. **Open the fixed chests.** Each one opens once:
   - Floor 20: Steel Smallsword, your first real weapon.
   - Floor 90: Obsidian Edge, good enough to reach 120.
   - Floor 100: Stardrop (permanent max energy).
   - Floor 120: Skull Key.
   - Floors 10, 50, 80 and 110 hold boots, 40 and 70 slingshots, 60 the Crystal Dagger. Floor 30 has no chest.
   - Remixed mine rewards change these chests.
5. **Get free Coal.** Minecarts in the Mines give 6 Coal once each. Dust Sprites on the iron floors are the best early Coal farm.
6. **Find ladders cheaply.**
   - Many normal floors already have an open ladder. Check the visible floor first, then break rocks.
   - Every kill has a 15% chance to drop a ladder where the monster dies, and kills cost no energy. Early on, fight before you swing at rocks: energy runs out long before HP.
   - Each rock's ladder chance rises as fewer rocks remain, and the last rock always holds the ladder.
   - Rock odds are better on high-luck days and once every monster on the floor is dead.
7. **Skip infested floors.** On slime and monster floors the ladder appears only when every monster is dead, including Duggies hiding in the dirt.
   - With a strong weapon, clear the floor (it feeds the slayer goals).
   - With a weak one, place a Staircase. Carry 1-2 from Mining 2 on (99 Stone each).
8. **Weapon path (cheap and enough):**
   - Rusty Sword (Marlon's gift on your first visit).
   - Steel Smallsword (floor 20 chest).
   - Optional: Cutlass (1,500g at floor 25) or Templar's Blade (4,000g at floor 55), only if cash is idle.
   - Obsidian Edge (floor 90 chest).
   - Galaxy Sword: hold one Prismatic Shard at the Three Pillars in the Calico Desert. It is free and far stronger, and it opens the Galaxy Dagger and Hammer at the Guild. It is the first thing to do once the bus runs.
   - Lava Katana (25,000g after floor 120) only if no Prismatic Shard is in sight.
   - Compare found weapons with `item_info`.
9. **Rings:**
   - Buy the Ruby Ring (+10% attack, 5,000g once you reach floor 80) when cash allows.
   - Kill bats whenever it is safe: 200 bats earns the Vampire Ring from Gil (heals on kills), the best Skull Cavern sustain.
   - Collect other slayer rewards from Gil with free inventory space.
   - Later, the Iridium Band (Combat 9) combines Glow, Magnet and Ruby in one ring slot.
10. **Heal and bring food:**
    - Eat when HP drops below 40% of max.
    - On floors 80+, never fight below 40% HP.
    - Carry 5-10 healing items.
    - Keep one Life Elixir (craftable from Combat 2) for emergencies.
    - Speed and luck buffs speed up deep pushes. A second food replaces the first food buff and a second drink the first drink, but one drink (Coffee) and one food (Spicy Eel or Lucky Lunch) stack.
    - `fight` handles spacing inside the tool: support casters are targeted first, chargers and ranged monsters use hit-and-run, flyers are engaged when they enter reach, and blocked lines of sight cause repositioning instead of empty swings. Do not stand still and trade blows with Serpents or ranged monsters.
11. **Respect the knockout penalty.** At 0 HP in the Mines you wake at the entrance with 2 energy, so the day is lost. Your money loss is random, 2.5-12.5% of your gold, at most 15,000g.
    - Each inventory item has about a 22% chance to be lost, and at most 3 items go.
    - Slingshots and most weapons can be lost, the Galaxy Dagger and Hammer included. Tools (fishing rods and Pan too), scythes, the Galaxy Sword, Infinity weapons and rings cannot.
    - Leave gems, bundle items and spare weapons in a chest before a deep push.
    - Marlon sells back one item from the last knockout.
    - In Skull Cavern, a knockout sends you to the clinic, costs up to 1,000g and can still take items.
12. **Craft bombs, never buy them.** The Dwarf charges 450-1,600g each.
    - Cherry Bomb (Mining 1): 4 Copper Ore + 1 Coal.
    - Bomb (Mining 6): 4 Iron Ore + 1 Coal.
    - Mega Bomb (Mining 8): 4 Gold Ore + 1 Solar Essence + 1 Void Essence.
    - In the Mines, bomb dense rock clusters only when hunting a ladder. Bombs matter most in Skull Cavern.
13. **Crack geodes only for the museum and bundles.** Clint charges 25g each; cracking for cash loses money on average.
    - Crack them while the museum collection is incomplete (count in `progress()`) or a bundle needs a mineral. Afterwards run `list_museum_donations()` to see which results to donate.
    - After that, sell spare Geodes, Frozen Geodes and Magma Geodes uncracked. Always crack Omni Geodes: they sell for 0 and can hold a Prismatic Shard.
14. **Other mine content lives elsewhere:**
    - Mine fishing (Ice Pip, Lava Eel): see `guide('fishing')`.
    - Pickaxe upgrades and the Miner/Fighter professions: see `guide('progression')`.

## Keep / Don't sell
- The first Prismatic Shard: it becomes the Galaxy Sword.
- One of each new mineral and artifact until the museum has it.
- Mine items that bundles ask for:
  - Quartz, Earth Crystal, Frozen Tear, Fire Quartz.
  - 99 Slime and 10 Bat Wings.
  - Solar and Void Essence.
  - One Copper, Iron and Gold Bar.
  - Red and Purple Mushrooms and Cave Carrots.
  - A Frozen Geode and Aquamarine.
  - Remixed bundles differ: check `bundles(action=keep)`.
- Ore and Coal: smelt them into bars.
- Extra Solar and Void Essence: Mega Bombs and the Iridium Band.
- 1-2 Staircases and a stack of Cherry Bombs in a mine chest.

## Avoid
- Deep pushes on bad-luck days.
- Buying mid-tier Guild weapons right before the floor 90 Obsidian Edge, or the Lava Katana when a Prismatic Shard is close.
- Staying in the Mines so late that you pass out on the way home (cost: see `guide('spring')`).

## Look it up
- `progress()`: deepest and elevator floor, Skull Cavern depth, slayer counts, Mining and Combat levels, recipe unlocks, museum count.
- `item_info(name)`: sell price, Found sources and keep reasons for any ore, gem, geode or mineral; weapon damage, speed and crit; ring effects; bomb and Staircase recipes.
- `bundles(missing_only=true)` and `bundles(action=keep)`: which mine items to hold.
- `list_museum_donations()`: which items you carry can still be donated.
- `machines(machine='Furnace')`: smelting inputs and times.
- `fish_info(location='mines')`: fish in the mine floors' water.
- `status`: today's luck before a deep push.
