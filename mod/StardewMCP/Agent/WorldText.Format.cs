using System.Text.RegularExpressions;

namespace StardewMCP.Agent;

// The part of WorldText that needs no game types, moved here unchanged so the unit tests
// (mod/StardewMCP.Tests) can compile it.
public static partial class WorldText
{
    /// <summary>Game clock (e.g. 1350, or 2600 for 2 AM) as "13:50".</summary>
    public static string FormatTime(int t)
    {
        int h = t / 100, m = t % 100;
        return $"{h:00}:{m:00}";
    }

    public static string DirName(int d) => d switch { 0 => "up", 1 => "right", 2 => "down", 3 => "left", _ => "?" };

    /// <summary>
    /// A mine level as the player sees it: floors 1-120 in the Mines, and floor 1+ in the Skull Cavern where
    /// the game's level 121 is the first Skull Cavern floor (#55).
    /// </summary>
    public static string MineFloorName(int level) =>
        level >= 121 ? $"Skull Cavern floor {level - 120}" : level == 0 ? "Mine entrance" : $"floor {level}";

    /// <summary>
    /// What a breakable stone (Name "Stone", category -999) gives, from its item id: the ids in
    /// GameLocation.breakStone (1.6). Most of them are all called "Stone" in the game data, so a farmer
    /// could not tell a copper node from a plain rock (and the game picks the gem of a Gem Node when it breaks).
    /// </summary>
    public static string StoneLabel(string? itemId) => itemId switch
    {
        "751" or "849" => "Copper Node",
        "290" or "850" => "Iron Node",
        "764" or "VolcanoGoldNode" => "Gold Node",
        "765" => "Iridium Node",
        "BasicCoalNode0" or "BasicCoalNode1" or "VolcanoCoalNode0" or "VolcanoCoalNode1" => "Coal Node",
        "44" => "Gem Node",
        "2" => "Diamond Node",
        "4" => "Ruby Node",
        "6" => "Jade Node",
        "8" => "Amethyst Node",
        "10" => "Topaz Node",
        "12" => "Emerald Node",
        "14" => "Aquamarine Node",
        "46" => "Mystic Stone",
        "75" => "Geode Stone",
        "76" => "Frozen Geode Stone",
        "77" => "Magma Geode Stone",
        "819" => "Omni Geode Stone",
        "95" => "Radioactive Node",
        "843" or "844" => "Cinder Shard Node",
        "25" => "Mussel Stone",
        "816" or "817" => "Fossil Stone",
        "818" => "Clay Stone",
        "CalicoEggStone_0" or "CalicoEggStone_1" or "CalicoEggStone_2" => "Calico Egg Stone",
        _ => "Stone"
    };

    /// <summary>
    /// Time until a machine is done, from its MinutesUntilReady: "40m left", "2h 10m left", or whole days
    /// for long jobs (a game day is about 1600 machine minutes, the game's own count from 6 AM to the next
    /// 6 AM with the night). Empty when there is nothing to wait for or the value is a placeholder
    /// (a cask holds 999999 while it ages by quality instead of by time).
    /// </summary>
    public static string TimeLeft(int minutes)
    {
        if (minutes <= 0 || minutes >= 999999) return "";
        if (minutes < 60) return $"{minutes}m left";
        if (minutes < 1600)
        {
            int h = minutes / 60, m = minutes % 60;
            return m == 0 ? $"{h}h left" : $"{h}h {m}m left";
        }
        int days = (minutes + 1599) / 1600;
        return days == 1 ? "about 1 day left" : $"about {days} days left";
    }

    // The ", 2h 10m left" that HeldState puts before the ")" of "(holding X, ...)": every TimeLeft form.
    private static readonly Regex TimeLeftPart = new(@", (\d+h \d+m|\d+h|\d+m|about \d+ days?) left(?=\))", RegexOptions.CultureInvariant);

    /// <summary>
    /// A tile label split into its stable part and the machine's time left: "Keg (holding Wine, 2h left)" gives
    /// ("Keg (holding Wine)", "2h left"). The time ticks down every 10 game minutes, so look(diff), the look list
    /// grouping and the harvest/interact "did the tile change" checks compare the stable part only.
    /// </summary>
    public static (string stable, string left) SplitTimeLeft(string label)
    {
        var m = TimeLeftPart.Match(label);
        return m.Success ? (label.Remove(m.Index, m.Length), m.Value.Substring(2)) : (label, "");
    }

    /// <summary>The label without the machine's time left (null stays null).</summary>
    public static string? StableLabel(string? label) => label == null ? null : SplitTimeLeft(label).stable;
}
