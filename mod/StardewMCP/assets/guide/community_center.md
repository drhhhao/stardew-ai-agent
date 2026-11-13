<!-- summary: How to unlock and finish the Community Center in Year 1: room order and rewards, what to keep, the hard items, cheapest optional slots, and why Joja loses. -->
# Community Center

## When this matters
- From Day 1: items count whenever you got them, so stockpile before the CC opens.
- Before every ship batch, and before spending over 1,000g in Summer or Fall (Vault money).
- Whenever Morris offers the Joja membership: refuse.

## Priorities
1. **Unlock it in week 1** (first dry day of Days 5-11; steps in `guide('spring')`). Only after the Wizard's visit can you donate.
2. **Unlock order.** Crafts Room (6 bundles) is open at once. Pantry (6) and Fish Tank (6) open after 1 completed bundle, Boiler Room (3) after 2, Bulletin Board (5) after 3, Vault (4) after 4. Lists here are the standard bundles; on a Remixed save trust `bundles()` for contents.
3. **First four bundles** (they open every room by the end of Spring): Spring Foraging (forage on the way, week 1-2), Construction (198 Wood, 99 Stone, 10 Hardwood; Hardwood needs the Copper Axe for stumps), Spring Crops (Parsnip, Green Bean, Cauliflower, Potato, timing in `guide('spring')`), then Crab Pot (Fishing 3) or Adventurer's (99 Slime + 10 Bat Wing).
4. **Room priority, with the unlock you get:**
   1. **Pantry → Greenhouse**, plus Evelyn's Garden Pot recipe. Biggest money impact; rewards include Quality Sprinkler, Bee House, Keg. Blockers: Animal (a Duck and a Goat, so upgrade to a Big Coop and a Big Barn with `upgrade_building` in early Fall Y1: 22,000g + 850 Wood + 350 Stone, see `guide('animals')`), Artisan, and the gold crops. Target end of Fall Y1, early Spring Y2 at worst.
   2. **Vault → Bus to the Desert.** 2,500 / 5,000 / 10,000 / 25,000g = 42,500g, paid per bundle; the 5,000g bundle returns 30 Quality Fertilizer. The bus opens Sandfish, Coconut, Cactus Fruit, and Skull Cavern once you hold the Skull Key (Mines floor 120). Pay in Summer/Fall Y1, never with next season's seed money.
   3. **Boiler Room → Minecarts.** Bars, minerals and monster loot: finishes by itself while you mine (`guide('mines')`).
   4. **Crafts Room → Quarry bridge** (ore nodes, Quarry Mine). Winter Foraging means it closes in Winter Y1.
   5. **Fish Tank → Glittering Boulder removed**, which opens panning. Specialty Fish needs Sandfish, so it finishes after the Vault.
   6. **Bulletin Board → +2 hearts with every villager you have met and cannot date.** The bonus lands when you read the thank-you letter the next day; meet everyone before opening it. The Dwarf only counts once you can understand him. Usually last (Truffle, Duck Feather, Rabbit's Foot, Red Cabbage): the Duck Feather comes with the Big Coop, but the Truffle needs a Deluxe Barn and a Pig (41,000g after the Big Barn), usually Year 2.
5. **Completing all 6 rooms:** 3 Grandpa evaluation points, Pierre opens Wednesdays, JojaMart closes, and the Missing Bundle (→ Movie Theater) appears: a Year 2-3 goal, see `guide('year2')`.
6. **Timing:** new room notes appear at once; a finished room's unlock (Greenhouse, bridge, bus...) appears the next morning. Unclaimed rewards sit on the room's note; for finished rooms they go to a chest in the CC main room.

## Keep / Don't sell
- **Bundle chest:** put the first copy of every needed item in one chest before shipping the rest; run `bundles(action=keep)` before each ship batch.
- **Donate the worst copy.** Only Quality Crops (5 gold each of 3 of Parsnip, Melon, Pumpkin, Corn) and the Missing Bundle check quality; elsewhere normal quality works, so sell the gold copies.
- **Gold crops:** Corn is the easiest source (Summer to Fall, regrows every 4 days, many rolls). Put Quality Fertilizer on Corn, Melon or Pumpkin. In Y1 it comes only from the 5,000g Vault bundle or the Farming 9 recipe (Pierre sells it from Year 2): pay that bundle before planting Summer Corn/Melon only if the Blueberry money is already set aside; otherwise craft Basic Fertilizer.
- **Keep two or more copies of these:** Maple Syrup (Exotic + Chef's), Oak Resin (Exotic + Enchanter's), Red Mushroom (Exotic + Dye), Purple Mushroom (Exotic + Field Research), Wine (Enchanter's + silver for Missing), and 1 normal + 5 gold of Melon, Corn, Pumpkin or Parsnip.
- **Fruit trees fill four slots in three bundles:** once seed money is safe, plant one Apple and one Pomegranate Sapling (Pierre); planted by mid-Summer Y1 they fruit in Fall: 3 Apples for Fodder, Pomegranate for Enchanter's, one of each for Artisan.
- **Five rain fish** (rain only; snow does not count): Catfish, Shad, Eel, Red Snapper, Walleye (Fall only in practice). Fish every rainy day until you own all five; no Fall Y1 rain means Walleye waits a year.
- **Other one-season items:** Sturgeon, Tiger Trout, Pufferfish (sunny Summer), Nautilus Shell (Winter beach), Fiddlehead Fern (Summer), Winter Root and Snow Yam (hoe soil in Winter), the Fall forage, and every seasonal crop.
- **Red Cabbage:** in Year 1 only the Traveling Cart has it (guaranteed only with the "Year 1 completable" option); Pierre sells seeds from Summer Y2. In Summer Y1 buy the seeds (9 days to grow, plant by Summer 19); in other seasons buy the cabbage itself.
- **Hard items and what they need:**
  - Truffle: a Pig (16,000g) in a Deluxe Barn (25,000g upgrade of a Big Barn).
  - Rabbit's Foot: a Rabbit (8,000g) in a Deluxe Coop (20,000g upgrade of a Big Coop), the Cart, Jas's Desert Festival shop (Spring 15-17 once the bus runs, 100 Calico Eggs), or rare drops from Serpents in Skull Cavern. Skip the Deluxe Coop if one of the others comes first.
  - Duck Feather: a Duck (1,200g) in a Big Coop (10,000g upgrade of a Coop) with high friendship.
  - Large Goat Milk: a Goat (4,000g) in a Big Barn (12,000g upgrade of a Barn).
  - Upgrades: `upgrade_building(building="Big Coop")` at Robin's; `list_buildings()` shows every upgrade's wood and stone too.
  - Woodskip, Morel (Spring), Fiddlehead Fern (Summer): Secret Woods, behind the log that needs the Steel Axe. Chef's needs Fiddlehead: get the Steel Axe before Summer ends, or Chef's waits until Year 2.
  - Maki Roll: a kitchen plus the recipe (Gus or TV); fish, Seaweed, Rice.
  - Large eggs and milk: pet animals daily from the day they arrive (timing: `guide('animals')`). Hen color is random; buy until you own a white and a brown.
- **Cheapest picks for optional slots:**
  - Exotic Foraging (5 of 9): Maple Syrup, Oak Resin, Pine Tar (one tapper per tree type, early), Cave Carrot, Red Mushroom or Morel.
  - Animal (5 of 6): Large Milk, Large White Egg, Large Brown Egg, Large Goat Milk, Duck Egg. Skip Wool, so Big Barn + Big Coop is enough.
  - Artisan (6 of 12): Honey, Cheese, Goat Cheese, Jelly, Apple, Pomegranate (or any tree fruit from the Cart). Skip Truffle Oil and Cloth.
  - Crab Pot (5 of 10): Clam, Cockle, Mussel, Oyster from beach forage, plus one catch from a single crafted crab pot (Fishing 3: 40 Wood + 3 Iron Bars).
  - Adventurer's (2 of 4): 99 Slime and 10 Bat Wing.
  - Quality Crops (3 of 4): the three you can grow gold soonest.

## Avoid
- **Never buy the Joja membership** (5,000g from Morris). It is permanent: the CC becomes a warehouse and bundles end.
- Joja projects give no bundle rewards, Bulletin hearts, Grandpa points or Wednesday Pierre; the Vault (42,500g, four rewards) beats the Joja Bus (45,000g with membership, nothing back).
- Consider Joja only in Year 2+ when the Greenhouse or Bus is blocked by items out of reach for a season and you have 40,000g+ spare. Completed rooms count as done on the Joja form; half-done rooms give no discount.
- Do not plant a bundle crop that cannot ripen before day 28 (Cauliflower after Spring 16, Melon after Summer 16, Pumpkin after Fall 15).
- Do not wait on Sandfish before the bus runs; the Cart rarely has it.

## Look it up
- `bundles()` lists every slot, what you hold (pack + chests) and where to get the rest; `bundles(room=Pantry, missing_only=true)` narrows it; `bundles(action=keep)` tags pack items keep or safe to ship.
- `item_info(name)` gives `Found:` sources and `Keep for:` bundle needs for any item.
- `fish_info(weather=rain)` gives where and when the rain fish bite.
- `crops(season)` gives last planting days and seed shops.
- `progress()` shows Fishing level and axe upgrade status.
- `joja` shows the Joja project prices and which projects count as done.
