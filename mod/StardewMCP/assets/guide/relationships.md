<!-- summary: Friendship and marriage: daily talks, gift slots, birthdays, what never to give, decay caps, the pendant path and spouse upkeep. -->
# Relationships: friendship, gifts and marriage

## When this matters
- Every day you pass villagers, every gift, and before you spend on a Bouquet, the Mermaid's Pendant or the house upgrade.
- Friendship pays in recipes, mailed gifts and a spouse Stardrop, not cash. Never detour far for it in Year 1.

## Priorities
1. **Talk to every villager you pass, every day.** A talk is +20 points (250 points = 1 heart) and free. Only talking stops daily decay: a gift alone does not count. Gift with `give_gift(name, item)`.
2. **Spend both weekly gift slots on one target.** Each villager takes at most 1 gift a day and 2 a week; the week resets on Sunday, and giving both adds +10 at the reset. Daily talk plus 2 loved gifts reaches 8 hearts in about 6.5 weeks. Befriend everyone else by talking only.
3. **Birthdays multiply a gift by 8.** A loved gift is worth 640 points, a liked one 360. The birthday gift is allowed even when the weekly 2 are used. Check `calendar` and `villagers()` each morning and hold a loved item for each birthday you care about. Never give a disliked or hated item on a birthday (-160 / -320); a neutral one gives only +160.
4. **Pick by taste, then quality.** Loved +80, liked +45, neutral +20 (same as a talk), disliked -20, hated -40. Quality multiplies only liked and loved gifts (silver x1.1, gold x1.25, iridium x1.5), so a normal loved gift beats an iridium liked one. Keep gold and iridium for bundles and the Fair. Never guess a taste: check `villagers(name)`; if a taste is unknown, use the universal lists below.
5. **Use the decay caps, then stop investing.** Without talks, points drop 2 a day, only below 8 hearts for a candidate you are not dating and below 10 hearts for everyone else. An undated candidate cannot pass 8 hearts until you give a Bouquet. So push candidates you might date to 8 hearts and stop there until you decide; push non-datable villagers you care about to 10 hearts, where their hearts stay free.
6. **Take the cheap heart sources:**
   - Help Wanted deliveries: +150 with the requester plus gold.
   - The Introductions quest (meet everyone in town): +100 with each.
   - Finishing the Bulletin Board room: +500 with every non-datable villager you have met (see `guide('community_center')`).
   - Flower Dance partner, Luau soup, Winter Star secret gift (x5): see `guide('festivals')`.
   - Movies: a 1,000g ticket invites one villager a week; loved movie +200, liked +100; buy a snack they love (+50) or like (+25).
   - The Friendship 101 book (Prize Machine, Bookseller) adds 10% to every gain for good.
7. **Universal loves work on almost anyone:** Pearl, Golden Pumpkin, Magic Rock Candy, Rabbit's Foot (Penny hates it), Prismatic Shard (Haley hates it; keep Shards early). Stardrop Tea gives a flat +250 (up to 750 on a birthday) and ignores the daily and weekly limits. Diamond is loved by 8 villagers and liked by the rest: the best stock gift.
8. **Fallback likes** when you lack a loved item (+45 beats a neutral +20): vegetables and flowers (any farm crop of those groups, e.g. Parsnip, Cauliflower, Tulip), gems, cooked dishes, artisan goods, fruit-tree fruit, Coffee, Maple Syrup, Mead, Pina Colada, Rainbow Shell, Treasure Chest. Some villagers dislike a whole group (vegetables, artisan goods, flowers or gems): confirm with `villagers(name)`.
9. **Choose one marriage candidate early:** the one whose loved items you can produce cheaply and repeatedly. Focus gifts there; talk to the others.
10. **Marriage path, in order:**
    1. Reach 8 hearts; a letter arrives and Pierre sells the Bouquet (200g). Give it with `bouquet(name)`: you are dating, +25, and the cap rises to 10 hearts. While dating, each day without a talk costs 10 points.
    2. Reach 10 hearts; Lewis writes about the pendant.
    3. Get the first house upgrade (kitchen): 10,000g and 450 Wood from Robin. No pendant without it.
    4. Repair the beach bridge to the tide pools (300 Wood).
    5. On a rainy day before 19:00 buy the Mermaid's Pendant (5,000g) from the Old Mariner at the tide pools. Winter has no rain: buy it before Winter or use a Rain Totem.
    6. Give it with `mermaid_pendant(name)`. The wedding follows 3 days later.
    - Total about 15,200g plus 750 Wood: start once the money engine (Kegs, sprinklers) runs.
11. **Heart events:** pick kind, supportive answers; a rude one can cost hundreds of points. If you date all six same-gender candidates at 10 hearts and have seen their 10-heart events, they confront you as a group: carry a Rabbit's Foot from then on and it goes well. Trigger it before you give a Mermaid's Pendant: it cannot happen once you are engaged or married.
12. **Spouse upkeep:**
    - Talk to your spouse every day: a missed day costs about 13 points even at max hearts (all spouse gains and losses count about two-thirds; gifts about a third, so talking is the cheap way up).
    - A spouse takes 1 gift every day (no weekly cap); a cheap liked item daily also stops bad moods.
    - Keep the spouse at 12+ hearts, or 10+ with a kiss every day and a gift every day: below that, after the first week of marriage, they can wake in a bad mood and skip chores.
    - At 12.5 hearts, talking gives a Stardrop (permanent max energy) once per save.
    - Chores are random and partial: keep watering yourself.
    - Keep the kitchen, bed and porch paths clear of furniture.
    - After marriage, a non-birthday gift to another dated candidate of your spouse's gender has a 30% chance to cost about 20 spouse points; avoid it.
13. **Special villagers:**
    - **Dwarf:** gains no friendship until you own the Dwarvish Translation Guide (museum reward for all 4 Dwarf Scrolls).
    - **Krobus** (Sewers, Rusty Key): non-datable, cap 10 hearts. At 10 hearts, with the first house upgrade and while not engaged or married, the Desert Trader sells the Void Ghost Pendant for 200 Void Essence: Krobus becomes a roommate (no chores or kids, same Stardrop).

## Keep / Don't sell
- 1 loved item per upcoming birthday you care about.
- Pearls, Golden Pumpkins, Magic Rock Candy and Stardrop Tea: birthdays or blocked weeks only.
- A Rabbit's Foot if you date several candidates.
- Diamonds not needed for bundles or the museum.

## Avoid
- **Never give** (universal hates): trash (Driftwood is only disliked), bait, Sap, Weeds, Hay, Carp, Seaweed, Green and White Algae, Sea Urchin, Red Mushroom, Snail, Slime, Bug Meat, Bat Wing, Copper, Iron and Radioactive Ore, Radioactive Bar, Strange Bun, Seafoam Pudding, Oil of Garlic, Void Mayonnaise (Krobus loves it), Poppy (Penny loves it), Rusty Spoon/Spur/Cog, Chewing Stick.
- **Usually disliked:** building materials, most fish, seeds, fertilizer, ores and bars, geodes and geode minerals, monster loot, cooking ingredients (Oil, Vinegar, Flour, Rice), crafted equipment (sprinklers, bombs, fences, tackle).
- Giving the pendant at 6-9 hearts: -20, then -50 on every retry.
- Gifts on the Year-1 Green Rain day: refused; just talk.
- `search_trash` in view of a villager: -25 with each one who sees it.
- Hitting villagers with the Slingshot: -30.
- Giving a Wilted Bouquet: it ends dating.

## Look it up
- `villagers()`: hearts, points, talked today, gifts this week, birthdays soon, who can be dated.
- `villagers(name)`: loves, likes, hates, what dating or marriage still needs, your last gift.
- `villagers(item=X)`: who loves, likes or hates an item you hold.
- `calendar`: birthdays and festivals ahead.
- `item_info(name)`: heart-level recipe mail, and bundle needs before you gift an item.
- `list_quests()`: open Help Wanted deliveries.
