using Xunit;
using StardewMCP.Agent;

namespace StardewMCP.Tests;

/// <summary>
/// Self-defense while an AI thinks (Agent/SelfDefense.cs): the rules that decide when the farmer answers a
/// monster by itself, and when it eats. The game side is checked in game.
/// </summary>
public class DefenseRulesTests
{
    /// <summary>An AI seat, in a running world, between actions: nothing else is in the way.</summary>
    private static bool Defends(
        bool enabled = true, bool isAiSeat = true, bool connected = true, bool worldReady = true,
        bool actionRunning = false, bool wakingUp = false, bool released = false, bool menuOpen = false,
        bool paused = false, bool holding = false, bool newDay = false, bool inBed = false, bool dead = false) =>
        DefenseRules.ShouldDefend(enabled, isAiSeat, connected, worldReady, actionRunning, wakingUp, released,
            menuOpen, paused, holding, newDay, inBed, dead);

    [Fact]
    public void DefendsBetweenActions()
    {
        Assert.True(Defends());
    }

    [Fact]
    public void DoesNothingWhenTurnedOff()
    {
        Assert.False(Defends(enabled: false));
    }

    [Fact]
    public void OnlyAiSeatsDefend()
    {
        // A human at the keyboard and the camera seat answer for themselves.
        Assert.False(Defends(isAiSeat: false));
    }

    [Fact]
    public void NobodyAttachedMeansNothingToDefendFor()
    {
        // A seat no AI has talked to must not start swinging on its own.
        Assert.False(Defends(connected: false));
    }

    [Fact]
    public void ItsOwnActionHasPriority()
    {
        // fight, walk_to and the rest drive the farmer; the defense must not swing over them.
        Assert.False(Defends(actionRunning: true));
    }

    [Fact]
    public void StaysStillInMenusDuringTransitionsAndWhenPaused()
    {
        Assert.False(Defends(menuOpen: true));
        Assert.False(Defends(newDay: true));
        Assert.False(Defends(paused: true));
        Assert.False(Defends(holding: true));
        Assert.False(Defends(worldReady: false));
    }

    [Fact]
    public void StaysStillInBedWhileWakingAndWhenReleased()
    {
        Assert.False(Defends(inBed: true));
        Assert.False(Defends(wakingUp: true));
        Assert.False(Defends(released: true));
        Assert.False(Defends(dead: true));
    }

    [Fact]
    public void EatsAtOrBelowTheConfiguredPercentOfMaxHealth()
    {
        Assert.True(DefenseRules.ShouldEat(40, 100, 40));
        Assert.True(DefenseRules.ShouldEat(34, 100, 40));
        Assert.False(DefenseRules.ShouldEat(41, 100, 40));

        // The percentage is of the maximum, not a fixed number: 40% of 125 HP is 50.
        Assert.True(DefenseRules.ShouldEat(50, 125, 40));
        Assert.False(DefenseRules.ShouldEat(51, 125, 40));
    }

    [Fact]
    public void EatingCanBeTurnedOff()
    {
        Assert.False(DefenseRules.ShouldEat(1, 100, 0));
    }

    [Fact]
    public void EatPercentIsClampedToSensibleValues()
    {
        Assert.Equal(0, DefenseRules.EatPercent(-5));
        Assert.Equal(99, DefenseRules.EatPercent(100));
        Assert.Equal(40, DefenseRules.EatPercent(40));
    }

    [Fact]
    public void TheReachIsTheMeleeRange()
    {
        // fight swings from 2 tiles; the defense must use the same range so it never reaches farther than a player.
        Assert.Equal(2f, DefenseRules.ReachTiles);
        Assert.True(DefenseRules.ThreatTiles > DefenseRules.ReachTiles);
    }

    [Fact]
    public void APickaxeSwingsOneTileFartherIn()
    {
        // A shelled rock crab is cracked with the pickaxe, which only reaches the tile in front: the sword's 2 tiles
        // made every defensive swing at it miss.
        Assert.Equal(2f, DefenseRules.ReachFor(needsPickaxe: false));
        Assert.Equal(1.5f, DefenseRules.ReachFor(needsPickaxe: true));
        Assert.True(DefenseRules.PickaxeReachTiles < DefenseRules.ReachTiles);
    }

    [Fact]
    public void BacksAwayOnlyWhenNearlyOutOfHealth()
    {
        Assert.True(DefenseRules.ShouldFlee(25, 100));
        Assert.True(DefenseRules.ShouldFlee(10, 100));
        Assert.False(DefenseRules.ShouldFlee(26, 100));

        // Percent of the maximum: 25% of 125 HP is 31.
        Assert.True(DefenseRules.ShouldFlee(31, 125));
        Assert.False(DefenseRules.ShouldFlee(32, 125));

        // It only makes sense while there is health to lose.
        Assert.False(DefenseRules.ShouldFlee(0, 0));
    }

    [Fact]
    public void SummaryCountsWhatHappened()
    {
        Assert.Equal("", DefenseRules.Summary(0, 0, 0));
        Assert.Equal("3 swing(s)", DefenseRules.Summary(3, 0, 0));
        Assert.Equal("ate 2", DefenseRules.Summary(0, 2, 0));
        Assert.Equal("3 swing(s), ate 1", DefenseRules.Summary(3, 1, 0));
        Assert.Equal("backed away once", DefenseRules.Summary(0, 0, 1));
        Assert.Equal("2 swing(s), ate 1, backed away 3 times", DefenseRules.Summary(2, 1, 3));
    }
}
