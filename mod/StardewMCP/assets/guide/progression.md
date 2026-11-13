<!-- summary: Skills and profession picks, Farming craft unlocks, tool upgrade order and timing at Clint, building order and Robin's rules, house upgrades and the backpack. -->
# Progression: skills, professions, tools, buildings, house

## When this matters
- The morning after you reach level 5 or 10, `status` shows a pending permanent profession choice. Call `choose_profession` before `sleep`; otherwise option 0 is auto-picked (Rancher at Farming 5, Forester at Foraging 5).
- Before any trip to Clint (tools) or Robin (buildings, house), and when you plan cash for Winter and Year 2.

## Priorities
1. **Choose professions with `choose_profession`, using these defaults:**
   - **Farming:** Tiller at 5, Artisan at 10 (see `guide('money')` for why). Rancher only for an animal-only farm: it locks you out of Artisan.
   - **Fishing:** Fisher at 5 (fish +25%), Angler at 10 (fish +50%, which replaces Fisher's +25%). Take Trapper, then Luremaster (crab pots need no bait), only if you run many crab pots.
   - **Foraging:** Gatherer at 5 (chance of double forage), Botanist at 10 (forage and pig truffles always iridium, so double price). Take Forester (+25% wood), then Lumberjack (any tree can drop Hardwood), instead if Wood and Hardwood keep blocking your Kegs, Stable or house.
   - **Mining:** Miner at 5 (+1 ore per vein; every tool tier, sprinkler and Keg needs bars). At 10, Prospector (double coal chance) when coal limits smelting, which is the usual case. Take Blacksmith (bars +50%) only if you sell bars. Take Geologist, then Gemologist, only for a Crystalarium setup.
   - **Combat:** Fighter at 5 (+10% damage, +15 max HP). At 10, Brute (+15% damage); take Defender (+25 max HP) instead if you keep getting knocked out. Skip the Scout line: crit builds need gear you will not have.
   - The shipment from the night you reach a level gets no new profession bonus. Once you hit Farming or Fishing 5/10, hold your best crops or fish; choose next morning, then ship or sell them that day.
2. **Avoid respecs.** The Statue of Uncertainty in the Sewers resets one skill's professions for 10,000g. The Sewers need the Rusty Key, which comes at 60 museum donations. Use it only to fix a real mistake.
3. **Craft each Farming unlock the day you get it:**
   - 1: Scarecrow, Basic Fertilizer.
   - 2: Sprinkler (make few), Mayonnaise Machine.
   - 3: Bee House, Speed-Gro.
   - 4: Preserves Jar. Mass-produce it: it is your first real processing engine.
   - 6: Quality Sprinkler, Cheese Press. This is the Year-1 farming milestone.
   - 7: Loom (Wool only; low priority).
   - 8: Keg, Oil Maker, Deluxe Speed-Gro.
   - 9: Iridium Sprinkler, Seed Maker, Quality Fertilizer.
   - Targets: Farming 6 by mid-Summer, 8 during Fall, 10 before you scale Kegs. Expensive crops give more harvest XP.
   - Other unlocks worth chasing: Mining 2 Staircase; Foraging 4 Tapper and Mushroom Log; Foraging 6 Lightning Rod (storms charge it into Battery Packs, which Iridium Sprinklers need); Fishing 3 Crab Pot.
4. **Tool upgrade rules at Clint.**
   - Each tier costs 2,000 / 5,000 / 10,000 / 25,000g, plus 5 Copper / Iron / Gold / Iridium Bars.
   - One tool at a time: drop it off on day D, collect it on D+2.
   - Clint does not hand tools back on festival days: pick D so D+2 is not a festival. Keep one inventory slot free when you collect, or he refuses to hand it over.
5. **Upgrade tools in this order:**
   1. **Copper Pickaxe first,** as soon as you have 5 Copper Bars and 2,000g. Fewer hits per rock: faster descent, less energy.
   2. **Copper Axe** for Large Stumps (Hardwood).
   3. **Steel Axe before you need Hardwood in bulk.** It clears the log into the Secret Woods, and the stumps there come back every day.
   4. **Steel Pickaxe** for the boulders on the farm and for the deep Mines (see `guide('mines')`). **Gold Pickaxe** for meteorites and before the Skull Cavern (see `guide('year2')`).
   5. **Watering Can last.** `water_all` waters one tile per swing, so a bigger can only saves refill trips (40 water, then 55/70/85/100). It never saves energy. Upgrade it only while it would sit idle anyway: the day the TV forecasts rain tomorrow (water first), on Fall 28, or in Winter. In Winter, upgrade the Can and the Axe first; send the Pickaxe only on days you plan to farm, forage or socialise, never before mining days.
   6. Skip the Hoe (`till_area` also works one tile at a time), the Trash Can and the Pan.
6. **Backpack (Pierre).** Buy the 2,000g Large Pack (24 slots) as soon as your next seed buy is covered (timing: see `guide('spring')`). Tools take 5 of the 12 starting slots (6 with the Fishing Rod). Leave the 10,000g Deluxe Pack (36 slots) until Year 2.
7. **Robin's rules.**
   - Robin is away on Tuesdays. `construct_building`, `upgrade_building` and `upgrade_house` work only inside her shop during the day.
   - She takes one project at a time, and your house upgrade counts as one. Order the most urgent project first.
   - Building construction pauses on festival days and on the Year-1 Green Rain day (Summer); house upgrades do not pause. Never order a building that has to be ready for a festival or the first day of a season.
   - Coop and Barn take 3 days; Silo, Shed, Stable and building upgrades take 2; each house upgrade takes 3.
   - **Wood is 10g and Stone 20g at Robin in Year 1, and 50g and 100g from Year 2.** In Year 1, buy Wood when you have more cash than time (450 Wood = 4,500g). Before Spring 1 of Year 2, stock the Wood and Stone your Year-2 projects need.
8. **Build in this order:**
   1. **Silo** (100g, 100 Stone, 10 Clay, 5 Copper Bars) before any animal and before you cut Fall grass. Hay plan: see `guide('fall')`.
   2. **Coop, then Barn,** once crop income is stable (Summer to Fall of Year 1). Their Big upgrades and the animal plan: see `guide('animals')`.
   3. **Shed** (15,000g + 300 Wood) once machines crowd the space near the house: all Jars and Kegs in one room. Big Shed in Year 2 as Kegs scale.
   4. **Stable** (10,000g, 100 Hardwood, 5 Iron Bars), as soon as you have the Hardwood. The horse speeds every long trip; ride it with `mount`.
   5. Fish Pond: see `guide('fishing')`. Skip the Slime Hutch, Mill and Well early; the Wizard's Obelisks (500,000g and up) are endgame luxuries.
9. **House upgrades (`upgrade_house`):**
   1. **Level 1 (10,000g + 450 Wood):** adds a kitchen, so you can cook energy and buff food, and it is required before you can propose (see `guide('relationships')`). Buy it in Summer or early Fall of Year 1, after the Summer seeds and before the Fall 1 seed reserve.
   2. **Level 2 (65,000g + 100 Hardwood):** adds a nursery (needed for children) and unlocks renovations; it is required before Level 3. It earns nothing on its own. Buy it in Year 2 or later, after Kegs, a Shed and the Greenhouse are running.
   3. **Level 3 (100,000g):** a cellar plus the Cask recipe. Casks work only in the cellar. It pays only if you age high-value wine or cheese (see `guide('year2')`).
10. **Mastery** begins only when all five skills reach 10. Ignore it until then (see `guide('year2')`).

## Keep / Don't sell
- 5 bars of the right metal for each planned tool tier, on top of the bars your sprinklers and Kegs need.
- 5 Copper Bars for the Silo and 5 Iron Bars for the Stable.
- Hardwood: 100 for the Stable, 100 for the second house upgrade, and 1 per Cask.
- Battery Packs: Iridium Sprinklers need one each.
- Refined Quartz: one per Quality Sprinkler.

## Avoid
- Choosing a profession without reading both level-10 options under it. Rancher when you plan Artisan; the Scout line; Tracker; the Tapper profession (it boosts syrups only).
- Taking the Watering Can to Clint on a dry day while unsprinkled crops still need water.
- The Gold or Iridium Watering Can, or any Hoe upgrade. Bars do more as sprinklers.
- A tool pickup day on a festival, or a building whose last construction day crosses a festival.
- House upgrades 2 and 3 before the Keg engine runs.
- The 10,000g backpack or decor in Year 1.
- Selling bars, Hardwood or Battery Packs.

## Look it up
- `progress()`: each skill's level and XP to the next level, the next craft unlocks, the profession options at 5 and 10, the cost of your next tool tier against the bars you hold, and any tool at Clint.
- `status`: whether a profession choice is pending. `choose_profession` makes it.
- `list_buildings()`: what Robin builds, gold and material costs, build days, and whether you can afford each. `upgrade_house` gives the house costs.
- `item_info(name)`: recipe ingredients and unlock source for any machine, and where to find bars, Hardwood, Clay or Refined Quartz.
