using System.Collections;
using System.Linq;
using System.Text.Json;

namespace StardewMCP.Agent;

// Semantic (intent-shaped) planting (phase 4A of docs/tool-decoupling/README.md): plant_item replaces the legacy
// plant tool's three modes with one explicit scope per call — tiles, an area, or a confirmed all_empty — so an
// omitted or empty scope can never plant the whole map by accident. The ToolDef registration lives in
// Agent/SemanticTools.cs; this Run body validates the scope and then reuses the legacy PlantTool/Plant
// implementation with the original arguments (which already carry the explicit scope), so behavior cannot drift.
// The legacy plant tool in Agent/AgentTools.cs keeps every mode, including the implicit whole-map one.

public static partial class AgentTools
{
    internal static IEnumerator PlantItemSemantic(JsonElement a, CallContext c)
    {
        if (S(a, "item").Trim().Length == 0)
        {
            c.Fail(Reasons.InvalidArgs, "item is required: the seed or fertilizer name, as your inventory shows it.");
            yield break;
        }

        var loc = Loc;
        int scopes = 0;

        // scope 1: an explicit tile list (TileList also reads "tile" and x,y; empty or unparsable means no scope)
        var tiles = TileList(a, "tiles");
        bool tilesGiven = Has(a, "tiles");
        if (tiles.Count > 0)
        {
            scopes++;
            if (tiles.Count > 40)
            {
                c.Fail(Reasons.InvalidArgs, $"tiles has {tiles.Count} entries; the maximum is 40 per call. Split the list and call plant_item again. Nothing was planted.");
                yield break;
            }
            if (!tiles.Any(t => loc.isTileOnMap(t.X, t.Y)))
            {
                c.Fail(Reasons.NotHere, $"None of the given tiles is on {loc.Name} (you are at ({P.TilePoint.X},{P.TilePoint.Y})). Nothing was planted.");
                yield break;
            }
        }

        // scope 2: a rectangle [x, y, w, h] that at least partly lies on the current map
        if (a.ValueKind == JsonValueKind.Object && a.TryGetProperty("area", out var area) && area.ValueKind != JsonValueKind.Null)
        {
            scopes++;
            if (area.ValueKind != JsonValueKind.Array || area.GetArrayLength() != 4
                || AsInt(area[0]) is not int ax || AsInt(area[1]) is not int ay || AsInt(area[2]) is not int aw || AsInt(area[3]) is not int ah)
            {
                c.Fail(Reasons.InvalidArgs, "area must be [x, y, w, h] of whole numbers, x,y = the top-left tile. Nothing was planted.");
                yield break;
            }
            if (aw < 1 || ah < 1)
            {
                c.Fail(Reasons.InvalidArgs, $"area is {aw}x{ah}; its width and height must be at least 1. Nothing was planted.");
                yield break;
            }
            if (FarmAreaTiles(loc, ax, ay, aw, ah).Count == 0)
            {
                c.Fail(Reasons.NotHere, $"The area ({ax},{ay}) {aw}x{ah} is outside {loc.Name}. Nothing was planted.");
                yield break;
            }
        }

        // scope 3: the whole map's empty tilled soil, only behind an explicit confirmation
        bool allEmpty = a.ValueKind == JsonValueKind.Object && a.TryGetProperty("all_empty", out var ae) && ae.ValueKind == JsonValueKind.True;
        if (allEmpty)
        {
            scopes++;
            bool confirm = a.TryGetProperty("confirm", out var cf) && cf.ValueKind == JsonValueKind.True;
            if (!confirm)
            {
                c.Fail(Reasons.InvalidArgs, "all_empty=true plants every empty tilled tile on the current map, so it also needs confirm=true. " +
                                            "Pass confirm=true to do that, or give tiles / area for a smaller scope. Nothing was planted.");
                yield break;
            }
        }

        if (scopes != 1)
        {
            string parsedToNone = tilesGiven && tiles.Count == 0 ? " (the tiles you gave parsed to zero tiles)" : "";
            c.Fail(Reasons.InvalidArgs, "Choose exactly one scope: a non-empty tiles list, an area [x,y,w,h], or all_empty=true" + parsedToNone + ". " +
                                        "An empty or missing scope never means the whole map. Nothing was planted.");
            yield break;
        }

        // The arguments already carry the one validated scope; PlantTool reads tiles / area and its no-tiles,
        // no-area mode is exactly all_empty. No action JSON is invented here.
        yield return PlantTool(a, c);
        // PlantTool's own continuation hint names the legacy tool; this is plant_item now.
        if (c.Result.Contains("call plant again"))
            c.Result = c.Result.Replace("call plant again", "call plant_item again");
    }
}
