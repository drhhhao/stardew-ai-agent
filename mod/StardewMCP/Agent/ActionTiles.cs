using System;
using StardewValley;

namespace StardewMCP.Agent;

/// <summary>Readable names for map tile actions (the "Action" tile property) that <c>look</c> shows on A tiles.</summary>
public static class ActionTiles
{
    /// <summary>
    /// The label for a tile action string such as "BuyBackpack". Actions without a friendlier name keep their
    /// raw string, which is what the AI and runner/smoke.py already match on (for example "MineElevator").
    /// </summary>
    public static string Label(string action)
    {
        var name = action.Split(' ')[0];
        switch (name)
        {
            // Pierre's backpack is a tile action that opens the Backpack_Purchase question, not a shop item.
            case "BuyBackpack":
                return Game1.player.MaxItems switch
                {
                    < 24 => "Backpack upgrade to 24 slots (2000g; interact, then respond)",
                    < 36 => "Backpack upgrade to 36 slots (10000g; interact, then respond)",
                    _ => "Backpack (already 36 slots)"
                };
            case "Message":
                return SignLabel(action);
            default:
                return action;
        }
    }

    /// <summary>
    /// "Message Town_SeedShopSign" on a sign: resolve the string table so look shows the hours instead of an internal
    /// key. Falling back to "Sign (interact to read)" keeps a miss harmless.
    /// </summary>
    private static string SignLabel(string action)
    {
        string key = action.Length > "Message".Length ? action["Message".Length..].Trim().Trim('"') : "";
        if (key.Length == 0) return "Sign (interact to read)";
        string text = Resolve("Strings\\Locations:" + key) ?? Resolve("Strings\\StringsFromMaps:" + key) ?? "";
        text = text.Replace("\n", " ").Replace("^", " ").Trim();
        if (text.Length == 0)
        {
            // A literal message in the map (not a key) is still worth showing; an unresolved key is not.
            if (key.Contains(' ') || key.Contains('\'')) return "Sign: " + key;
            return "Sign (interact to read)";
        }
        if (text.Length > 140) text = text[..137] + "...";
        return "Sign: " + text;
    }

    /// <summary>The sign beside Pierre's shop is a bare tile index (620), handled by <c>Town.checkAction</c>, not an Action property.</summary>
    public static string SeedShopSign()
    {
        string text = Resolve("Strings\\Locations:Town_SeedShopSign") ?? "Pierre's General Store sign";
        text = text.Replace("\n", " ").Replace("^", " ").Trim();
        return text.Length > 140 ? text[..137] + "..." : text;
    }

    private static string? Resolve(string key)
    {
        try
        {
            string text = Game1.content.LoadString(key);
            return text == key ? null : text;
        }
        catch { return null; }
    }
}
