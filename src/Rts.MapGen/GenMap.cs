using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;

namespace Rts.MapGen;

/// <summary>
/// The map while the stages of <see cref="MapGenerator"/> build it: corner levels, land and ore per cell, and the lists. Every
/// stage keeps it symmetric (see <see cref="Symmetry"/>). <see cref="ToDefinition"/> gives the finished map.
/// </summary>
internal sealed class GenMap
{
    public GenMap(MapGenSettings settings, uint seed)
    {
        Settings = settings;
        Seed = seed;
        N = settings.Size;
        Symmetry = new Symmetry(settings.Symmetry, N);
        Levels = new int[(N + 1) * (N + 1)];
        Land = new LandType[N * N];
        System.Array.Fill(Land, LandType.Clear);
        Ore = new byte[N * N];
        Paint = new string?[N * N];
    }

    public MapGenSettings Settings { get; }

    public uint Seed { get; }

    /// <summary>Cells a side.</summary>
    public int N { get; }

    public Symmetry Symmetry { get; }

    public int[] Levels { get; }

    public LandType[] Land { get; }

    public byte[] Ore { get; }

    /// <summary>A texture material painted over the one of the cell's land (look only, <see cref="Stages.Features.Ground"/>), or null.</summary>
    public string?[] Paint { get; }

    public List<MapStart> Starts { get; } = new();

    public List<MapObjectPlacement> Objects { get; } = new();

    public List<BridgePlacement> Bridges { get; } = new();

    public List<StructurePlacement> Structures { get; } = new();

    public int Corner(int x, int y) => Levels[y * (N + 1) + x];

    public void SetCorner(int x, int y, int level) => Levels[y * (N + 1) + x] = level;

    public bool Contains(CellPos cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < N && cell.Y < N;

    public LandType LandAt(CellPos cell) => Land[cell.Y * N + cell.X];

    public void SetLand(CellPos cell, LandType land) => Land[cell.Y * N + cell.X] = land;

    public bool IsWater(CellPos cell) => LandAt(cell) is LandType.Shallow or LandType.Deep;

    /// <summary>True when the corners of the cell differ by 2 or more: a cliff.</summary>
    public bool IsCliff(CellPos cell)
    {
        var (low, high) = (int.MaxValue, int.MinValue);
        for (var dy = 0; dy <= 1; dy++)
        {
            for (var dx = 0; dx <= 1; dx++)
            {
                var level = Corner(cell.X + dx, cell.Y + dy);
                (low, high) = (IntMath.Min(low, level), IntMath.Max(high, level));
            }
        }

        return high - low >= 2;
    }

    /// <summary>True when all four corners of the cell are on one level.</summary>
    public bool IsFlat(CellPos cell)
    {
        var level = Corner(cell.X, cell.Y);
        return Corner(cell.X + 1, cell.Y) == level && Corner(cell.X, cell.Y + 1) == level && Corner(cell.X + 1, cell.Y + 1) == level;
    }

    /// <summary>Squared distance from the cell to the nearest start.</summary>
    public int StartDistanceSquared(CellPos cell)
    {
        var best = int.MaxValue;
        foreach (var start in Starts)
        {
            var (dx, dy) = (cell.X - start.Cell.X, cell.Y - start.Cell.Y);
            best = IntMath.Min(best, dx * dx + dy * dy);
        }

        return best;
    }

    /// <summary>The map so far as a definition (bridges and buildings included).</summary>
    public MapDefinition ToDefinition(string name, string description)
    {
        var legend = TestMapConverter.Legend;
        var texture = new char[N * N];
        for (var i = 0; i < texture.Length; i++)
        {
            texture[i] = CodeOf(legend, Paint[i] ?? TestMapConverter.MaterialOf(Land[i]));
        }

        var heights = new byte[Levels.Length];
        for (var i = 0; i < heights.Length; i++)
        {
            heights[i] = (byte)Levels[i];
        }

        return new MapDefinition(
            $"random-{Seed}", name, "Map generator", description, N, N, Settings.Climate, Settings.Players,
            [.. Starts], legend, ImmutableArray.Create(heights), [.. Land], [.. texture], ImmutableArray.Create(Ore),
            [.. Objects], [.. Bridges], [.. Structures], Settings.Lighting);
    }

    /// <summary>The ground of the map for the rules: land, heights, objects, bridges and buildings as they are now.</summary>
    public MapGrid Grid() => ToDefinition("work", "").ToMapGrid();

    private static char CodeOf(ImmutableArray<TextureEntry> legend, string material)
    {
        foreach (var entry in legend)
        {
            if (entry.Material == material)
            {
                return entry.Code;
            }
        }

        return legend[0].Code;
    }
}
