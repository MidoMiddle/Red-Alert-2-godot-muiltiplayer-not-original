using System;
using System.Collections.Generic;
using Rts.Client.Terrain;
using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Client.Maps;

/// <summary>
/// The picture of a map seen from above (docs/implementation/M7_maps.md, 4.11, step 10): <see cref="PixelsPerCell"/> pixels per
/// cell, RGB bytes row by row from the north-west. Symbolic, like the field (conventions, section 12): the land in the colours
/// of the game (<see cref="LandColors"/>), lighter the higher, cliffs in dark rock; standing trees, rocks, walls and fences;
/// standing bridge segments as road; buildings; and a disc in the slot's colour on every start (the screen lays the numbers on
/// top). Ore has no rules before M3 and is not drawn. Drawn from the map of the rules (<see cref="MapDefinition.ToMapGrid"/>).
/// Pure, tested in Rts.Data.Tests.
/// </summary>
public static class MapPreview
{
    public const int PixelsPerCell = 2;

    /// <summary>Radius of a start disc, in pixels.</summary>
    public const int StartRadius = 3;

    /// <summary>Where the cached previews live: one PNG per map hash, so a changed map gets a new picture.</summary>
    public const string CacheDirectory = "user://cache/previews";

    public static readonly (float R, float G, float B) Tree = (0.12f, 0.3f, 0.12f);
    public static readonly (float R, float G, float B) Stone = (0.55f, 0.55f, 0.57f);
    public static readonly (float R, float G, float B) Fence = (0.5f, 0.35f, 0.2f);
    public static readonly (float R, float G, float B) Building = (0.72f, 0.66f, 0.55f);
    public static readonly (float R, float G, float B) SnowDrift = (0.78f, 0.84f, 0.9f);
    public static readonly (float R, float G, float B) OreSource = (0.95f, 0.8f, 0.15f);
    public static readonly (float R, float G, float B) GemSource = (0.35f, 0.8f, 1f);

    /// <summary>The cached picture of the map with this <see cref="MapHasher.Hash"/>.</summary>
    public static string CachePath(string hash) => $"{CacheDirectory}/{hash}.png";

    /// <summary>The picture of <paramref name="map"/>: (2W) × (2H) pixels, three bytes each.</summary>
    public static byte[] Render(MapDefinition map) => Render(map.ToMapGrid(), map.Starts);

    public static byte[] Render(MapGrid grid, IReadOnlyList<MapStart> starts)
    {
        var width = grid.Width * PixelsPerCell;
        var pixels = new byte[width * grid.Height * PixelsPerCell * 3];
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var color = CellColor(grid, new CellPos(x, y));
                for (var py = y * PixelsPerCell; py < (y + 1) * PixelsPerCell; py++)
                {
                    for (var px = x * PixelsPerCell; px < (x + 1) * PixelsPerCell; px++)
                    {
                        Put(pixels, (py * width + px) * 3, color);
                    }
                }
            }
        }

        foreach (var start in starts)
        {
            // The disc is centred on the middle of the start cell: pixel centres within the radius take the slot's colour.
            var centreX = start.Cell.X * PixelsPerCell + PixelsPerCell / 2f;
            var centreY = start.Cell.Y * PixelsPerCell + PixelsPerCell / 2f;
            for (var py = (int)centreY - StartRadius; py <= (int)centreY + StartRadius; py++)
            {
                for (var px = (int)centreX - StartRadius; px <= (int)centreX + StartRadius; px++)
                {
                    var (dx, dy) = (px + 0.5f - centreX, py + 0.5f - centreY);
                    if (px >= 0 && py >= 0 && px < width && py < grid.Height * PixelsPerCell && dx * dx + dy * dy <= StartRadius * StartRadius)
                    {
                        Put(pixels, (py * width + px) * 3, SlotColor(start.Slot));
                    }
                }
            }
        }

        return pixels;
    }

    /// <summary>The colour of one cell of the preview, components 0…1.</summary>
    public static (float R, float G, float B) CellColor(MapGrid grid, CellPos cell)
    {
        if (grid.HasBuilding(cell))
        {
            return Building;
        }

        foreach (var mapObject in grid.ObjectsAt(cell))
        {
            if (!mapObject.IsAlive)
            {
                continue;
            }

            switch (mapObject.Kind)
            {
                case MapObjectKind.PineTree:
                    return Tree;
                case MapObjectKind.Boulder or MapObjectKind.WallStone:
                    return Stone;
                case MapObjectKind.FenceWood:
                case MapObjectKind.AntiTankHedgehog or MapObjectKind.Pipeline:
                    return Fence;
                case MapObjectKind.Snowdrift:
                    return SnowDrift;
                case MapObjectKind.OreSource:
                    return OreSource;
                case MapObjectKind.GemSource:
                    return GemSource;
            }
        }

        if (grid.BridgeAt(cell) is { IsIntact: true })
        {
            return LandColors.Of(LandType.Road);
        }

        var shape = grid.Heights.ShapeAt(cell);
        var (r, g, b) = LandColors.Of(grid.LandAt(cell), shape);
        var shade = shape == CellShape.Cliff ? 1f : HeightShade(grid.Heights.CellLevel(cell));
        return (r * shade, g * shade, b * shade);
    }

    /// <summary>Lighter the higher: ×0.85 at level 0, 3 % more per level, at most ×1.25.</summary>
    public static float HeightShade(int level) => Math.Min(0.85f + 0.03f * level, 1.25f);

    /// <summary>The colour of a start slot, the same as the units of that player: 1 blue, 2 red, then round the hues.</summary>
    public static (float R, float G, float B) SlotColor(int slot) => slot switch
    {
        1 => (0.2f, 0.45f, 0.95f),
        2 => (0.9f, 0.25f, 0.2f),
        _ => FromHue(slot * 0.13f % 1f),
    };

    public static byte ToByte(float component) => (byte)(component <= 0f ? 0 : component >= 1f ? 255 : (int)(component * 255f + 0.5f));

    private static void Put(byte[] pixels, int index, (float R, float G, float B) color)
    {
        pixels[index] = ToByte(color.R);
        pixels[index + 1] = ToByte(color.G);
        pixels[index + 2] = ToByte(color.B);
    }

    // Saturation 0.7, value 0.9, as Godot's Color.FromHsv for the other owners of UnitRenderer.
    private static (float R, float G, float B) FromHue(float hue)
    {
        const float S = 0.7f;
        const float V = 0.9f;
        var h = hue * 6f;
        var i = (int)h % 6;
        var f = h - (int)h;
        var (p, q, t) = (V * (1 - S), V * (1 - S * f), V * (1 - S * (1 - f)));
        return i switch
        {
            0 => (V, t, p),
            1 => (q, V, p),
            2 => (p, V, t),
            3 => (p, q, V),
            4 => (t, p, V),
            _ => (V, p, q),
        };
    }
}
