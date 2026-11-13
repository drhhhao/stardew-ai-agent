using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewMCP.Agent;

/// <summary>A building on the Farm as the upgrade rules see it.</summary>
/// <param name="Type">Its building type id (<c>Building.buildingType</c>), e.g. "Coop", "Big Barn".</param>
/// <param name="X">Top-left tile x.</param>
/// <param name="Y">Top-left tile y.</param>
/// <param name="Width">Footprint width in tiles.</param>
/// <param name="Height">Footprint height in tiles.</param>
/// <param name="DaysBusy">Days of construction or upgrade left; 0 when finished.</param>
/// <param name="UpgradingTo">The type id Robin is upgrading it into (<c>Building.upgradeName</c> while <c>daysUntilUpgrade</c> > 0), else null.</param>
public sealed record FarmBuilding(string Type, int X, int Y, int Width, int Height, int DaysBusy = 0, string? UpgradingTo = null)
{
    public bool Finished => DaysBusy <= 0;
    public bool Upgrading => !Finished && !string.IsNullOrEmpty(UpgradingTo);
    public string At => $"({X},{Y})";
}

/// <summary>What <see cref="UpgradeRules.Choose"/> decided.</summary>
public enum UpgradeDecision
{
    /// <summary>Upgrade <see cref="UpgradeChoice.Target"/>.</summary>
    Upgrade,
    /// <summary>Several buildings can take it: the caller must name one with x,y.</summary>
    NeedTile,
    /// <summary>Nothing can be upgraded; <see cref="UpgradeChoice.Message"/> says why.</summary>
    Refuse
}

public sealed record UpgradeChoice(UpgradeDecision Decision, FarmBuilding? Target, string Message);

/// <summary>
/// Game-free rules for Robin's building upgrades (Coop to Big Coop, Barn to Big Barn, Shed to Big Shed...), as
/// CarpenterMenu does them in 1.6.15: an upgrade blueprint (<c>BuildingData.BuildingToUpgrade</c> set) applies to a
/// finished building whose type is exactly <c>BuildingToUpgrade</c>, picked by any tile of it (<c>getBuildingAt</c>).
/// </summary>
public static class UpgradeRules
{
    /// <summary>The finished buildings of type <paramref name="from"/>, the ones an upgrade from it can be ordered for.</summary>
    public static List<FarmBuilding> Eligible(IEnumerable<FarmBuilding> farm, string from) =>
        farm.Where(b => b.Finished && b.Type.Equals(from, StringComparison.OrdinalIgnoreCase)).OrderBy(b => b.Y).ThenBy(b => b.X).ToList();

    /// <summary>Whether the tile lies on the building's footprint.</summary>
    public static bool Covers(FarmBuilding b, int x, int y) => x >= b.X && x < b.X + b.Width && y >= b.Y && y < b.Y + b.Height;

    /// <summary>The building whose footprint holds the tile, or null.</summary>
    public static FarmBuilding? At(IEnumerable<FarmBuilding> farm, int x, int y) => farm.FirstOrDefault(b => Covers(b, x, y));

    /// <summary>
    /// Which building to upgrade to <paramref name="upgrade"/> (an upgrade of <paramref name="from"/>). Without a tile:
    /// the only eligible one, or a request for x,y when there are several. With a tile: <paramref name="atTile"/> is the
    /// building the game finds there (null for none), and it must be a finished <paramref name="from"/>.
    /// <paramref name="upgrade"/> is the shown name; <paramref name="upgradeId"/> (default: the same) is the type id compared
    /// with <see cref="FarmBuilding.Type"/>, and <paramref name="name"/> turns a type id into its shown name for the messages.
    /// </summary>
    public static UpgradeChoice Choose(IReadOnlyList<FarmBuilding> farm, string upgrade, string from, bool hasTile, int x, int y, FarmBuilding? atTile,
        string? upgradeId = null, Func<string, string>? name = null)
    {
        var n = new Names(upgrade, upgradeId ?? upgrade, from, name);
        var eligible = Eligible(farm, from);
        if (hasTile)
        {
            if (atTile == null)
                return Refuse($"There is no building at ({x},{y}) on the Farm. {Options(eligible, farm, n)}");
            if (atTile.Type.Equals(n.UpgradeId, StringComparison.OrdinalIgnoreCase))
                return Refuse($"The building at ({x},{y}) is already a {upgrade}. {Options(eligible, farm, n)}");
            if (!atTile.Type.Equals(from, StringComparison.OrdinalIgnoreCase))
                return Refuse($"The building at ({x},{y}) is a {n.Show(atTile.Type)} (top-left {atTile.At}); a {upgrade} is an upgrade of a {n.From} only. {Options(eligible, farm, n)}");
            if (atTile.Upgrading)
                return Refuse(UpgradingText(atTile, n));
            if (!atTile.Finished)
                return Refuse($"The {n.From} at {atTile.At} is still under construction ({atTile.DaysBusy} day(s) left); it can be upgraded once it is finished.");
            return new UpgradeChoice(UpgradeDecision.Upgrade, atTile, "");
        }

        if (eligible.Count == 1) return new UpgradeChoice(UpgradeDecision.Upgrade, eligible[0], "");
        if (eligible.Count > 1)
            return new UpgradeChoice(UpgradeDecision.NeedTile, null,
                $"You have {eligible.Count} {n.From}s that can become a {upgrade}: {Positions(eligible)}. " +
                $"Say which one: build(building=\"{upgrade}\", x, y) with any tile of it, e.g. x={eligible[0].X}, y={eligible[0].Y}.");
        return Refuse(Options(eligible, farm, n));
    }

    /// <summary>The "which of your buildings can take it" part of the list line; <paramref name="name"/> shows a type id.</summary>
    public static string Takers(IEnumerable<FarmBuilding> farm, string from, Func<string, string>? name = null)
    {
        var all = farm.ToList();
        string Show(string id) => name?.Invoke(id) ?? id;
        var eligible = Eligible(all, from);
        if (eligible.Count > 0) return $"your {Show(from)} at {Positions(eligible)} can take it";
        var building = all.Where(b => b.Type.Equals(from, StringComparison.OrdinalIgnoreCase)).ToList();
        var upgrading = building.Where(b => b.Upgrading).ToList();
        if (upgrading.Count > 0) return $"your {Show(from)} at {Positions(upgrading)} is already being upgraded into a {Show(upgrading[0].UpgradingTo!)}";
        if (building.Count > 0) return $"your {Show(from)} at {Positions(building)} is not finished yet";
        return $"needs a finished {Show(from)} first (you have none)";
    }

    /// <summary>
    /// Whether a requested building name is an upgrade, from the names last read from the game's data (<paramref name="known"/>,
    /// may be empty) or, before that, from the vanilla naming ("Big ...", "Deluxe ..."). Used where the game can't be read.
    /// </summary>
    public static bool LooksLikeUpgrade(string? name, IReadOnlyCollection<string> known)
    {
        var n = (name ?? "").Trim();
        if (n.Length == 0) return false;
        if (known.Any(k => k.Equals(n, StringComparison.OrdinalIgnoreCase))) return true;
        return n.StartsWith("Big ", StringComparison.OrdinalIgnoreCase) || n.StartsWith("Deluxe ", StringComparison.OrdinalIgnoreCase);
    }

    private static string Positions(IEnumerable<FarmBuilding> list) => string.Join(", ", list.Select(b => b.At));

    /// <summary>The shown names next to the type ids they stand for.</summary>
    private sealed record Names(string Upgrade, string UpgradeId, string FromId, Func<string, string>? Name)
    {
        public string Show(string typeId) => Name?.Invoke(typeId) ?? typeId;
        public string From => Show(FromId);
    }

    private static string UpgradingText(FarmBuilding b, Names n) =>
        $"Your {n.Show(b.Type)} at {b.At} is already being upgraded into a {n.Show(b.UpgradingTo!)} ({b.DaysBusy} day(s) left); nothing to order.";

    private static string Options(List<FarmBuilding> eligible, IReadOnlyList<FarmBuilding> farm, Names n)
    {
        if (eligible.Count > 0)
            return $"Your {n.From}s that can become a {n.Upgrade}: {Positions(eligible)} (x,y may be any tile of the building).";
        var unfinished = farm.Where(b => !b.Finished && b.Type.Equals(n.FromId, StringComparison.OrdinalIgnoreCase)).ToList();
        if (unfinished.FirstOrDefault(b => b.Upgrading) is { } upgrading)
            return UpgradingText(upgrading, n);
        if (unfinished.Count > 0)
            return $"Your {n.From} at {Positions(unfinished)} is still under construction ({unfinished[0].DaysBusy} day(s) left); upgrade it once it is finished.";
        var already = farm.Where(b => b.Type.Equals(n.UpgradeId, StringComparison.OrdinalIgnoreCase)).ToList();
        var have = already.Count > 0 ? $" (your {n.Upgrade} at {Positions(already)} already is one)" : "";
        return $"A {n.Upgrade} is an upgrade of a {n.From}, and you have no finished {n.From} on the Farm{have}. Build a {n.From} first, or pick another upgrade in build(building=list).";
    }

    private static UpgradeChoice Refuse(string message) => new(UpgradeDecision.Refuse, null, message);
}
