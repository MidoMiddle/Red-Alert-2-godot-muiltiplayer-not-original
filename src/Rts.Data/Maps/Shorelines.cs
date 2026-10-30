using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;

namespace Rts.Data.Maps;

/// <summary>
/// The shores of a map as the owner wants them (2026-10-30): sand only where a vehicle can come down to the water; a cliff goes
/// straight into the water, with no strip of ground at its foot; and the rim of a cliff has the ground of its plateau.
/// <list type="bullet">
/// <item>A cell at the water's level that touches both a cliff and the water (along its sides) becomes shallow water, twice over, so a
/// strip of up to two cells at the foot of a cliff goes; a wider beach under a cliff stays, one can drive onto it. A cell that holds
/// anything (an object, a building, ore, a start) or lies by a bridge keeps its ground.</item>
/// <item>A beach or a shore-sand cell that is a cliff, lies above the water's level or farther than <see cref="SandReach"/> cells from
/// the water becomes clear land with the texture its dry neighbours have most (the plain ground of the climate when it has none): the
/// converted RA2 maps took every sand of their previews for the shore, fields of it far from any water among them.</item>
/// </list>
/// The generator tidies every map it makes; the map tool the maps made before (<c>tidy-shores</c>). The rule looks only at a cell and
/// its neighbours, so it keeps a symmetric map symmetric.
/// </summary>
public static class Shorelines
{
    // How many times the foot of the cliffs is taken: a strip this wide at most goes under the water.
    private const int FootPasses = 2;

    /// <summary>The sand of a shore lies this many cells from the water at most.</summary>
    public const int SandReach = 2;

    private static readonly (int X, int Y)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static MapDefinition Tidy(MapDefinition map, MapTypes types)
    {
        var (w, h) = (map.Width, map.Height);
        var land = map.Land.ToArray();
        var texture = map.Texture.ToArray();
        var legend = map.TextureLegend.ToList();
        var ground = ClimateGrounds.Of(map.Climate);
        var shoreCodes = legend.Where(e => e.Material == ground.Shore).Select(e => e.Code).ToHashSet();
        var waterCode = Code(legend, ground.Water);
        var kept = Kept(map, types);

        bool Inside(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;
        bool Water(int x, int y) => Inside(x, y) && land[y * w + x] is LandType.Shallow or LandType.Deep;
        int Low(int x, int y) => IntMath.Min(IntMath.Min(map.CornerHeight(x, y), map.CornerHeight(x + 1, y)),
            IntMath.Min(map.CornerHeight(x, y + 1), map.CornerHeight(x + 1, y + 1)));
        int High(int x, int y) => IntMath.Max(IntMath.Max(map.CornerHeight(x, y), map.CornerHeight(x + 1, y)),
            IntMath.Max(map.CornerHeight(x, y + 1), map.CornerHeight(x + 1, y + 1)));
        bool Cliff(int x, int y) => Inside(x, y) && High(x, y) - Low(x, y) >= 2;

        // The foot of the cliffs.
        for (var pass = 0; pass < FootPasses && waterCode is { } water; pass++)
        {
            var under = new List<int>();
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    if (!Water(x, y) && land[y * w + x] is LandType.Clear or LandType.Beach or LandType.Rough && High(x, y) == 0
                        && !kept.Contains(new CellPos(x, y))
                        && Sides.Any(s => Water(x + s.X, y + s.Y)) && Sides.Any(s => Cliff(x + s.X, y + s.Y)))
                    {
                        under.Add(y * w + x);
                    }
                }
            }

            foreach (var i in under)
            {
                land[i] = LandType.Shallow;
                texture[i] = water;
            }
        }

        // The rims: no beach up there.
        var dry = new List<(int Index, char Code)>();
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = y * w + x;
                if ((land[i] == LandType.Beach || shoreCodes.Contains(texture[i])) && (Cliff(x, y) || Low(x, y) > 0 || !NearWater(x, y)))
                {
                    dry.Add((i, Plateau(x, y) ?? Code(legend, ground.Plain) ?? Add(legend, ground.Plain)));
                }
            }
        }

        foreach (var (i, code) in dry)
        {
            land[i] = land[i] == LandType.Beach ? LandType.Clear : land[i];
            texture[i] = code;
        }

        return new MapDefinition(
            map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players, map.Starts,
            [.. legend], map.Heights, [.. land], [.. texture], map.Ore, map.Objects, map.Bridges, map.Structures,
            map.Lighting, map.Playable, map.Origin);

        bool NearWater(int x, int y)
        {
            for (var dy = -SandReach; dy <= SandReach; dy++)
            {
                for (var dx = -SandReach; dx <= SandReach; dx++)
                {
                    if (Water(x + dx, y + dy))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // The texture most of the dry ground round the cell has: not water, not shore sand.
        char? Plateau(int x, int y)
        {
            var counts = new SortedDictionary<char, int>();
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var (nx, ny) = (x + dx, y + dy);
                    if ((dx, dy) != (0, 0) && Inside(nx, ny) && !Water(nx, ny) && land[ny * w + nx] != LandType.Beach
                        && !shoreCodes.Contains(texture[ny * w + nx]))
                    {
                        counts[texture[ny * w + nx]] = counts.GetValueOrDefault(texture[ny * w + nx]) + 1;
                    }
                }
            }

            return counts.Count == 0 ? null : counts.OrderByDescending(c => c.Value).ThenBy(c => c.Key).First().Key;
        }
    }

    // The cells that keep their ground: those of objects, buildings, ore and starts, and every cell next to a bridge.
    private static HashSet<CellPos> Kept(MapDefinition map, MapTypes types)
    {
        var kept = new HashSet<CellPos>(map.Objects.Select(o => o.Cell));
        kept.UnionWith(map.Starts.Select(s => s.Cell));
        foreach (var structure in map.Structures)
        {
            kept.UnionWith(MapChecks.Footprint(structure, types));
        }

        for (var i = 0; i < map.Ore.Length; i++)
        {
            if (map.Ore[i] > 0)
            {
                kept.Add(new CellPos(i % map.Width, i / map.Width));
            }
        }

        foreach (var bridge in map.Bridges)
        {
            foreach (var cell in MapChecks.BridgeCells(bridge))
            {
                for (var dy = -2; dy <= 2; dy++)
                {
                    for (var dx = -2; dx <= 2; dx++)
                    {
                        kept.Add(new CellPos(cell.X + dx, cell.Y + dy));
                    }
                }
            }
        }

        return kept;
    }

    private static char? Code(List<TextureEntry> legend, string material) =>
        legend.FirstOrDefault(e => e.Material == material) is { } entry ? entry.Code : null;

    // A legend entry for the material, on the first letter no entry has.
    private static char Add(List<TextureEntry> legend, string material)
    {
        var code = "abcdefghijklmnopqrstuvwxyz0123456789".First(c => legend.All(e => e.Code != c));
        legend.Add(new TextureEntry(code, material));
        return code;
    }
}
