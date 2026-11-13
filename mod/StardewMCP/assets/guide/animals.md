<!-- summary: Farm animals: when to build the Silo, Coop and Barn, why chickens and cows come first, when to upgrade to a Big or Deluxe Coop or Barn and what each unlocks (Ducks, Goats, Rabbits, Sheep, Pigs), daily care, grazing, feeding and Winter, and why every product goes into a machine. -->
# Animals: coops, barns and artisan goods

## When this matters
- From Summer of Year 1, once crops pay steadily. Animals are a steady second income behind crops and Kegs. Never buy them with seed money.
- Before you buy any animal, farm building or Marnie item, and every morning once you own animals.

## Priorities
1. **Silo and tools before the animal.** Build the Silo before or with the first Coop: cut grass gives Hay only while a Silo has room. For how much Hay to stock for Winter, see `guide('fall')`. Buy the Milk Pail (1,000g, Marnie) before the first cow. Have the machine for the product ready before the first animal arrives.
2. **Daily care. Production depends on it.**
   - Pet every animal every day with `pet_animals()`, inside each building and outside. An animal you did not pet loses friendship and mood overnight. Petting also gives Farming XP.
   - Collect every day with `collect_animal_products()`. If you do not milk an animal, the next day's milk replaces the old one, so you lose a day's milk.
   - **Graze on sunny days outside Winter.** Open the animal door (`interact`) once at the start of grazing season and leave it open; it stays open from day to day. Animals walk out only before 16:30, only on dry days outside Winter, and only while you are not inside the building. Eating grass sets mood to full, adds friendship and saves Hay. Leave a field of grass by each building and do not scythe all of it. Grass Starter from Pierre spreads it.
   - **Coming home:** from 17:00 animals go back inside by themselves while the door is open, whether or not you are on the Farm. Leave the door open on mine days too. Keep the way from the grass to the door clear: an animal still outside at night loses half its mood.
   - **Rain, Winter or no grass:** run `feed_animals()` in each Coop and Barn so every trough has Hay. An animal that grazed leaves the Hay, and Hay stays in the trough, so feeding every day wastes nothing. A night without food costs a lot of mood and friendship, the animal produces nothing, and a young animal does not grow that day, so its adult date slips.
   - **Winter:** put a Heater in each Coop and Barn before Winter 1. Without one, mood drops every Winter evening. Finish the Bulletin Board Fodder bundle (10 Wheat, 10 Hay, 3 Apples) before Winter: it gives one Heater free. Buy the rest from Marnie (2,000g each). Animals stay inside all Winter and eat Hay every day.
3. **Process every product.** A raw egg, milk or truffle sells for far less than what a machine makes from it.
   - Egg 50g -> Mayonnaise 190g (Mayonnaise Machine, Farming 2, needs an Earth Crystal).
   - Milk 125g -> Cheese 230g (Cheese Press, Farming 6, or the Animal bundle reward).
   - Truffle 625g -> Truffle Oil 1,065g (Oil Maker, Farming 8).
   - Only Large vs normal matters: a Large Egg or Large Milk becomes a gold-quality product, while the quality of a normal egg or milk is lost in the machine. Each batch takes 3-6 hours, so one machine per 2 animals is enough if you reload it once during the day.
   - Exception: with Botanist, truffles you pick up are iridium quality (1,250g). Without Artisan, sell those raw.
4. **Coop and chickens first (Summer Y1).** A chicken costs 800g, is adult in 3 days and lays an egg a day. It repays itself in about a week of Mayonnaise. Fill all 4 places.
5. **Barn and cows next (late Summer to Fall Y1).** A cow costs 1,500g and gives milk every day. As Cheese it repays itself in under two weeks. Time the cows for when the Cheese Press is close.
6. **Upgrade the Coop and Barn for the animals they unlock, not for room.** At Robin's, `upgrade_building(building="Big Coop")` upgrades your Coop in place in 2 days; the animals stay in and keep producing. Each tier upgrades the one before and holds 4 more animals (4, 8, 12), but a second Coop (4,000g) or Barn (6,000g) adds room for less.
   - **Big Coop** (10,000g + 400 Wood + 150 Stone): Ducks (1,200g) for the Duck Egg (Animal bundle) and Duck Feather (Dye bundle), plus an incubator.
   - **Big Barn** (12,000g + 450 Wood + 200 Stone): Goats (4,000g) for Large Goat Milk (Animal bundle) and Goat Cheese.
   - **When:** early Fall Y1, once Kegs run and seed money is safe, so the Duck and Goat (5 days to grow) reach 1 heart before Winter. Robin takes one project at a time: Big Coop first, then Big Barn. One Duck and one Goat cover the bundles; fill the rest with chickens and cows, which earn more per gold.
   - **Deluxe Coop** (20,000g + 500 Wood + 200 Stone): only for Rabbits (8,000g) and the Enchanter's Rabbit's Foot, which the Cart or the Desert Festival usually sells for less.
   - **Deluxe Barn** (25,000g + 550 Wood + 300 Stone) and Pigs (16,000g each), in Year 2 with spare cash, for the Chef's Truffle and Truffle Oil. Pigs take 10 days to grow: buy them in late Winter so they are adult on Spring 1. Sheep (8,000g) only for Wool. A Deluxe Coop or Barn fills its troughs from the Silo by itself.
   - Pigs dig truffles only outdoors, on sunny days outside Winter, on bare tiles: no grass, flooring, tilled soil, trees or objects. Keep a patch of bare dirt by the barn door. Truffles stay on the Farm overnight. Pick them up with `harvest_all` on the Farm each evening: they count as forage, so Botanist applies. Higher friendship means more truffles per day.
7. **Large products come from friendship plus mood.** An animal needs at least 1 heart to give a Large product. The chance then rises with friendship and mood. For chickens, cows and goats, at about 4 hearts and full mood almost every product is Large. Duck Feathers and Rabbit's Feet stay rare (about 1 in 4 products) even at full hearts. Daily petting and grazing from the first day pay for themselves.
8. **Auto-Grabber (25,000g, Marnie, needs Farming 10) only for buildings with 8 or more animals,** and only when your time is the limit. It gives no XP and none of the friendship from milking. Keep petting by hand.
9. **Professions:** do not take Rancher over the Artisan path (see `guide('progression')`).

## Keep / Don't sell
- Pantry Animal bundle: any 5 of Large Milk, Large White Egg, Large Brown Egg, Large Goat Milk, Duck Egg and Wool. Chickens from Marnie come white or brown at random (see `guide('community_center')`). Large Goat Milk and Duck Egg need the Big upgrades, Wool a Deluxe one.
- Artisan bundle: Cheese and Goat Cheese.
- Bulletin Board: 10 Hay for the Fodder bundle, a Truffle, a Duck Feather and a Rabbit's Foot.
- Large Eggs and Large Milk go to the machines once the bundle has its copy.
- Run `bundles(action=keep)` before every bulk sale.

## Avoid
- Buying animals before the Silo, the Milk Pail or the machine that processes their product.
- Buying any animal with Spring Y1 seed money.
- Upgrading only for more places while the Farm has room for a second Coop or Barn.
- Goats, sheep, rabbits or extra ducks for money beyond the bundle copies. They produce only every 2-4 days, so a chicken or cow earns more per gold spent.
- Selling raw eggs, milk or truffles while a machine is free.
- Scything every blade of grass around the Coop and Barn in grazing season.
- Closing the animal door every evening or on mine days: animals then miss the grass, and they come home by themselves anyway.
- Blocking the way between the grass and the animal door with fences, machines or crops.
- Leaving animals hungry on a rainy or Winter day.
- Skipping a day of milking.

## Look it up
- `list_animals()`: which animals are petted, whose products are ready, and how much Hay is in the Silo.
- `list_animals_for_sale()`: what Marnie sells today, prices, the building each animal needs, and free places.
- `list_buildings()`: costs, materials and build days for new buildings and upgrades, and which of your buildings can take each upgrade.
- `machines(item='Milk')`: outputs, prices and times. `machines()`: which machines are ready or empty.
- `item_info('Truffle')`: the price with your professions and what to keep it for.
- `bundles(room='Pantry')`: the animal products the bundle still needs.
- `progress()`: your Farming level and when the Mayonnaise Machine, Cheese Press and Oil Maker unlock.
