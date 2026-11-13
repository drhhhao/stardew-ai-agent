using System;

namespace StardewMCP.Agent;

/// <summary>
/// The game-free numbers and decisions of <see cref="SelfDefense"/>, so the unit tests can check them
/// (mod/StardewMCP.Tests compiles this file: no game types here).
/// </summary>
public static class DefenseRules
{
    /// <summary>A monster this close (tiles) is hit while the farmer is thinking: the same melee range <c>fight</c> uses.</summary>
    public const float ReachTiles = 2f;

    /// <summary>
    /// A pickaxe swings at the tile in front, so a shelled rock crab (cracked with the pickaxe, never the sword) has to
    /// be this close. Using <see cref="ReachTiles"/> for it made the defensive swing miss.
    /// </summary>
    public const float PickaxeReachTiles = 1.5f;

    /// <summary>The reach of the item the defence picks for the monster: pickaxe for a shell, weapon otherwise.</summary>
    public static float ReachFor(bool needsPickaxe) => needsPickaxe ? PickaxeReachTiles : ReachTiles;

    /// <summary>A monster this close makes the farmer eat when hurt: it is on its way.</summary>
    public const float ThreatTiles = 8f;

    /// <summary>Update ticks between two swings (fight's own rhythm).</summary>
    public const int SwingTicks = 18;

    /// <summary>At most one line per this many seconds about the same fight.</summary>
    public const int NoteSeconds = 20;

    /// <summary>Lines kept for the AI's next tool result.</summary>
    public const int MaxNotes = 4;

    /// <summary>Eat once health is at or below this percent of the maximum; 0 turns eating off.</summary>
    public static int EatPercent(int configured) => Math.Clamp(configured, 0, 99);

    /// <summary>
    /// At or below this percent of the maximum health, with no healing food left, the farmer backs away instead of
    /// standing there: the only other way a fight it cannot win ends in the mine entrance.
    /// </summary>
    public const int FleePercent = 25;

    /// <summary>How far (tiles) a defensive retreat steps away from the monster.</summary>
    public const int FleeTiles = 5;

    /// <summary>Whether health is low enough to back away when there is nothing to eat.</summary>
    public static bool ShouldFlee(int health, int maxHealth) =>
        maxHealth > 0 && health * 100 <= maxHealth * FleePercent;

    /// <summary>Whether the health is now low enough to eat (percent against the maximum, as <c>fight</c> does).</summary>
    public static bool ShouldEat(int health, int maxHealth, int configuredPercent)
    {
        int percent = EatPercent(configuredPercent);
        return percent > 0 && maxHealth > 0 && health * 100 <= maxHealth * percent;
    }

    /// <summary>
    /// Whether the farmer defends itself at all: only an AI seat that is attached to something, in a running world,
    /// with no action of its own, no menu and no pause.
    /// </summary>
    public static bool ShouldDefend(bool enabled, bool isAiSeat, bool connected, bool worldReady, bool actionRunning,
        bool wakingUp, bool released, bool menuOpen, bool paused, bool holding, bool newDay, bool inBed, bool dead)
    {
        if (!enabled || !isAiSeat || !connected || !worldReady) return false;
        if (actionRunning || wakingUp || released || menuOpen || paused || holding || newDay) return false;
        return !inBed && !dead;
    }

    /// <summary>"3 swing(s)" / "" for the day's stats.</summary>
    public static string SwingText(int swings) => swings > 0 ? $"{swings} swing(s)" : "";

    /// <summary>"ate 2" / "" for the day's stats.</summary>
    public static string AteText(int eaten) => eaten > 0 ? $"ate {eaten}" : "";

    /// <summary>"backed away twice" / "" for the day's stats.</summary>
    public static string BackAwayText(int retreats) => retreats switch
    {
        <= 0 => "",
        1 => "backed away once",
        _ => $"backed away {retreats} times"
    };

    /// <summary>"3 swing(s), ate 1" for the day's stats; "" when it never had to defend itself.</summary>
    public static string Summary(int swings, int eaten, int retreats) =>
        string.Join(", ", Array.FindAll(new[] { SwingText(swings), AteText(eaten), BackAwayText(retreats) },
            p => p.Length > 0));
}
