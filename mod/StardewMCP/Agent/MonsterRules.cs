using System;

namespace StardewMCP.Agent;

/// <summary>
/// What each monster is and how a farmer should answer it. Game-free, so the unit tests can check the table and the
/// built-in prompt can name the same rules the combat tools use.
/// <para>Keys are matched against both class names (<c>RockCrab</c>) and display names (<c>Rock Crab</c>), with a
/// family fallback, so a modded variant that keeps its family still gets sensible advice.</para>
/// </summary>
public static class MonsterRules
{
    /// <summary>One monster's fighting style. <see cref="Role"/> is a short label for the AI; <see cref="Advice"/> is one line.</summary>
    public sealed record Tactic(
        string Name,
        string Role,
        string Advice,
        bool Pickaxe = false,
        bool Invulnerable = false,
        bool NeedsBomb = false,
        bool Flying = false,
        bool Ranged = false,
        bool Splits = false,
        bool Burrows = false,
        bool Submerged = false,
        bool Explodes = false,
        bool NonHostile = false);

    /// <summary>The rules for a class name, display name or query; never null.</summary>
    public static Tactic For(string name)
    {
        string k = Key(name);
        if (k.Length == 0) return Generic("Unknown monster");

        // Exact names first: the variants that do not contain their family word.
        switch (k)
        {
            case "dustspirit":
            case "dustsprite":
                return new("Dust Spirit", "fast melee", "Fast and weak: chase it down and swing; it dies in a hit or two.");
            case "grub":
                return new("Grub", "slow melee", "Slow: kill it quickly. A hit can make it turn into a Fly.");
            case "fly":
                return new("Fly", "fast flying", "Fast flyer: stand still and swing when it comes into reach.", Flying: true);
            case "duggy":
            case "magmaduggy":
                return new("Duggy", "burrower", "It burrows and cannot be hit underground. Wait for it to pop up, then hit it.", Burrows: true);
            case "rockcrab":
            case "trufflecrab":
            case "lavacrab":
            case "iridiumcrab":
            case "falsemagmacap":
                return Crab(name);
            case "ghost":
            case "carbonghost":
            case "putridghost":
                return Ghost(name);
            case "skeleton":
            case "skeletonwarrior":
                return new("Skeleton", "ranged melee", "It throws bones: close the distance and swing. Skeletons are tough, so eat before the fight.", Ranged: true);
            case "skeletonmage":
                return new("Skeleton Mage", "ranged", "It casts fire from a distance and has low HP: close in fast and hit it first.", Ranged: true);
            case "shadowguy":
            case "shadowbrute":
                return new("Shadow Brute", "melee", "Heavy melee hitter: fight it in the open, keep HP high and do not trade blows at low health.");
            case "shadowshaman":
                return new("Shadow Shaman", "support ranged", "It heals the other monsters: kill the Shaman first.", Ranged: true);
            case "shadowsniper":
            case "shooter":
                return new("Shadow Sniper", "ranged", "It shoots from a distance: close in behind cover and hit it hard.", Ranged: true);
            case "shadowgirl":
                return new("Shadow Girl", "melee", "Quick shadow melee: close in and swing; do not let it corner you.");
            case "squidkid":
            case "bluesquid":
                return new("Squid Kid", "ranged", "It shoots fireballs: close in fast; it has little health.", Ranged: true);
            case "serpent":
            case "royalserpent":
                return new("Serpent", "fast charger", "Fast charger with heavy damage: fight it in a corridor, keep HP high and swing as it passes.", Ranged: false);
            case "mummy":
                return new("Mummy", "reviving melee", "Your weapon knocks it down but it gets back up: finish it with a bomb (or a Crusader weapon) and do not stand over it.", NeedsBomb: true);
            case "pepperrex":
            case "dinomonster":
                return new("Pepper Rex", "ranged charger", "It breathes fire and charges: close in from the side, keep HP high and eat early.", Ranged: true);
            case "lavalurk":
                return new("Lava Lurk", "submerged turret", "It is invulnerable while submerged: wait for it to surface and shoot, then hit it.", Submerged: true, Ranged: true);
            case "metalhead":
                return new("Metal Head", "armored melee", "Heavy armor and slow: bring your strongest weapon or a bomb; a weak sword takes forever.");
            case "hothead":
                return new("Hot Head", "armored exploder", "Armored and it explodes when low: finish it from a distance and step away.", Explodes: true);
            case "magmasprite":
            case "magmasparker":
                return new("Magma Sprite", "fast ranged", "Fast fire caster: close in quickly and swing; do not stand in its line of fire.", Flying: true, Ranged: true);
            case "spiker":
                return new("Spiker", "invulnerable hazard", "It cannot be killed: walk around it and never stand on its tile.", Invulnerable: true);
            case "bug":
            case "armoredbug":
                return new("Bug", "melee", "A normal bug dies to any weapon. The armored kind is immune to weapons: use a bomb or Bug Killer.");
            case "dwarvishsentry":
                return new("Dwarvish Sentry", "stationary turret", "It is a fixed turret: close the distance and hit it; it cannot chase you.", Ranged: true);
            case "spider":
            case "leaper":
                return new("Spider", "leaping melee", "It leaps at you: keep moving and hit it when it lands.");
            case "fireball":
                return new("Fireball", "projectile", "A projectile, not a monster: do not chase it; move out of its line.", NonHostile: true);
            case "crow":
            case "frog":
            case "cat":
                return new("Harmless creature", "harmless", "Harmless: ignore it and get on with the mine.", NonHostile: true);
        }

        // Family fallbacks: a new colour or variant still gets the right idea.
        if (k.Contains("slime") || k.Contains("jelly"))
            return Slime(name);
        if (k.Contains("bat") || k.Contains("sparker"))
            return new("Bat", "flying", "Flying and fast: stand your ground and swing when it dips into reach; do not chase it around.", Flying: true);
        if (k.Contains("crab"))
            return Crab(name);
        if (k.Contains("golem"))
            return new("Golem", "slow melee", "Slow and tough: close in, dodge its ground slam, and keep swinging.");
        if (k.Contains("ghost"))
            return Ghost(name);
        if (k.Contains("skeleton"))
            return new("Skeleton", "ranged melee", "It throws bones: close the distance and swing; eat before a long fight.", Ranged: true);
        if (k.Contains("shadow"))
            return new("Shadow creature", "melee", "Close in and swing; Shadow Shamans heal the others, so kill them first.");
        if (k.Contains("serpent"))
            return new("Serpent", "fast charger", "Fast charger with heavy damage: fight it in a corridor and keep HP high.");
        if (k.Contains("duggy"))
            return new("Duggy", "burrower", "It burrows and cannot be hit underground. Wait for it to pop up, then hit it.", Burrows: true);
        if (k.Contains("squid"))
            return new("Squid", "ranged", "It shoots fireballs: close in fast and hit it before it fires again.", Ranged: true);
        if (k.Contains("lurk"))
            return new("Lava Lurk", "submerged turret", "It is invulnerable while submerged: wait for it to surface, then hit it.", Submerged: true, Ranged: true);
        if (k.Contains("head"))
            return new("Armored head", "armored melee", "Armored and slow: use your strongest weapon or a bomb.");
        return Generic(name);
    }

    /// <summary>The class-name variant used by the live game objects.</summary>
    public static Tactic ForClass(string className) => For(className);

    /// <summary>The display-name variant used by the codex and by the AI's questions.</summary>
    public static Tactic ForName(string displayName) => For(displayName);

    /// <summary>
    /// A live monster: its display name is localized, its class name is not. Prefer the display name when it knows the
    /// monster (a Shadow Sniper is a <c>Shooter</c>), and fall back to the class name for localised names and modded types.
    /// </summary>
    public static Tactic ForLive(string className, string displayName)
    {
        var byName = ForName(displayName);
        if (!byName.Advice.StartsWith("Close in and swing;", StringComparison.Ordinal)) return byName;
        return ForClass(className);
    }

    /// <summary>True when a weapon cannot do anything to this kind: an invulnerable hazard, or a monster that only a bomb finishes.</summary>
    public static bool NeedsSpecialTool(string name) => For(name) is { Invulnerable: true } or { NeedsBomb: true };

    private static Tactic Slime(string name)
    {
        string k = Key(name);
        if (k.Contains("big") || k.Contains("tiger"))
            return new("Big Slime", "melee", "Big or Tiger Slime: high HP and Big Slime splits into small slimes; keep swinging until the children are gone.", Splits: true);
        if (k.Contains("sludge"))
            return new("Sludge", "melee", "Tough slime with high HP: keep swinging and eat before it wears you down.");
        if (k.Contains("frost"))
            return new("Frost Jelly", "melee", "Frost slime: tougher than a green one; keep swinging and watch your HP.");
        return new("Green Slime", "melee", "Basic melee slime: walk up and swing; a Big Slime's children keep coming, so keep swinging.");
    }

    private static Tactic Crab(string name) => new(
        "Rock Crab", "shelled melee",
        "Its shell only cracks under the Pickaxe (within 1.5 tiles); a sword just clinks. Crack the shell, then switch to your weapon.",
        Pickaxe: true);

    private static Tactic Ghost(string name) => new(
        name.Contains("Putrid", StringComparison.OrdinalIgnoreCase) ? "Putrid Ghost" : "Ghost",
        "floating melee",
        "It floats through walls: your weapon works, but the Putrid Ghost's nausea makes a long fight dangerous. Keep moving and finish it fast.",
        Flying: true);

    private static Tactic Generic(string name) => new(
        name, "melee",
        "Close in and swing; eat when hurt and back away if the fight turns bad.");

    /// <summary>Letters and digits only, lower-case: lets the tool match a query against class and display names.</summary>
    public static string Normalize(string text) => Key(text);

    private static string Key(string text)
    {
        var chars = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
            if (char.IsLetterOrDigit(c)) chars.Append(char.ToLowerInvariant(c));
        return chars.ToString();
    }
}
