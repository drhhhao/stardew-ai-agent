namespace StardewMCP.Agent;

/// <summary>Which game knowledge the AIs may read straight from the game data (<see cref="ModConfig.GuideKnowledge"/>).</summary>
public static class Knowledge
{
    /// <summary>Full knowledge: what upstream play leaves to discovery (legendary fish, bundle preview, friendship recipe sources...).</summary>
    public static bool Omniscient { get; private set; }

    /// <summary>All gift tastes from the game data (GuideKnowledge implies RevealGiftTastes).</summary>
    public static bool GiftTastes { get; private set; }

    /// <summary>
    /// Read once at startup, like the guide tool, the tool descriptions and the prompt: toggling the option in the config
    /// menu takes effect after a restart, so the gates never disagree with what the AI was told.
    /// </summary>
    public static void Init(ModConfig config)
    {
        Omniscient = config.GuideKnowledge;
        GiftTastes = config.GuideKnowledge || config.RevealGiftTastes;
    }
}
