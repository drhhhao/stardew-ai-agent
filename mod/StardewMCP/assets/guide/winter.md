<!-- summary: Winter Year 1 plan: Mines to floor 120, back-to-back tool upgrades, winter forage and seeds, Night Market seeds, buildings and the setup for entering Year 2 with 25k+. -->
# Winter (Year 1)

## When this matters
- Winter 1 to 28. All non-winter outdoor crops died overnight, and it never rains unless you use a Rain Totem. Energy goes to the Mines, tools, buildings, crafting and social time.
- Read on Fall 28 or Winter 1 to plan the month.

## Priorities
1. **Finish the Mines to floor 120** if Fall did not. Go down most non-festival days. The floor-120 chest holds the Skull Key for the Skull Cavern once the bus is fixed (see `guide('year2')`). Mine floors 80+ for Gold Ore and keep smelting: Quality Sprinklers and every tool tier need bars. Tactics: see `guide('mines')`.
2. **Keep Clint busy all month.** He keeps a tool 2 nights; pick it up and hand in the next.
   - Watering Can and Axe first: the Can sits idle now (an unwatered Greenhouse only pauses), and the Steel Axe clears the log into the Secret Woods, the Hardwood source (Stable, second house upgrade).
   - Hand in the Pickaxe only before days you will not mine: Steel breaks farm boulders; Gold before the Skull Cavern. Skip the Hoe. Full order: see `guide('progression')`.
   - Keep pickup days off Winter 8 and 25 (festivals), and never spend into the Year 2 reserve (step 9).
3. **Finish Winter Foraging (Crafts Room):** Crystal Fruit, Crocus, Snow Yam, Winter Root.
   - Crystal Fruit and Crocus lie on the ground in the Forest, Mountain, Town, Railroad and Bus Stop.
   - Snow Yam and Winter Root come only from digging. Dig every artifact spot (wiggling worms; also museum artifacts). Hoeing snowy ground off the farm finds one only about 8% of the time.
4. **Plant every winter crop you get.** Only Winter Seeds, Powdermelon Seeds and Fiber Seeds grow outdoors in winter; each takes 7 days, so plant by Winter 21.
   - The bundle pays 30 Winter Seeds (random winter forage). At Foraging 7 you can craft more from one of each forage.
   - Dig Seed Spots too (about 1 dig spot in 6). On Winter 1-20 they give 2-3+ Powdermelon Seeds: plant them at once. From Winter 21 they give Carrot Seeds: keep those for Spring 1. Mine crates and barrels also drop Powdermelon Seeds.
5. **Night Market (Winter 15-17, from 17:00, Beach).** The Magic Boat sells seeds at Pierre's prices: Spring seeds on the 15th, Summer (Blueberry, Melon, Hops) on the 16th, Fall (Cranberry, Pumpkin, Grape) on the 17th.
   - With the Greenhouse open, fill it with Blueberries or Cranberries until Ancient Fruit takes over (see `guide('year2')`).
   - Otherwise buy Spring 1 seeds on the 15th so you can plant at dawn on Spring 1.
   - Take the free Coffee each night and save it for deep Mines runs.
   - At the Traveling Cart stall, buy only a Rare Seed or a missing bundle item. Other stalls: see `guide('festivals')`.
6. **Winter fish.**
   - Glacierfish at Fishing 6: one per save, a hard fight. Spot and gear: `guide('fishing')`, `fish_info(fish='Glacierfish')`.
   - Sturgeon (Mountain lake) and Tuna (ocean) bite only in summer and winter. Catch them now if `bundles` lists them.
   - Rain-only fish (Catfish, Walleye, Red Snapper) need rain. Catch them on the next rainy day in their season (Catfish Spring, Red Snapper Summer, Walleye Fall), or use a Rain Totem now.
7. **Festivals** (rules and rewards: see `guide('festivals')`):
   - Festival of Ice (Winter 8): the contest hands you its own rod.
   - SquidFest (Winter 12-13, Beach): worth it if you can fish.
   - Feast of the Winter Star (Winter 25): Lewis's Winter 18 letter names your secret-gift recipient. Check their loves with `villagers(name)` and have one ready; if it shows no tastes, give a universally loved item (see `guide('relationships')`).
8. **Build for Year 2.** Robin builds one project at a time, so order early.
   - First house upgrade (kitchen) before Spring if not done; cost in `guide('progression')`.
   - Then a Silo and a Coop or Barn if you have no animals, with Mayonnaise Machines or Cheese Presses (see `guide('animals')`). A Shed once machines outgrow the space.
   - Chop trees and keep Tappers on Oaks all winter: Oak Resin limits Kegs.
   - Stock the Wood and Stone your Year-2 projects need from Robin now; his prices rise 5x in Year 2 (see `guide('progression')`).
9. **Winter 26-28: set up Spring 1.**
   - Place 20+ Quality Sprinklers to cover every Spring field (see `guide('money')`). Plant on Spring 1 only what they cover; seed plan in `guide('spring')` and `guide('year2')`.
   - Craft fertilizer, clear and lay out the fields, load Kegs and Jars.
   - Scale Kegs as soon as you have fruit to fill them; pick Artisan at Farming 10 when you get there.
   - Community Center order: see `guide('community_center')`.
   - Enter Year 2 with at least 25,000g cash after all of the above.
10. **Optional: A Winter Mystery.** Walk from the Farm into the Bus Stop between 06:00 and 16:00 on any winter day to see a shadowy figure. Follow its footprints into Town and shake the bush at tile 28,14 for the Magnifying Glass (finds Secret Notes). It takes minutes.

## Keep / Don't sell
- One Crystal Fruit, Crocus, Snow Yam and Winter Root until the bundle is done.
- Winter and Powdermelon Seeds (plant them); late-winter Carrot Seeds (Spring 1).
- Sturgeon and Tuna if a bundle needs them; the Glacierfish (see `guide('fishing')`).
- Gold and Iron Bars, Refined Quartz, Quartz, Oak Resin, Hardwood.
- Night Market Coffees.

## Avoid
- Planting outdoor seeds other than Winter, Powdermelon or Fiber Seeds: they will not grow.
- Waiting for rain to fish: it never rains in winter unless you use a Rain Totem.
- A Clint pickup that falls on Winter 8 or 25.
- Night Market paintings, decor or furniture before tools, buildings and sprinklers.
- Paying Year 2 prices for Wood and Stone.
- Letting Hay run out: animals stay indoors all winter.
- Dropping below the 25,000g reserve for anything that can wait until Spring.

## Look it up
- `progress()`: mine depth, tool tiers and upgrade costs, skills and next unlocks. `inventory`: bars on hand.
- `bundles(missing_only=true)`, `bundles(action=keep)`: winter items and fish still needed.
- `fish_info(season='winter')`: where and when winter fish bite.
- `item_info('Quality Sprinkler')`: recipe. `item_info('Snow Yam')`: where found.
- `crops(season='spring')` (or summer/fall for the Greenhouse): seed prices in every shop, including the boat.
- `calendar`: festivals and birthdays. `machines`: Keg and Jar outputs.
