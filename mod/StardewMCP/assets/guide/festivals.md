<!-- summary: Every festival ranked by value: date, goal and what to buy or bring, including the Fair grange and Stardrop, Luau soup tiers, Desert Festival, Trout Derby, SquidFest and Night Market. -->
# Festivals

## When this matters
- Check `calendar` the day before. A main festival (Egg, Flower Dance, Luau, Jellies, Fair, Spirit's Eve, Ice, Winter Star) takes over its map for the day and the shopkeepers go to it: buy seeds and supplies the day before, and do chores before it opens.
- Day festivals send you home at 22:00 and evening ones (Jellies, Spirit's Eve) at midnight. Go straight to bed after an evening festival: going to bed after 24:00 cuts the next day's energy.

## Priorities
1. **Talking to Lewis starts the main event, and the festival ends after it.** Shop, gift, fill the soup pot and find your dance partner first, then `festival('start')`. The Fair and Spirit's Eve have no main event: you stay until you `festival('leave')`.

2. **Stardew Valley Fair (Fall 16, Town, 09:00-15:00): win the grange, then get the Stardrop.**
   - Grange score = 14 + item points + 5 per category (max 30, so 6 categories) + 9 - 2 per empty slot. Fill all 9 slots.
   - Categories: vegetable, fruit, animal product, artisan good, cooked dish, fish, forage/flower/syrup, mineral/gem. Truffles fit no category. Duplicates are not penalized.
   - Item points use the quality-adjusted sell price: gold 90g+, silver 200g+ or normal 300g+ = 5; gold 200g+, silver 300g+ or normal 400g+ = 6; iridium = 5-8. Nine 5-point items in 6 categories = 98.
   - 90+ wins 1st place and 1,000 Star Tokens. 75+ gets 500, 60+ gets 250, anything less 50.
   - Collect the items on Fall 15 (see `guide('fall')`), fill the grange and talk to Lewis to have it judged. Talk again for tokens. Take the items back before you leave (leftovers go to Lewis's Lost and Found).
   - **Wheel: bet green, about half your tokens per spin.** Green wins about 73% and pays even money.
   - Buy the Stardrop first (2,000 tokens; +34 max energy for good; one per save). Then buy a Prize Ticket (1,000), or the Glowstone Ring if it is in today's rotating slot.
   - Tokens reset when the Fair ends. A little short of 2,000? Buy tokens at 50g each for the Stardrop.

3. **Luau (Summer 11, Beach, 09:00-14:00): one soup item, put in before you talk to Lewis.** Hold the item and interact with the soup pot. It changes friendship with almost every villager. Tiers use the base price and the Edibility field:
   - +120: gold or iridium quality and base 160g+, or silver with base 300g+ and Edibility above 10.
   - +60: any item with base 100g+, Edibility 20+ (about 50 energy), or silver-or-better with base 70g+.
   - 0: adding nothing, any inedible item under 100g, or a cheap but decent food.
   - −50: a cheap edible item that fails every tier above.
   - −100: anything with negative edibility: Void Egg, Red Mushroom, Holly, Sap, Pufferfish.
   - Co-op: every farmer's item applies its friendship change to everyone, and the worst item sets the governor's reaction. Agree that each farmer adds a +120 item or nothing.
   - Easy +120 picks: gold Melon, Cauliflower, Catfish, Sturgeon, Mayonnaise (from a Large Egg) or Cheese (from Large Milk).
   - Don't buy the 3,000g Starfruit at Pierre's booth: normal quality, only +60.

4. **Egg Festival (Spring 13, Town, 09:00-14:00).**
   - Goal: Strawberry Seeds, 100g each at Pierre's booth. No shop sells them for gold on any other day. Planting plan: see `guide('spring')`.
   - The egg hunt (`festival('eggs')`) needs 9 eggs alone (6 / 5 / 4 with 2 / 3 / 4 farmers). First win: a Straw Hat; later wins: a Prize Ticket. Worth a try, not worth planning the day around.

5. **Feast of the Winter Star (Winter 25, Town, 09:00-14:00).** Lewis's letter on Winter 18 names your secret-gift recipient.
   - The gift counts five times (a loved gift = +400 at normal quality, higher with quality) and does not use a weekly gift slot.
   - Golden Pumpkin, Pearl or Rabbit's Foot are universal loves; check the recipient's hates with `villagers(name)` first.

6. **Flower Dance (Spring 24, Forest, 09:00-14:00).**
   - Ask an unmarried candidate at 4+ hearts before talking to Lewis; if they accept, you get +250 friendship (1 heart). Below 4 hearts they refuse. Dancing with your spouse gives no bonus.
   - The booth sells Daffodil and Dandelion at 50g each, a cheap rescue for the Spring Foraging bundle.

7. **Desert Festival (Spring 15-17, Calico Desert, from 10:00).** Needs desert access (Vault bundles or Joja bus), so usually Year 2+.
   - Currency is Calico Eggs, and they vanish the night after Spring 17. Spend every egg by then.
   - Earn eggs: racer guesses (free; a correct guess pays 20), the scholar's quiz (50 eggs for answering all 4 questions right, once per year; one wrong answer ends it for the day), the daily quest, artifact spots, and above all the Skull Cavern (rocks and monsters drop eggs).
   - In the Skull Cavern, activate Calico Statues and reach new depths (+1 per 5 floors) to raise the egg rating (more eggs drop), then claim Gil's prize at the festival: 200 eggs at rating 25+, 500 eggs plus a Magic Rock Candy at 55+.
   - Best buys: the Prismatic Shard (Emily's stall), Magic Rock Candy, Iridium Bars, Rabbit's Foot, Mega Bombs, the Woodcutter's Weekly book, and Strawberry Seeds at the egg shop (5 eggs on Spring 15, 6 on Spring 17). Stalls vary by day: `shop_list`.

8. **Trout Derby (Summer 20-21, all day).** Fish the Cindersap Forest river: during the Derby, Rainbow Trout bite there in any weather at any hour.
   - Each Rainbow Trout catch has a 33% chance per fish caught to give a Golden Tag (pond trout never do). Trade tags at the Derby booth.
   - The first tag always gives a Tent Kit. Later tags cycle through 10 prizes (the start is random per save): Crab Pot, 3 Mystery Boxes, Diamond, 20 Deluxe Bait, 2 Triple Shot Espresso, Quality Sprinkler, 3 Warp Totems: Farm, 3 Omni Geodes and two cosmetics.

9. **Spirit's Eve (Fall 27, Town, 22:00; entry closes 23:50).** Go straight for the maze chest: in odd years it is at tile 33,13 and holds a Golden Pumpkin; in even years it is at tile 63,16 and holds a Prize Ticket. Then go to bed.

10. **SquidFest (Winter 12-13, Beach).** During it Squid bite all day at the beach (normally only from 18:00). Squid you catch are counted automatically; visit the booth each day to claim the tiers reached (you keep the Squid).
    - Winter 12: 1 / 3 / 5 / 8 Squid. The 5-Squid tier gives a Pearl and Coffee.
    - Winter 13: 2 / 5 / 7 / 10 Squid. The 7-Squid tier gives a Treasure Chest and Triple Shot Espresso.
    - First top tier: The Art O' Crabbing (crab pots sometimes give double).

11. **Festival of Ice (Winter 8, Forest, 09:00-14:00).** No rod needed: the contest lends you a Bamboo Pole with a Dressed Spinner. Catch 5+ fish in the 2-minute contest, and more than any other farmer, to win.
    - First win: Sailor's Cap, Barbed Hook and Dressed Spinner (tackle for the Iridium Rod) and a Magnet (bait).
    - Later wins: a Prize Ticket.

12. **Night Market (Winter 15-17, Beach, from 17:00).** It does not take over the day. Seed boat, free nightly gift and Traveling Cart: see `guide('winter')`.
    - Submarine (1,000g): the only source of Blobfish, Midnight Squid and Spook Fish. Take it once, for the collection.
    - Mermaid boat: tap the shells in the order 1-5-4-2-3 for a free Pearl, once per save.
    - Warp home (250g) if walking would pass midnight.

13. **Dance of the Moonlight Jellies (Summer 28, Beach, 22:00).** A scene only. Optional.

## Keep / Don't sell
- From Summer 1 until the Luau: one +120 soup item (gold, base 160g+).
- Until Fall 16: your best item in each grange category, 9+ items, at least 6 categories.
- Until Winter 25: one loved gift for the Winter Star recipient.
- Prize Tickets: redeem them at the prize machine in Lewis's house. Don't sell them.
- The Golden Pumpkin: a 2,500g sale or a universal loved gift; save it for a birthday or the Winter Star.

## Avoid
- Talking to Lewis before shopping, soup, partner or gifts are done.
- Arriving after entry closes (14:00 for most day festivals, Fair 15:00, Spirit's Eve 23:50).
- Festival decor and Rarecrows in Year 1.

## Look it up
- `calendar`: festival dates, places, hours and open shops.
- `shop_list`: a booth's stock.
- `item_info(name)`: base price, quality prices and edibility, to check Luau tiers and grange points.
- `villagers(name)` and `villagers(item=X)`: Winter Star gift tastes and current hearts for the Flower Dance.
- `fish_info(fish='Rainbow Trout')`, `fish_info(fish='Squid')`: where and when they bite.
