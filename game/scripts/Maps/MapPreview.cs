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
/// A map with a playable area is drawn as the screen shows it (<see cref="RenderArea"/>): that rectangle, twice as wide as it is
/// tall in the screen's axes. Pure, tested in Rts.Data.Tests.
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

    /// <summary>The size of the picture of <paramref name="map"/> in pixels (<see cref="Render(MapDefinition)"/>).</summary>
    public static (int Width, int Height) SizeOf(MapDefinition map) => map.Playable is { } area
        ? (2 * (area.Right - area.Left), area.Bottom - area.Top)
        : (map.Width * PixelsPerCell, map.Height * PixelsPerCell);

    /// <summary>The picture of <paramref name="map"/>: (2W) × (2H) pixels, three bytes each; or that of its playable area.</summary>
    public static byte[] Render(MapDefinition map) =>
        map.Playable is { } area ? RenderArea(map.ToMapGrid(), map.Starts, area, map.Climate) : Render(map.ToMapGrid(), map.Starts, map.Climate);

    /// <summary>The colour of the plain ground in a climate: the grey green of winter, the green of summer, the sand of the desert.</summary>
    public static (float R, float G, float B) PlainOf(string climate) => climate switch
    {
        "temperate" => (0.42f, 0.58f, 0.3f),
        "urban" => (0.5f, 0.56f, 0.44f),
        "desert" => (0.82f, 0.7f, 0.5f),
        _ => LandColors.Of(LandType.Clear),
    };

    /// <summary>
    /// The playable area as the screen shows it, <see cref="SizeOf"/> pixels: a pixel is a step of the screen's axes
    /// (<c>v = x − z</c> across in half steps, <c>u = x + z</c> down), coloured as the cell under it.
    /// </summary>
    public static byte[] RenderArea(MapGrid grid, IReadOnlyList<MapStart> starts, MapArea area, string climate = "winter")
    {
        var (width, height) = (2 * (area.Right - area.Left), area.Bottom - area.Top);
        var pixels = new byte[width * height * 3];
        (float U, float V) Axes(int px, int py) => (area.Top + py + 0.5f, area.Left + (px + 0.5f) * (area.Right - area.Left) / width);
        for (var py = 0; py < height; py++)
        {
            for (var px = 0; px < width; px++)
            {
                var (u, v) = Axes(px, py);
                var cell = new CellPos((int)MathF.Floor((u + v) / 2f), (int)MathF.Floor((u - v) / 2f));
                var color = grid.Contains(cell) ? CellColor(grid, cell, climate) : Tree;
                Put(pixels, (py * width + px) * 3, color);
            }
        }

        foreach (var start in starts)
        {
            var (u, v) = (start.Cell.X + start.Cell.Y + 1f, (float)(start.Cell.X - start.Cell.Y));
            var (centreX, centreY) = ((v - area.Left) * width / (area.Right - area.Left), u - area.Top);
            for (var py = (int)centreY - StartRadius; py <= (int)centreY + StartRadius; py++)
            {
                for (var px = (int)centreX - StartRadius; px <= (int)centreX + StartRadius; px++)
                {
                    var (dx, dy) = (px + 0.5f - centreX, py + 0.5f - centreY);
                    if (px >= 0 && py >= 0 && px < width && py < height && dx * dx + dy * dy <= StartRadius * StartRadius)
                    {
                        Put(pixels, (py * width + px) * 3, SlotColor(start.Slot));
                    }
                }
            }
        }

        return pixels;
    }

    public static byte[] Render(MapGrid grid, IReadOnlyList<MapStart> starts, string climate = "winter")
    {
        var width = grid.Width * PixelsPerCell;
        var pixels = new byte[width * grid.Height * PixelsPerCell * 3];
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var color = CellColor(grid, new CellPos(x, y), climate);
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
    public static (float R, float G, float B) CellColor(MapGrid grid, CellPos cell, string climate = "winter")
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
                case var _ when mapObject.Type.BlockMask != 0:
                    // A prop of the element documents that stops movers: a hedge, a well, a phone booth.
                    return Fence;
            }
        }

        if (grid.BridgeAt(cell) is { IsIntact: true })
        {
            return LandColors.Of(LandType.Road);
        }

        var shape = grid.Heights.ShapeAt(cell);
        var (r, g, b) = grid.LandAt(cell) == LandType.Clear && shape != CellShape.Cliff ? PlainOf(climate) : LandColors.Of(grid.LandAt(cell), shape);
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
