using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;

namespace StardewMCP.Agent;

/// <summary>Which open Community Center bundle slots still want an item (for item_info, fish_info and other lookups).</summary>
public static class BundleNeeds
{
    /// <summary>
    /// One line per open bundle slot (not donated yet, in an unfinished bundle) that takes this item, e.g.
    /// "Quality Crops (Pantry): 5, gold or better (any 3 of 4 still open)". Empty when none, on the Joja route, and before the
    /// Community Center opens unless GuideKnowledge previews it.
    /// </summary>
    public static IReadOnlyList<string> For(string qualifiedItemId)
    {
        var who = Game1.player;
        if (who == null || Game1.netWorldState?.Value == null || string.IsNullOrEmpty(qualifiedItemId)) return Array.Empty<string>();
        if (!BundleReader.NeedsVisible(who)) return Array.Empty<string>();
        var data = ItemRegistry.GetData(qualifiedItemId);
        if (data == null) return Array.Empty<string>();
        return BundleReader.OpenItemSlots(BundleReader.Read(), who)
            .Where(x => BundlePlan.Accepts(x.need, data.QualifiedItemId, data.Category))
            .Select(x => BundlePlan.SlotText(x.bundle, x.need))
            .ToList();
    }
}
