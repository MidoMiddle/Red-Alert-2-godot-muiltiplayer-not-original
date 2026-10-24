using System;
using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>The materials of a climate the skirt paints far from the edge: plain ground and its patches (-1 for none).</summary>
public readonly record struct SkirtSlots(byte Ground, int Grass, int Dirt, int Road, int Water);

/// <summary>
/// The land beyond the edge of a map (docs/implementation/M7_maps.md, 4.12, «Край карты»): as in RA2 the map does not end in a
/// void. A band of <see cref="WidthFor"/> cells round the map continues what its edge has: the heights, rivers and roads go on,
/// then fade into gentle hills under snow with patches of grass and dirt; the forest at the edge goes on and pines stand here
/// and there. Never played: the rules and the map file never see it, it is made again from the edge every time, the same each
/// time. The land in front of the map, east (+X) and south (+Z) where the camera looks from, does not rise above the edge, so no
/// hill there hides the map.
/// Godot-free: covered by xUnit.
/// </summary>
public sealed class MapSkirt
{
    // Cells beyond what the view needs: the hills of the map stand up to this far above the ground on screen.
    private const int Margin = 12;

    // Cells of ground from the middle of the view to its corner, zoomed out fully on a wide screen (21:9, with the sidebar).
    private const int ViewReach = 60;

    private const float HillLevels = 4f;
    private const float FadeStart = 2f;
    private const float FadeEnd = 14f;
    private const float FrontFade = 6f;

    private readonly float[] _levels;

    /// <summary>
    /// Cells beyond every edge so the view never shows a void: the camera stops at the frame of the map on screen
    /// (<c>RtsCamera</c>), and the corners of that frame lie half the longer side of the map beyond its edge; a map smaller
    /// than the view sits in its middle, so the band reaches the corners of the view from there.
    /// </summary>
    public static int WidthFor(int mapWidth, int mapHeight) =>
        Math.Max((Math.Max(mapWidth, mapHeight) + 1) / 2, ViewReach - Math.Min(mapWidth, mapHeight) / 2) + Margin;

    private MapSkirt(int mapWidth, int mapHeight, int width)
    {
        MapWidth = mapWidth;
        MapHeight = mapHeight;
        Skirt = width;
        _levels = new float[(CellsX + 1) * (CellsZ + 1)];
        Cells = new byte[CellsX * CellsZ * 2];
    }

    public int MapWidth { get; }

    public int MapHeight { get; }

    /// <summary>Cells beyond every edge.</summary>
    public int Skirt { get; }

    /// <summary>Cells a row of the whole grid, the map and both bands.</summary>
    public int CellsX => MapWidth + 2 * Skirt;

    public int CellsZ => MapHeight + 2 * Skirt;

    /// <summary>
    /// Two bytes a cell of the whole grid, row by row from (-<see cref="Skirt"/>, -<see cref="Skirt"/>): the material slot and
    /// the water, as <c>Climates.MaterialSlots</c> gives them for the map, whose cells are copied unchanged.
    /// </summary>
    public byte[] Cells { get; }

    /// <summary>
    /// <see cref="Cells"/> with the map's own cells replaced by <paramref name="cells"/>: a change inside the map (a stroke of the
    /// texture brush) shows at once without making the skirt again.
    /// </summary>
    public byte[] WithMapCells(byte[] cells)
    {
        var all = (byte[])Cells.Clone();
        for (var z = 0; z < MapHeight; z++)
        {
            Array.Copy(cells, z * MapWidth * 2, all, ((z + Skirt) * CellsX + Skirt) * 2, MapWidth * 2);
        }

        return all;
    }

    /// <summary>Where the pines of the skirt stand, in world cells (never on the map).</summary>
    public List<(float X, float Z)> Trees { get; } = new();

    /// <summary>The level of a corner of the whole grid, in world corner coordinates; on the map it is the level of the rules.</summary>
    public float LevelAt(int x, int z)
    {
        x = Math.Clamp(x, -Skirt, MapWidth + Skirt);
        z = Math.Clamp(z, -Skirt, MapHeight + Skirt);
        return _levels[(z + Skirt) * (CellsX + 1) + x + Skirt];
    }

    /// <param name="map">The map of the rules: heights, land and pines of its edge.</param>
    /// <param name="cells">The materials of its cells (two bytes a cell, <c>Climates.MaterialSlots</c>).</param>
    public static MapSkirt Build(MapGrid map, byte[] cells, SkirtSlots slots) => Build(map, cells, slots, WidthFor(map.Width, map.Height));

    public static MapSkirt Build(MapGrid map, byte[] cells, SkirtSlots slots, int width)
    {
        var skirt = new MapSkirt(map.Width, map.Height, width);
        var seed = Seed(map);
        skirt.BuildLevels(map, seed);
        skirt.BuildCells(map, cells, slots, seed);
        skirt.PlantTrees(map, slots, seed);
        return skirt;
    }

    /// <summary>
    /// A number for the edge of the map: the corner levels, land and pines along it. The skirt is the same for the same number, so
    /// a change inside the map (an editor stroke, a felled tree) does not make it again.
    /// </summary>
    public static uint EdgeSignature(MapGrid map, byte[] cells)
    {
        var h = (uint)(map.Width * 73856093 ^ map.Height * 19349663);
        for (var x = 0; x <= map.Width; x++)
        {
            h = Mix(h, (uint)map.Heights.CornerLevel(x, 0));
            h = Mix(h, (uint)map.Heights.CornerLevel(x, map.Height));
        }

        for (var z = 0; z <= map.Height; z++)
        {
            h = Mix(h, (uint)map.Heights.CornerLevel(0, z));
            h = Mix(h, (uint)map.Heights.CornerLevel(map.Width, z));
        }

        foreach (var index in EdgeCells(map.Width, map.Height))
        {
            h = Mix(h, (uint)(cells[index * 2] | cells[index * 2 + 1] << 8));
        }

        foreach (var mapObject in map.Objects)
        {
            if (mapObject.Kind == MapObjectKind.PineTree && !mapObject.Destroyed && NearEdge(map, mapObject.Cell, 2))
            {
                h = Mix(h, (uint)(mapObject.Cell.Y * map.Width + mapObject.Cell.X));
            }
        }

        return h;
    }

    private void BuildLevels(MapGrid map, uint seed)
    {
        for (var z = -Skirt; z <= MapHeight + Skirt; z++)
        {
            for (var x = -Skirt; x <= MapWidth + Skirt; x++)
            {
                var (ex, ez) = (Math.Clamp(x, 0, MapWidth), Math.Clamp(z, 0, MapHeight));
                float level = map.Heights.CornerLevel(ex, ez);
                var distance = Distance(x - ex, z - ez);
                if (distance > 0f)
                {
                    // A river or a lake at the edge keeps its level as far as its water goes on, then the ground rises.
                    var start = WaterAtCorner(map, ex, ez) ? WaterReach(seed, ex, ez) : FadeStart;
                    var t = SmoothStep(start, start + FadeEnd - FadeStart, distance);
                    var hills = Fbm(x / 11f, z / 11f, seed) * HillLevels;
                    var target = hills - InFront(x, z) * Math.Max(0f, hills - level);
                    level += (target - level) * t;
                }

                _levels[(z + Skirt) * (CellsX + 1) + x + Skirt] = level;
            }
        }
    }

    /// <summary>
    /// How much a corner stands between the camera and the map, 0…1: the camera looks along −X and −Z, so the land east and
    /// south of the map within the diagonal band <c>-H ≤ x − z ≤ W</c> is in front of it. Full a few cells past the edge and inside
    /// the band, fading out over a few cells round them, so the hills there never rise in front of the map and no step forms.
    /// </summary>
    private float InFront(int x, int z)
    {
        var beyond = Math.Max(x - MapWidth, z - MapHeight);
        var outOfBand = Math.Max(0f, Math.Max(x - z - MapWidth, -MapHeight - (x - z)) / MathF.Sqrt(2f));
        return SmoothStep(0f, FrontFade, beyond) * (1f - SmoothStep(0f, FrontFade * 2f, outOfBand));
    }

    private void BuildCells(MapGrid map, byte[] cells, SkirtSlots slots, uint seed)
    {
        for (var z = -Skirt; z < MapHeight + Skirt; z++)
        {
            for (var x = -Skirt; x < MapWidth + Skirt; x++)
            {
                var (ex, ez) = (Math.Clamp(x, 0, MapWidth - 1), Math.Clamp(z, 0, MapHeight - 1));
                var edge = (ez * MapWidth + ex) * 2;
                var at = ((z + Skirt) * CellsX + x + Skirt) * 2;
                var distance = Distance(x - ex, z - ez);
                if (distance == 0f)
                {
                    (Cells[at], Cells[at + 1]) = (cells[edge], cells[edge + 1]);
                    continue;
                }

                var water = cells[edge + 1] != 0 || cells[edge] == slots.Water;
                var reach = water ? WaterReach(seed, ex, ez) : 6f + 8f * Noise(ex / 5f, ez / 5f, seed ^ 0x51u);
                if (distance <= reach)
                {
                    (Cells[at], Cells[at + 1]) = (cells[edge], cells[edge + 1]);
                    continue;
                }

                Cells[at] = slots.Ground;
                var patch = Fbm(x / 6f, z / 6f, seed ^ 0xD1u);
                if (slots.Dirt >= 0 && patch > 0.68f)
                {
                    Cells[at] = (byte)slots.Dirt;
                }
                else if (slots.Grass >= 0 && Fbm(x / 7f, z / 7f, seed ^ 0x6Au) > 0.64f)
                {
                    Cells[at] = (byte)slots.Grass;
                }
            }
        }
    }

    private void PlantTrees(MapGrid map, SkirtSlots slots, uint seed)
    {
        var forest = new bool[map.Width * map.Height];
        foreach (var mapObject in map.Objects)
        {
            if (mapObject.Kind != MapObjectKind.PineTree || mapObject.Destroyed || !NearEdge(map, mapObject.Cell, 2))
            {
                continue;
            }

            // The edge cells within two cells of the pine count as forest.
            for (var dz = -2; dz <= 2; dz++)
            {
                for (var dx = -2; dx <= 2; dx++)
                {
                    var cell = new CellPos(mapObject.Cell.X + dx, mapObject.Cell.Y + dz);
                    if (map.Contains(cell))
                    {
                        forest[cell.Y * map.Width + cell.X] = true;
                    }
                }
            }
        }

        for (var z = -Skirt; z < MapHeight + Skirt; z++)
        {
            for (var x = -Skirt; x < MapWidth + Skirt; x++)
            {
                var (ex, ez) = (Math.Clamp(x, 0, MapWidth - 1), Math.Clamp(z, 0, MapHeight - 1));
                var distance = Distance(x - ex, z - ez);
                var at = ((z + Skirt) * CellsX + x + Skirt) * 2;
                if (distance == 0f || Cells[at + 1] != 0 || Cells[at] == slots.Water || Cells[at] == slots.Road)
                {
                    continue;
                }

                var chance = 0.03f;
                if (forest[ez * MapWidth + ex] && distance <= 8f + 10f * Noise(ex / 6f, ez / 6f, seed ^ 0x7Fu))
                {
                    chance = 0.6f;
                }
                else if (Fbm(x / 9f, z / 9f, seed ^ 0x3Cu) > 0.66f)
                {
                    chance = 0.4f;
                }

                var roll = Hash(x, z, seed ^ 0xB5u);
                if ((roll & 0xFFFF) / 65536f < chance)
                {
                    var jitterX = ((roll >> 16) & 0xFF) / 255f - 0.5f;
                    var jitterZ = ((roll >> 24) & 0xFF) / 255f - 0.5f;
                    Trees.Add((x + 0.5f + jitterX * 0.5f, z + 0.5f + jitterZ * 0.5f));
                }
            }
        }
    }

    /// <summary>The indices of the cells along the four edges of the map.</summary>
    private static IEnumerable<int> EdgeCells(int width, int height)
    {
        for (var x = 0; x < width; x++)
        {
            yield return x;
            yield return (height - 1) * width + x;
        }

        for (var z = 1; z < height - 1; z++)
        {
            yield return z * width;
            yield return z * width + width - 1;
        }
    }

    private static bool NearEdge(MapGrid map, CellPos cell, int cells) =>
        cell.X < cells || cell.Y < cells || cell.X >= map.Width - cells || cell.Y >= map.Height - cells;

    /// <summary>True when a cell next to the edge corner is water: the river goes on beyond it.</summary>
    private static bool WaterAtCorner(MapGrid map, int x, int z)
    {
        for (var dz = -1; dz <= 0; dz++)
        {
            for (var dx = -1; dx <= 0; dx++)
            {
                var cell = new CellPos(x + dx, z + dz);
                if (map.Contains(cell) && map.LandAt(cell) is LandType.Shallow or LandType.Deep)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // How far water goes on beyond the edge: 20 to 26 cells, along the edge by noise, so a river ends raggedly.
    private static float WaterReach(uint seed, int x, int z) => 20f + 6f * Noise(x / 4f, z / 4f, seed ^ 0x2Du);

    private static float Distance(int dx, int dz) => MathF.Sqrt(dx * dx + dz * dz);

    private static float SmoothStep(float from, float to, float value)
    {
        var t = Math.Clamp((value - from) / (to - from), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static uint Seed(MapGrid map) => Mix((uint)map.Width * 2654435761u, (uint)map.Height);

    // Two octaves of value noise, 0…1.
    private static float Fbm(float x, float z, uint seed) => (Noise(x, z, seed) * 2f + Noise(x * 2.1f, z * 2.1f, seed ^ 0x9Eu)) / 3f;

    // Smooth value noise, 0…1.
    private static float Noise(float x, float z, uint seed)
    {
        var (ix, iz) = ((int)MathF.Floor(x), (int)MathF.Floor(z));
        var (fx, fz) = (x - ix, z - iz);
        (fx, fz) = (fx * fx * (3f - 2f * fx), fz * fz * (3f - 2f * fz));
        float At(int px, int pz) => (Hash(px, pz, seed) & 0xFFFF) / 65535f;
        var top = At(ix, iz) + (At(ix + 1, iz) - At(ix, iz)) * fx;
        var bottom = At(ix, iz + 1) + (At(ix + 1, iz + 1) - At(ix, iz + 1)) * fx;
        return top + (bottom - top) * fz;
    }

    private static uint Hash(int x, int z, uint seed) => Mix(Mix(seed, (uint)x), (uint)z);

    private static uint Mix(uint h, uint value)
    {
        h ^= value * 0x9E3779B1u;
        h = (h << 13) | (h >> 19);
        h = h * 5u + 0xE6546B64u;
        h ^= h >> 16;
        h *= 0x85EBCA6Bu;
        h ^= h >> 13;
        return h;
    }
}
