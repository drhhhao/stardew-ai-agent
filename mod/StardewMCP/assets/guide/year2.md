<!-- summary: Year 2 and late game: greenhouse Ancient Fruit, desert and Skull Cavern, Iridium Sprinklers, Kegs, Ginger Island, the Missing Bundle, house and casks. -->
# Year 2 and late game

## When this matters
- From Winter Y1 on: the greenhouse is open or close, the Mines reach floor 120, and seasonal crops stop growing your income.
- Reread before the first bus trip, Skull Cavern run and boat repair.

## Priorities
1. **Fill the greenhouse with Ancient Fruit.** It never dies there: after a 28-day first growth it fruits every 7 days forever.
   - Seed: donate your first Ancient Seed artifact to the museum. You get 1 Ancient Seeds and the recipe that turns every later Ancient Seed artifact into a packet of Ancient Seeds.
   - Multiply: the Seed Maker (Farming 9) turns 1 fruit into 1-3 seeds. Seed every fruit until the greenhouse is full, then Keg the fruit. The Traveling Cart no longer sells the seeds.
   - Fill gaps with the best regrow crop from `crops(season="greenhouse")`; fruit trees on the stone border fruit all year.
2. **Open the bus to the desert early in Year 2** (Vault bundles; see `guide('community_center')`). A ticket costs 500g and runs only while Pam stands at the stop (late morning to late afternoon). For an early start, use a Warp Totem: Desert (Desert Trader: 3 Omni Geodes each, or its recipe for 10 Iridium Bars).
   - **Oasis seeds:** Starfruit Seeds 400g (Summer, 13 days), the best Summer crop, and Rhubarb Seeds 100g (Spring, 13 days). Buy only what your Kegs can process.
   - **Deluxe Speed-Gro:** Sandy, 80g, Thursdays (Pierre: 150g from Year 2).
   - Get the Galaxy Sword on the first desert trip; see `guide('mines')`.
   - **Desert Trader:** 5 Quartz buys a Bomb any day, 1 Jade buys a Staircase on Sundays.
   - Desert Festival (Spring 15-17): see `guide('festivals')`.
3. **Skull Cavern** (Skull Key from Mines floor 120): the iridium source.
   - Go only on good-luck days and arrive early.
   - **Loadout:** Galaxy Sword (Obsidian Edge at minimum), 20-40 healing foods, 20-30 bombs, 10+ Staircases, a luck or speed buff food. With a weak weapon, turn back near floor 50.
   - **Each floor:** if no ladder shows, bomb the densest rock cluster; after 2-3 empty bombs, place a Staircase. Take holes over ladders: they drop several floors for 3 HP each but never below 1 HP; heal after.
   - Don't mine above floor 50; iridium thickens deeper. Finish Mummies with a bomb, fight Serpents in corridors.
   - **First run:** reach floor 25 for Qi's Challenge (10,000g).
   - No elevator. Leave by 01:30; a Warp Totem: Farm (Foraging 8) is the exit. Knockouts: see `guide('mines')`.
4. **Iridium Sprinklers at Farming 9** (1 Gold Bar + 1 Iridium Bar + 1 Battery Pack; waters 5x5). Battery Packs come from Lightning Rods (Foraging 6) on storm nights: place many before Summer. Krobus sells one Fridays for 10,000g: only with idle cash.
5. **Scale Kegs and Jars to your harvest.** A Shed (later Big Shed) is the machine hall.
   - One Keg per Ancient Fruit plant, plus Starfruit Kegs; cheap produce: see `guide('money')`. Oak Resin limits Kegs: tap spare Oaks.
   - Refill from `machines()` every morning.
6. **Year-2 seeds at Pierre** (also the Night Market boats): Garlic (Spring), Red Cabbage (Summer), Artichoke (Fall). Red Cabbage once if the Dye bundle needs it; Garlic and Artichoke fill end-of-season gaps.
7. **Ginger Island, after the CC (or Joja) is done.** Willy's letter opens his back room; repair the boat with 200 Hardwood, 5 Battery Packs and 5 Iridium Bars, then `travel_to_location('Ginger Island')`.
   - These compete with Iridium Sprinklers: stock both.
   - Golden Walnuts: 1 to the parrot in Leo's hut, then 10 to the turtle's parrot on the south beach, open the island farm, where crops grow all year.
   - The Volcano gives Cinder Shards and the Forge at its bottom.
8. **The Missing Bundle** (it gives the Movie Theater). The abandoned JojaMart opens on the first rainy day after the CC ceremony. Hand in any 5 of: silver+ Wine, Dinosaur Mayonnaise, Prismatic Shard, 5 gold Ancient Fruit, gold Void Salmon, Caviar.
   - Kegs make only normal Wine; silver needs 14 days in a Cask (item 10). Without the cellar, you need all five other slots.
   - `donate_bundle` does not reach it: go into the abandoned JojaMart, interact with the Junimo note and use `menu()`.
9. **Second house upgrade:** once the greenhouse is full and Kegs match your fruit. It adds the nursery (needed for children) and is required before the third upgrade; costs in `guide('progression')`.
10. **Casks:** the third upgrade gives the cellar and the Cask recipe. Casks work only in the cellar.
    - Age only Starfruit and Ancient Fruit Wine (double value at iridium, 56 days) and spare Cheese (14 days).
11. **Mastery: ignore it** until all five skills are 10; earlier XP does not count.

## Keep / Don't sell
- Every Ancient Fruit until the greenhouse is full.
- For the Missing Bundle: 5 gold Ancient Fruit, one Prismatic Shard beyond the Galaxy Sword's, and one Wine aged to silver.
- Jade (Sunday Staircases), Quartz (Bombs), Omni Geodes (desert totems), Battery Packs, Iridium Bars, Oak Resin and Hardwood.

## Avoid
- Buying Wood, Stone or ore: Robin's and Clint's prices jump in Year 2. Gather them; see `guide('progression')`.
- Skull Cavern on a bad-luck day or with fewer than 20 heals.
- Planting more Starfruit or Hops than your Kegs can take in a week.
- An Iridium Watering Can when sprinklers cover every crop.
- Obelisks and decor before the greenhouse, Kegs and Iridium Sprinklers are done. The 10,000g Deluxe Pack is fine before regular Skull Cavern runs.

## Look it up
- `crops(season)`, `crops(season="greenhouse")`: seed prices from every shop, profit per tile; `machines(item=X)` for Keg/Jar value.
- `machines(item="Ancient Fruit")`: Seed Maker, Keg and Jar outputs.
- `item_info("Void Salmon")`, `item_info("Caviar")`: sources of Missing Bundle items.
- `bundles(room="Vault")` for the bus; `bundles(room="Joja")` for the Missing Bundle.
- `status`: today's luck and tomorrow's weather.
