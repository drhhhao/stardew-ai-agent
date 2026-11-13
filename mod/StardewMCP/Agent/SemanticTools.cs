using System.Collections.Generic;

namespace StardewMCP.Agent;

/// <summary>
/// Semantic tools (docs/tool-decoupling/README.md): single-intent replacements for the mode-switch legacy tools.
/// Phase 1: quests, museum, special_orders. Phase 2: craft, cook, crack_geode, buy_animal, animals, build.
/// Phase 3: chest, talk_to (and the frequent menu/interact intents). Phase 4A: plant_item and the
/// explicit menu decisions replacing plant and close_menu.
/// A read only reads, an action does one named job and carries a required reason; pure observations are Instant
/// and need no action parameter. Only the ToolDef registrations live here — the Run bodies are the AgentTools
/// methods in Agent/Semantic/, reusing the legacy implementations so behavior cannot drift.
/// </summary>
public static partial class AgentTools
{
    private static List<ToolDef> SemanticToolDefs() => new()
    {
        new()
        {
            Name = "list_quests",
            Description = "Everything you can work towards, with game time frozen: your journal (Help Wanted and Story quests) with objective, " +
                          "progress, days left and reward; finished quests whose money is not collected yet; today's Help Wanted board in Town " +
                          "and whether it is still free; the team's Special Orders; the Community Center / Joja line. " +
                          "Collect a finished quest's money with claim_quest_reward; take today's board request with accept_quest.",
            Schema = Obj(new { }),
            Instant = true,
            Run = ListQuestsSemantic
        },
        new()
        {
            Name = "claim_quest_reward",
            Description = "Open the journal and collect the money reward of a finished quest. quest is its exact title as list_quests shows it, " +
                          "or leave it out to claim the first finished one. list_quests first shows which quests have money waiting.",
            Schema = Obj(new
            {
                quest = Str("Optional: exact title of a finished quest, as list_quests shows it; omit to claim the first one"),
                reason = Str(ReasonDesc)
            }, "reason"),
            Run = ClaimQuestRewardSemantic
        },
        new()
        {
            Name = "list_museum_donations",
            Description = "The Museum (ArchaeologyHouse, Gunther): the items in your inventory that the Museum still accepts (one of each artifact " +
                          "and mineral not on display yet), and how many rewards Gunther is holding for you. Pure observation; game time stays frozen. " +
                          "Donate with donate_museum while standing inside the Museum; collect with claim_museum_rewards.",
            Schema = Obj(new { }),
            Instant = true,
            Run = ListMuseumDonationsSemantic
        },
        new()
        {
            Name = "donate_museum",
            Description = "Donate an item from your inventory to the Museum's next free display spot. You must be inside the Museum " +
                          "(travel_to ArchaeologyHouse). item \"all\" donates every acceptable item at once. The Museum keeps one of each; " +
                          "rewards at donation milestones are collected with claim_museum_rewards.",
            Schema = Obj(new { item = Str("Item name from your inventory, or \"all\""), reason = Str(ReasonDesc) }, "item", "reason"),
            Run = DonateMuseumSemantic
        },
        new()
        {
            Name = "claim_museum_rewards",
            Description = "Talk to Gunther inside the Museum and take the rewards you have earned from Museum donations. " +
                          "You must be inside the Museum (travel_to ArchaeologyHouse).",
            Schema = Obj(new { reason = Str(ReasonDesc) }, "reason"),
            Run = ClaimMuseumRewardsSemantic
        },
        new()
        {
            Name = "list_special_orders",
            Description = "Walk to the Special Orders board in Pelican Town, open it and show the orders posted now: requester, what they want, " +
                          "reward and time limit (index 0 = left, 1 = right). Time passes while you walk. accept_special_order takes one; " +
                          "active_special_orders shows the ones already running, without walking.",
            Schema = Obj(new { reason = Str(ReasonDesc) }, "reason"),
            Run = ListSpecialOrdersSemantic
        },
        new()
        {
            Name = "active_special_orders",
            Description = "The Special Orders the team has already accepted, with objectives, progress and days left. " +
                          "Game time stays frozen; no walking. list_special_orders reads the board instead.",
            Schema = Obj(new { }),
            Instant = true,
            Run = ActiveSpecialOrdersSemantic
        },
        new()
        {
            Name = "accept_special_order",
            Description = "Accept the order at index N (0 = left, 1 = right) from the open Special Orders board; list_special_orders shows the " +
                          "board. An accepted order is a job for the whole team. The board stays open: close_menu leaves it.",
            Schema = Obj(new { index = Int("The order's index as list_special_orders shows it (0 = left, 1 = right)"), reason = Str(ReasonDesc) }, "index", "reason"),
            Run = AcceptSpecialOrderSemantic
        },
        // ------------------------------------------------------------- phase 2: crafting and cooking
        new()
        {
            Name = "list_recipes",
            Description = "The crafting recipes you know, with their ingredients and how many you can craft right now from your inventory. " +
                          "Game time stays frozen; pure observation. Craft one with craft_item; dishes are list_dishes, not here.",
            Schema = Obj(new { }),
            Instant = true,
            Run = ListRecipesSemantic
        },
        new()
        {
            Name = "craft_item",
            Description = "Craft items from a crafting recipe you know, using ingredients in your inventory (costs no energy). " +
                          "recipe is the exact name list_recipes shows; quantity is how many crafts (default 1; one craft can make several items, e.g. torches). " +
                          "Crafted machines, chests, sprinklers and fences are inventory items: put them in the world with place.",
            Schema = Obj(new
            {
                recipe = Str("Exact recipe name as list_recipes shows it, e.g. \"Chest\" or \"Furnace\""),
                quantity = Int("How many crafts (default 1)"),
                reason = Str(ReasonDesc)
            }, "recipe", "reason"),
            Run = CraftItemSemantic
        },
        new()
        {
            Name = "list_dishes",
            Description = "The cooking recipes (dishes) you know, with ingredients and how many you can cook right now; counts include your pack and the " +
                          "fridge of your home. Game time stays frozen; pure observation. Cook one with cook_dish inside a kitchen; eat dishes with eat.",
            Schema = Obj(new { }),
            Instant = true,
            Run = ListDishesSemantic
        },
        new()
        {
            Name = "cook_dish",
            Description = "Cook dishes from a cooking recipe you know, inside your farmhouse (or a cabin) with a kitchen; you walk to the stove yourself. " +
                          "recipe is the exact name list_dishes shows; quantity is how many times to cook it (default 1). Ingredients are taken from your pack " +
                          "and from the fridge there. Dishes restore energy and health: use eat.",
            Schema = Obj(new
            {
                recipe = Str("Exact dish name as list_dishes shows it, e.g. \"Fried Egg\""),
                quantity = Int("How many times to cook it (default 1)"),
                reason = Str(ReasonDesc)
            }, "recipe", "reason"),
            Run = CookDishSemantic
        },
        // ------------------------------------------------------------- phase 2: geodes
        new()
        {
            Name = "list_geodes",
            Description = "The geodes you carry (Geode, Frozen Geode, Magma Geode, Omni Geode, Artifact Trove, Golden Coconut) and how many of each. " +
                          "Game time stays frozen; pure observation. Clint cracks them open for 25g each at the Blacksmith: travel_to Blacksmith first " +
                          "(check his opening hours), then crack_geodes.",
            Schema = Obj(new { }),
            Instant = true,
            Run = ListGeodesSemantic
        },
        new()
        {
            Name = "crack_geodes",
            Description = "Have Clint crack open geodes you carry, 25g each. You must be inside the Blacksmith while Clint is there. " +
                          "item is the geode name as list_geodes shows it; quantity is how many (default 1). The result lists what came out. " +
                          "Artifacts, minerals and gems belong in the Museum (Gunther), one of each kind, before you sell them.",
            Schema = Obj(new
            {
                item = Str("Geode name as list_geodes shows it, e.g. \"Omni Geode\""),
                quantity = Int("How many to crack (default 1)"),
                reason = Str(ReasonDesc)
            }, "item", "reason"),
            Run = CrackGeodesSemantic
        },
        // ------------------------------------------------------------- phase 2: animals
        new()
        {
            Name = "list_animals_for_sale",
            Description = "What Marnie sells right now: each animal's price, the building it needs (Coop or Barn level) and whether you own one with room. " +
                          "Game time stays frozen; pure observation. Buy with purchase_animal, inside her shop in the Forest while she is there (about 9:00-16:00).",
            Schema = Obj(new { }),
            Instant = true,
            Run = ListAnimalsForSaleSemantic
        },
        new()
        {
            Name = "purchase_animal",
            Description = "Buy a farm animal from Marnie: pays from your own wallet and puts a baby animal into a finished Coop or Barn of yours. " +
                          "You must be inside her shop while she is there (about 9:00-16:00). type is the animal as list_animals_for_sale shows it " +
                          "(Chicken, Cow, Duck, Goat...); building picks which coop or barn (default: the first one with room); name is optional. " +
                          "Robin builds coops and barns (list_buildings / construct_building).",
            Schema = Obj(new
            {
                type = Str("Animal as list_animals_for_sale shows it, e.g. Chicken, Cow, Duck, Goat"),
                building = Str("Optional: which building, by name (\"Coop\") or tile \"x,y\" as list_animals/look show it; default: the first one with room"),
                name = Str("Optional name for the animal; default a random name"),
                reason = Str(ReasonDesc)
            }, "type", "reason"),
            Run = PurchaseAnimalSemantic
        },
        new()
        {
            Name = "list_animals",
            Description = "Your farm animals: where each lives, whether it is petted today, whether a product is ready, and the hay in your silo. " +
                          "Game time stays frozen; pure observation. Pet them with pet_animals (every day), gather products with " +
                          "collect_animal_products, and fill troughs with feed_animals.",
            Schema = Obj(new { }),
            Instant = true,
            Run = ListAnimalsSemantic
        },
        new()
        {
            Name = "pet_animals",
            Description = "Walk to each animal where you are (on the Farm, or inside a coop or barn: travel_to its name) and pet it, as a player would. " +
                          "Pet every animal daily; it keeps them happy and yields better products. name limits the walk to one animal (its name or type).",
            Schema = Obj(new
            {
                name = Str("Optional: only this animal (its name or type)"),
                reason = Str(ReasonDesc)
            }, "reason"),
            Run = PetAnimalsSemantic
        },
        new()
        {
            Name = "collect_animal_products",
            Description = "Pick up eggs and other products from the floor of the coop or barn you are in, and milk cows and goats (Milk Pail) or shear " +
                          "sheep and rabbits (Shears) when they have a product ready. Stand in the building (travel_to its name) or on the Farm.",
            Schema = Obj(new
            {
                name = Str("Optional: only this animal (its name or type) for tool collection; floor products are still collected"),
                reason = Str(ReasonDesc)
            }, "reason"),
            Run = CollectAnimalProductsSemantic
        },
        new()
        {
            Name = "feed_animals",
            Description = "Fill the troughs of the coop or barn you are in from your silo's hay, so the animals inside can eat (needs a Silo with hay; " +
                          "Marnie sells hay). Animals that are outside eat grass instead and need no hay.",
            Schema = Obj(new { reason = Str(ReasonDesc) }, "reason"),
            Run = FeedAnimalsSemantic
        },
        // ------------------------------------------------------------- phase 2: buildings
        new()
        {
            Name = "list_buildings",
            Description = "What Robin builds and upgrades, with gold and material costs, build days, whether you can afford each, and which of your " +
                          "buildings can take each upgrade (Big Coop, Deluxe Coop, Big Barn, Deluxe Barn, Big Shed...). Game time stays frozen; pure " +
                          "observation. For a new building, suggest_building_site then construct_building; for an upgrade, upgrade_building. " +
                          "You must be inside her shop (about 9:00-17:00) for the actions; house upgrades use upgrade_house.",
            Schema = Obj(new { }),
            Instant = true,
            Run = ListBuildingsSemantic
        },
        new()
        {
            Name = "suggest_building_site",
            Description = "Suggest free spots on your Farm for a new building's footprint. building is the name as list_buildings shows it (Coop, Barn, " +
                          "Silo, Well, Shed...). Pure read: nothing is paid, placed or reserved. Then order it for real with construct_building at the " +
                          "x,y (top-left tile of the footprint) you choose here.",
            Schema = Obj(new { building = Str("Building name as list_buildings shows it, e.g. Coop, Barn, Silo, Well, Shed") }, "building"),
            Instant = true,
            Run = SuggestBuildingSiteSemantic
        },
        new()
        {
            Name = "construct_building",
            Description = "Order a new farm building from Robin (the carpenter, ScienceHouse in the Mountain): pays the gold and materials from your own " +
                          "wallet and inventory and starts construction at x,y, the TOP-LEFT tile of the footprint on the Farm (suggest_building_site " +
                          "shows free spots). You must be inside her shop while it is open (about 9:00-17:00) and she is not busy; she works overnight, " +
                          "one project at a time, and takes festival days off.",
            Schema = Obj(new
            {
                building = Str("Building name as list_buildings shows it, e.g. Coop, Barn, Silo, Well, Shed"),
                x = Int("Top-left tile x of the footprint on the Farm"),
                y = Int("Top-left tile y of the footprint on the Farm"),
                reason = Str(ReasonDesc)
            }, "building", "x", "y", "reason"),
            Run = ConstructBuildingSemantic
        },
        new()
        {
            Name = "upgrade_building",
            Description = "Order a building upgrade from Robin (a Coop becomes a Big Coop, a Big Coop a Deluxe Coop; same for Barn and Shed). " +
                          "building is the upgrade name as list_buildings shows it. With exactly one of your buildings able to take it, that one is " +
                          "upgraded; if several qualify, give x,y of any tile of the one to upgrade. Paid from your own money and inventory; " +
                          "Robin works overnight and the animals stay inside while it is upgraded. You must be inside her shop while it is open.",
            Schema = Obj(new
            {
                building = Str("Upgrade name as list_buildings shows it, e.g. Big Coop, Deluxe Barn, Big Shed"),
                x = Int("Optional: x of any tile of the building to upgrade (needed only when several of yours qualify)"),
                y = Int("Optional: y of any tile of the building to upgrade"),
                reason = Str(ReasonDesc)
            }, "building", "reason"),
            Run = UpgradeBuildingSemantic
        },

        // ------------------------------------------------------------- phase 3: containers
        new()
        {
            Name = "list_chest",
            Description = "List what the open chest or fridge holds (slots used and each stack with its quality). item/items and quality optionally filter the listing. " +
                          "Game time stays frozen; pure observation. Open a container with open_container(x, y) or interact; find chests farm-wide with list_containers; " +
                          "move items with store_items / take_items.",
            Schema = Obj(new
            {
                item = Str("Optional: only this item name"),
                items = new { type = "array", items = new { type = "string" }, description = "Optional: only these item names" },
                quality = new { type = "string", @enum = new[] { "normal", "silver", "gold", "iridium" }, description = "Optional: only this quality" }
            }),
            Instant = true,
            Run = ListChestSemantic
        },
        new()
        {
            Name = "list_containers",
            Description = "Every chest and fridge on the farm and what it holds, from the farm's container ledger. No walking; game time stays frozen; pure observation. " +
                          "To work with one container, stand by it and call open_container(x, y), then list_chest / store_items / take_items.",
            Schema = Obj(new { }),
            Instant = true,
            Run = ListContainersSemantic
        },
        new()
        {
            Name = "store_items",
            Description = "Put items from your pack into a storage chest or the fridge, like the take/put clicks of the open chest menu. " +
                          "item or items names what to store (\"everything\" for the whole pack except tools, seeds and cooked dishes); quality limits to one quality " +
                          "(normal, silver, gold, iridium); quantity caps how many of each named item. Give x, y to walk to the chest and open it yourself, " +
                          "or leave x, y out to use the chest menu that is open and leave it open. The result says what was stored and what did not fit.",
            Schema = Obj(new
            {
                x = Int("Optional: chest tile x; omit to use the open chest"),
                y = Int("Optional: chest tile y"),
                item = Str("Item name, or \"everything\""),
                items = new { type = "array", items = new { type = "string" }, description = "Several item names" },
                quality = new { type = "string", @enum = new[] { "normal", "silver", "gold", "iridium" }, description = "Only this quality (default: all)" },
                quantity = Int("How many of each named item (default: all)"),
                reason = Str(ReasonDesc)
            }, "reason"),
            Run = StoreItemsSemantic
        },
        new()
        {
            Name = "take_items",
            Description = "Take items out of a storage chest or the fridge into your pack. item or items names what to take (\"everything\" for all of it); " +
                          "quality limits to one quality (normal, silver, gold, iridium); quantity caps how many of each named item. " +
                          "Give x, y to walk to the chest and open it yourself, or leave x, y out to use the chest menu that is open and leave it open. " +
                          "The result says what was taken and what your inventory could not fit.",
            Schema = Obj(new
            {
                x = Int("Optional: chest tile x; omit to use the open chest"),
                y = Int("Optional: chest tile y"),
                item = Str("Item name, or \"everything\""),
                items = new { type = "array", items = new { type = "string" }, description = "Several item names" },
                quality = new { type = "string", @enum = new[] { "normal", "silver", "gold", "iridium" }, description = "Only this quality (default: all)" },
                quantity = Int("How many of each named item (default: all)"),
                reason = Str(ReasonDesc)
            }, "reason"),
            Run = TakeItemsSemantic
        },
        // ------------------------------------------------------------- phase 3: the open menu
        new()
        {
            Name = "read_menu",
            Description = "Show the open menu: what it holds and which actions work (chest contents, mine floors, option lists, number and name entry, " +
                          "its labelled buttons). Game time stays frozen; pure observation. Drive it with the named tools — store_items/take_items for chests, " +
                          "choose_mine_floor, choose_menu_option, set_menu_amount, confirm_menu — and keep menu() only for menus none of those covers.",
            Schema = Obj(new { }),
            Instant = true,
            Run = ReadMenuSemantic
        },
        new()
        {
            Name = "choose_mine_floor",
            Description = "Ride the mine elevator to a floor: floor is the number as read_menu lists it (0 is the mine entrance). " +
                          "The mine elevator menu must be open — press the elevator switch in the Mine (interact) first.",
            Schema = Obj(new
            {
                floor = Int("Mine floor number as read_menu lists it (0 = the mine entrance)"),
                reason = Str(ReasonDesc)
            }, "floor", "reason"),
            Run = ChooseMineFloorSemantic
        },
        new()
        {
            Name = "choose_menu_option",
            Description = "Pick one option from an open option list (jukebox songs and other choose-one menus): option is the label as read_menu lists it. " +
                          "The list is stepped with its own arrow button and confirmed with OK, as a player would.",
            Schema = Obj(new { option = Str("Option label as read_menu lists it"), reason = Str(ReasonDesc) }, "option", "reason"),
            Run = ChooseMenuOptionSemantic
        },
        new()
        {
            Name = "set_menu_amount",
            Description = "Type a number into an open number entry (money to send, amounts to buy) and press OK. value must be inside the range the menu " +
                          "shows (read_menu); when the entry has a price, value x price must be no more than your gold.",
            Schema = Obj(new { value = Int("The number to enter, within the range the menu shows"), reason = Str(ReasonDesc) }, "value", "reason"),
            Run = SetMenuAmountSemantic
        },
        new()
        {
            Name = "confirm_menu",
            Description = "Press the OK button of the open menu, confirming what it shows. For dialogue answers use respond(option); " +
                          "to dismiss a menu without confirming use close_menu.",
            Schema = Obj(new { reason = Str(ReasonDesc) }, "reason"),
            Run = ConfirmMenuSemantic
        },
        // ------------------------------------------------------------- phase 3: one-tile actions
        new()
        {
            Name = "open_container",
            Description = "Walk next to the tile and open the chest, fridge or other container there; it stays open for list_chest and " +
                          "store_items / take_items without x, y (close_menu closes it). Nothing is moved by opening it.",
            Schema = Obj(new
            {
                x = Int("Container tile x"),
                y = Int("Container tile y"),
                reason = Str(ReasonDesc)
            }, "x", "y", "reason"),
            Run = OpenContainerSemantic
        },
        new()
        {
            Name = "load_machine",
            Description = "Load a machine with what it needs: walk next to the tile holding item from your inventory and press the action button, " +
                          "the same path a player's hands take (ore into a Furnace, hops into a Keg...). item names what you hold, as your inventory shows it. " +
                          "Which machine takes what: machines(item=...) explains it before you load.",
            Schema = Obj(new
            {
                x = Int("Machine tile x"),
                y = Int("Machine tile y"),
                item = Str("Item from your inventory to hold while pressing, e.g. Copper Ore"),
                reason = Str(ReasonDesc)
            }, "x", "y", "item", "reason"),
            Run = LoadMachineSemantic
        },
        new()
        {
            Name = "use_held_item",
            Description = "Use an item from your inventory where you stand, as a player would: totems that warp, books you read, and other items " +
                          "with a use (use eat for food instead). item is consumed unless the game keeps it (secret notes stay in the pack). " +
                          "To load a machine or gift someone, use load_machine / give_gift instead.",
            Schema = Obj(new { item = Str("Item from your inventory to use where you stand"), reason = Str(ReasonDesc) }, "item", "reason"),
            Run = UseHeldItemSemantic
        },
        new()
        {
            Name = "read_sign",
            Description = "Walk next to a sign (or notice board) and read it: the text is reported and the sign's dialogue closed again. " +
                          "To read the open menu instead, use read_menu.",
            Schema = Obj(new
            {
                x = Int("Sign tile x"),
                y = Int("Sign tile y"),
                reason = Str(ReasonDesc)
            }, "x", "y", "reason"),
            Run = ReadSignSemantic
        },
        // ------------------------------------------------------------- phase 3: villagers
        new()
        {
            Name = "talk_to_villager",
            Description = "Find a villager, walk up to them and talk (no gift). Someone walking somewhere is followed; when they are on another map " +
                          "you travel there unless go=false, which just reports where they are. Talking counts once a day; villagers() shows who you have " +
                          "not talked to yet. To also give an item, use give_gift.",
            Schema = Obj(new
            {
                name = Str("Villager name, e.g. Pierre"),
                go = new { type = "boolean", description = "Travel to them when they are on another map (default true)" },
                reason = Str(ReasonDesc)
            }, "name", "reason"),
            Run = TalkToVillagerSemantic
        },
        new()
        {
            Name = "give_gift",
            Description = "Give a villager an item from your inventory as a gift: you find them, walk up and hand it over, and hear their reaction " +
                          "(villagers(item=...) or villagers(name=...) shows how they would like it first). Limits: 1 gift per day, 2 per week, " +
                          "except birthdays and spouses. item leaves your pack only when they accept it. When they are on another map you travel there " +
                          "unless go=false.",
            Schema = Obj(new
            {
                name = Str("Villager name, e.g. Abigail"),
                item = Str("Item name from your inventory to give"),
                go = new { type = "boolean", description = "Travel to them when they are on another map (default true)" },
                reason = Str(ReasonDesc)
            }, "name", "item", "reason"),
            Run = GiveGiftSemantic
        },
        // ------------------------------------------------------------- phase 4A: planting with an explicit scope
        new()
        {
            Name = "plant_item",
            Description = "Plant seeds (or apply fertilizer) from your inventory on empty tilled soil (=); you walk to each tile automatically. " +
                          "Choose EXACTLY ONE scope — listed tiles, an area, or a confirmed all_empty — and give nothing else: an empty or " +
                          "missing scope plants nothing, never the whole map by accident. till_area prepares soil; farm_summary lists the " +
                          "empty tilled soil with its bounding box.",
            Schema = Obj(new
            {
                item = Str("Seed/fertilizer name, e.g. \"Parsnip Seeds\""),
                tiles = Tiles("One scope: list of [x,y] tilled tiles, max 40"),
                area = new { type = "array", description = "One scope: rectangle [x, y, w, h], x,y = top-left tile; every empty tilled tile inside is planted",
                             items = new { type = "integer" }, minItems = 4, maxItems = 4 },
                all_empty = new { type = "boolean", description = "One scope: true = every empty tilled tile on the current map" },
                confirm = new { type = "boolean", description = "Required with all_empty=true: pass true to confirm planting the whole map's empty tilled soil" },
                reason = Str(ReasonDesc)
            }, "item", "reason"),
            Run = PlantItemSemantic
        },
        new()
        {
            Name = "dismiss_menu",
            Description = "Close the open menu or dialogue and nothing more: no quest is accepted and no item is taken. " +
                          "It refuses when closing would smuggle a decision — a letter whose quest the game would auto-accept or that still " +
                          "holds attachments, or a one-shot item menu still holding items — naming the tool that takes the decision first " +
                          "(accept_letter_quest, claim_menu_items). On the box that waits for a proposal's answer it withdraws the proposal. " +
                          "The legacy close_menu still accepts and claims before closing.",
            Schema = Obj(new { reason = Str(ReasonDesc) }, "reason"),
            Run = DismissMenuSemantic
        },
        new()
        {
            Name = "accept_letter_quest",
            Description = "Accept the quest or special order attached to the open letter and receive its attached items, exactly as the " +
                          "letter's own exit would — but as your explicit decision, not a side effect of closing. The letter stays open; " +
                          "dismiss_menu closes it once nothing is left to decide. A letter without quest or attachments says so.",
            Schema = Obj(new { reason = Str(ReasonDesc) }, "reason"),
            Run = AcceptLetterQuestSemantic
        },
        new()
        {
            Name = "claim_menu_items",
            Description = "Take the items a one-shot menu offers (fishing treasure, a gift, fish pond output) or a letter's attachments into " +
                          "your inventory: what fits is taken, what is left over is named; taking letter attachments does not accept its quest. " +
                          "The menu stays open; dismiss_menu closes it. Chests and the shipping bin are " +
                          "never claimed — their items stay put (use take_items). The legacy close_menu did this silently before closing.",
            Schema = Obj(new { reason = Str(ReasonDesc) }, "reason"),
            Run = ClaimMenuItemsSemantic
        },
        new()
        {
            Name = "mine_floor",
            Description = "Progress to the next elevator floor (the next multiple of 5) with resources first: collect loose items and valuable ore/gem nodes, fight only threats or an infested-floor requirement, mine plain stones to reveal the down ladder, then stop at that elevator floor. " +
                          "Use it on any mine floor; repeat for the next 5 levels when ready. Stops on low health or when the ladder cannot be revealed and says what was gathered and where you stopped.",
            Schema = Obj(new { reason = Str(ReasonDesc) }, "reason"),
            Run = MineFloorSemantic
        }
    };

    // Run bodies referenced above, in order: ListQuestsSemantic, ClaimQuestRewardSemantic (Agent/Semantic/QuestTools.cs),
    // ListMuseumDonationsSemantic, DonateMuseumSemantic, ClaimMuseumRewardsSemantic (Agent/Semantic/MuseumTools.cs),
    // ListSpecialOrdersSemantic, ActiveSpecialOrdersSemantic, AcceptSpecialOrderSemantic (Agent/Semantic/SpecialOrderTools.cs),
    // ListRecipesSemantic, CraftItemSemantic (Agent/Semantic/CraftingTools.cs), ListDishesSemantic, CookDishSemantic (Agent/Semantic/CookingTools.cs),
    // ListGeodesSemantic, CrackGeodesSemantic (Agent/Semantic/GeodeTools.cs),
    // ListAnimalsForSaleSemantic, PurchaseAnimalSemantic, ListAnimalsSemantic, PetAnimalsSemantic, CollectAnimalProductsSemantic,
    // FeedAnimalsSemantic (Agent/Semantic/AnimalTools.cs),
    // ListBuildingsSemantic, SuggestBuildingSiteSemantic, ConstructBuildingSemantic, UpgradeBuildingSemantic (Agent/Semantic/BuildingTools.cs),
    // phase 3: containers and menus —
    // ListChestSemantic, ListContainersSemantic, StoreItemsSemantic, TakeItemsSemantic (Agent/Semantic/ChestTools.cs),
    // OpenContainerSemantic, LoadMachineSemantic, UseHeldItemSemantic, ReadSignSemantic (Agent/Semantic/InteractTools.cs),
    // ReadMenuSemantic, ChooseMineFloorSemantic, ChooseMenuOptionSemantic, SetMenuAmountSemantic, ConfirmMenuSemantic
    // (Agent/Semantic/MenuTools.cs),
    // TalkToVillagerSemantic, GiveGiftSemantic (Agent/Semantic/SocialInteractionTools.cs),
    // phase 4A: PlantItemSemantic (Agent/Semantic/PlantTools.cs); DismissMenuSemantic, AcceptLetterQuestSemantic,
    // ClaimMenuItemsSemantic (Agent/Semantic/MenuCloseTools.cs).
    // phase 4B (sleep, travel_to_location, travel_to_shop) registers in AgentTools.SleepTravelToolDefs with the
    // Run bodies in Agent/Semantic/SleepTools.cs and Agent/Semantic/TravelTools.cs.
}
