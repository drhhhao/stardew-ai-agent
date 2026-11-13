using System.Collections;
using System.Text.Json;
using StardewValley;
using StardewValley.Locations;

namespace StardewMCP.Agent;

// Semantic (intent-shaped) Museum tools (#13): one observation and one action per tool, no mode-switch
// "action" argument. The legacy "museum" tool in CommunityTools.cs keeps its three actions and stays in
// the MCP catalog; these are the versions the built-in AI catalog prefers. The ToolDef registrations
// are thin shims in top-level Agent/*.cs (Agent/SemanticTools.cs); this file only holds the
// implementations, reusing the legacy bodies (MuseumListText / MuseumDonate / MuseumRewards) so behavior
// cannot drift between the two catalogs. Each semantic body is wrapped in CommunityGuard here so a
// missing game member (LateBoundException) becomes a plain tool failure no matter which registration
// shim ran it.

public static partial class AgentTools
{
    /// <summary>list_museum_donations: what the Museum still accepts from your pack, and Gunther's pending rewards.</summary>
    internal static IEnumerator ListMuseumDonationsSemantic(JsonElement a, CallContext c) =>
        CommunityGuard(ListMuseumDonationsBody(a, c), c);

    private static IEnumerator ListMuseumDonationsBody(JsonElement a, CallContext c)
    {
        var museum = Game1.getLocationFromName("ArchaeologyHouse") as LibraryMuseum;
        if (museum == null) { c.Fail("The Museum (ArchaeologyHouse) is not available."); yield break; }
        c.Result = MuseumListText(museum);
    }

    /// <summary>donate_museum: the legacy donate action, unchanged.</summary>
    internal static IEnumerator DonateMuseumSemantic(JsonElement a, CallContext c) =>
        CommunityGuard(DonateMuseumBody(a, c), c);

    private static IEnumerator DonateMuseumBody(JsonElement a, CallContext c)
    {
        yield return MuseumDonate(a, c);
    }

    /// <summary>claim_museum_rewards: the legacy rewards action, unchanged.</summary>
    internal static IEnumerator ClaimMuseumRewardsSemantic(JsonElement a, CallContext c) =>
        CommunityGuard(ClaimMuseumRewardsBody(a, c), c);

    private static IEnumerator ClaimMuseumRewardsBody(JsonElement a, CallContext c)
    {
        yield return MuseumRewards(c);
    }
}
