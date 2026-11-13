using Xunit;
using StardewMCP.Agent;

namespace StardewMCP.Tests;

/// <summary>
/// The monster codex behind the monsters tool and the fight/self-defense exceptions: class names, display names and
/// the few monsters a weapon cannot answer alone.
/// </summary>
public class MonsterRulesTests
{
    [Fact]
    public void ACrabNeedsThePickaxeForItsShell()
    {
        Assert.True(MonsterRules.ForClass("RockCrab").Pickaxe);
        Assert.True(MonsterRules.ForName("Truffle Crab").Pickaxe);
        Assert.True(MonsterRules.ForName("False Magma Cap").Pickaxe);
        Assert.Contains("Pickaxe", MonsterRules.ForClass("RockCrab").Advice);
    }

    [Fact]
    public void TheSpikerCannotBeKilled()
    {
        var spiker = MonsterRules.ForName("Spiker");
        Assert.True(spiker.Invulnerable);
        Assert.Contains("cannot be killed", spiker.Advice);
        Assert.True(MonsterRules.NeedsSpecialTool("Spiker"));
    }

    [Fact]
    public void AMummyNeedsABombOrCrusader()
    {
        var mummy = MonsterRules.ForName("Mummy");
        Assert.True(mummy.NeedsBomb);
        Assert.Contains("bomb", mummy.Advice);
        Assert.True(MonsterRules.NeedsSpecialTool("Mummy"));
    }

    [Fact]
    public void ShadowShamansAreSupportCasters()
    {
        var shaman = MonsterRules.ForName("Shadow Shaman");
        Assert.Contains("support", shaman.Role);
        Assert.Contains("Shaman first", shaman.Advice);
    }

    [Fact]
    public void SerpentsAreFastChargers()
    {
        var serpent = MonsterRules.ForName("Serpent");
        Assert.Contains("charger", serpent.Role);
        Assert.Contains("corridor", serpent.Advice);
        Assert.Equal(serpent.Role, MonsterRules.ForName("Royal Serpent").Role);
    }

    [Fact]
    public void FamiliesCoverVariantsAndLocalisedClassNames()
    {
        // The class names are English even when the display name is localised.
        Assert.Contains("slime", MonsterRules.ForClass("GreenSlime").Advice, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("slime", MonsterRules.ForClass("BigSlime").Advice, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("flying", MonsterRules.ForClass("FrostBat").Role);
        Assert.Contains("burrow", MonsterRules.ForClass("Duggy").Advice, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ranged", MonsterRules.ForClass("Shooter").Role);
    }

    [Fact]
    public void AShooterUsesItsDisplayNameWhenTheClassNameIsGeneric()
    {
        // Shadow Sniper is a Shooter class; the display name carries the specific rule.
        var sniper = MonsterRules.ForLive("Shooter", "Shadow Sniper");
        Assert.Contains("ranged", sniper.Role);
    }

    [Fact]
    public void UnknownMonstersGetTheSafeFallback()
    {
        var unknown = MonsterRules.ForName("Modded Thing");
        Assert.Contains("swing", unknown.Advice, System.StringComparison.OrdinalIgnoreCase);
        Assert.False(unknown.Invulnerable);
        Assert.False(unknown.NeedsBomb);
    }
}
