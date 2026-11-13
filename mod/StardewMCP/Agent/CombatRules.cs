using System;

namespace StardewMCP.Agent;

public enum CombatStyle
{
    Melee,
    HitAndRun,
    Flying,
    Ranged
}

public static class CombatRules
{
    public static CombatStyle StyleFor(MonsterRules.Tactic tactic)
    {
        if (tactic.Explodes || tactic.Role.Contains("charger", StringComparison.OrdinalIgnoreCase)
            || tactic.Role.Contains("leaping", StringComparison.OrdinalIgnoreCase))
            return CombatStyle.HitAndRun;
        if (tactic.Ranged) return CombatStyle.Ranged;
        if (tactic.Flying) return CombatStyle.Flying;
        return CombatStyle.Melee;
    }

    public static float DesiredRange(CombatStyle style) => style switch
    {
        CombatStyle.HitAndRun => 2.5f,
        CombatStyle.Ranged => 2f,
        CombatStyle.Flying => 2f,
        _ => 1.75f
    };

    public static bool RetreatAfterSwing(CombatStyle style) => style is CombatStyle.HitAndRun or CombatStyle.Ranged;

    /// <summary>Support casters are removed before closer ordinary monsters.</summary>
    public static float TargetPriority(MonsterRules.Tactic tactic, float distance) =>
        distance - (tactic.Role.Contains("support", StringComparison.OrdinalIgnoreCase) ? 4f : 0f);

    /// <summary>Bresenham tiles between two tile centres, excluding both endpoints.</summary>
    public static bool LineClear(int x0, int y0, int x1, int y1, Func<int, int, bool> walkable)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int error = dx - dy;
        int x = x0, y = y0;
        while (x != x1 || y != y1)
        {
            int twice = error * 2;
            if (twice > -dy) { error -= dy; x += sx; }
            else if (twice < dx) { error += dx; y += sy; }
            if (x == x1 && y == y1) break;
            if (!walkable(x, y)) return false;
        }
        return true;
    }
}
