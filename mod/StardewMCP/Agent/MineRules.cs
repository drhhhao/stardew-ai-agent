namespace StardewMCP.Agent;

public static class MineRules
{
    /// <summary>The next elevator floor after the current one. The regular Mines cap at 120.</summary>
    public static int NextElevatorFloor(int level)
    {
        if (level >= 120 && level < 121) return 120;
        return level - level % 5 + 5;
    }

    /// <summary>Ore, gem, coal and geode nodes take priority over plain stone.</summary>
    public static bool IsValuableNode(string label) => label != "Stone";
}
