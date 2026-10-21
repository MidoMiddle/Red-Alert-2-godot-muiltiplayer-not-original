using System;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;

namespace Rts.Simulation.Map;

/// <summary>
/// Heights of the cell corners (docs/implementation/M7_maps.md, 4.1): <c>(W+1) × (H+1)</c> levels 0…<see cref="MaxLevel"/>,
/// row by row from the north-west corner. A cell's shape comes from its four corners, so slopes always meet without gaps;
/// a point's height is the bilinear blend of the corners of its cell, exact in <see cref="Fixed"/>.
/// </summary>
public sealed class CornerHeightGrid
{
    public const int MaxLevel = 14;

    /// <summary>Height of one level in leptons: four levels are almost one cell, as in RA2.</summary>
    public const int LevelHeight = 104;

    private readonly byte[] _levels;

    /// <summary>A flat grid for <paramref name="width"/> × <paramref name="height"/> cells, every corner on level 0.</summary>
    public CornerHeightGrid(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), $"Map size must be positive, got {width}x{height}.");
        }

        Width = width;
        Height = height;
        _levels = new byte[(width + 1) * (height + 1)];
    }

    /// <summary>A grid from a layer of corner levels in index order; throws on a wrong size or a level above 14.</summary>
    public static CornerHeightGrid FromLayer(int width, int height, ReadOnlySpan<byte> levels)
    {
        var grid = new CornerHeightGrid(width, height);
        if (levels.Length != grid._levels.Length)
        {
            throw new ArgumentException($"A height layer for {width}x{height} cells has {grid._levels.Length} corners, got {levels.Length}.");
        }

        for (var i = 0; i < levels.Length; i++)
        {
            if (levels[i] > MaxLevel)
            {
                throw new ArgumentException($"Corner {i} has level {levels[i]}, the highest is {MaxLevel}.");
            }

            grid._levels[i] = levels[i];
        }

        return grid;
    }

    /// <summary>Cells across; the grid has one corner more.</summary>
    public int Width { get; }

    /// <summary>Cells down; the grid has one corner more.</summary>
    public int Height { get; }

    /// <summary>Level of the corner at (<paramref name="x"/>, <paramref name="y"/>), 0 ≤ x ≤ Width, 0 ≤ y ≤ Height.</summary>
    public int CornerLevel(int x, int y) => _levels[CornerIndex(x, y)];

    /// <summary>The level of a cell for the rules ("who stands higher"): its lowest corner, so a ramp counts as its foot.</summary>
    public int CellLevel(CellPos cell)
    {
        var (a, b, c, d) = Corners(cell);
        return IntMath.Min(IntMath.Min(a, b), IntMath.Min(c, d));
    }

    /// <summary>Flat when all four corners are level, a slope when they differ by one level, a cliff above that.</summary>
    public CellShape ShapeAt(CellPos cell)
    {
        var (a, b, c, d) = Corners(cell);
        var rise = IntMath.Max(IntMath.Max(a, b), IntMath.Max(c, d)) - IntMath.Min(IntMath.Min(a, b), IntMath.Min(c, d));
        return rise == 0 ? CellShape.Flat : rise == 1 ? CellShape.Slope : CellShape.Cliff;
    }

    /// <summary>
    /// Height of a point in leptons: the bilinear blend of the corners of its cell. With weights in leptons (0…256 along X and
    /// along Y) the blend is S / 65 536 levels, so the height is S × 104 / 65 536 leptons — exactly <c>Fixed.FromRaw(S × 104)</c>,
    /// with nothing rounded. A point beyond the map takes the height of the nearest edge.
    /// </summary>
    public Fixed HeightAt(WorldPos point)
    {
        const int cell = WorldPos.LeptonsPerCell;
        var x = IntMath.Clamp(point.X, 0, Width * cell);
        var y = IntMath.Clamp(point.Y, 0, Height * cell);
        var cellX = IntMath.Min(x / cell, Width - 1);
        var cellY = IntMath.Min(y / cell, Height - 1);
        long fx = x - cellX * cell;
        long fy = y - cellY * cell;
        var north = CornerLevel(cellX, cellY) * (cell - fx) + CornerLevel(cellX + 1, cellY) * fx;
        var south = CornerLevel(cellX, cellY + 1) * (cell - fx) + CornerLevel(cellX + 1, cellY + 1) * fx;
        var sum = north * (cell - fy) + south * fy;
        return Fixed.FromRaw(sum * LevelHeight);
    }

    /// <summary>The corner levels as bytes in index order: the map file and replay layout.</summary>
    public byte[] LevelBytes() => (byte[])_levels.Clone();

    public CornerHeightGrid Clone() => FromLayer(Width, Height, _levels);

    public bool ContentEquals(CornerHeightGrid other) =>
        Width == other.Width && Height == other.Height && _levels.AsSpan().SequenceEqual(other._levels);

    /// <summary>Corner levels in index order, for the map digest.</summary>
    internal ReadOnlySpan<byte> Levels => _levels;

    internal void SetCornerLevel(int x, int y, int level)
    {
        if (level < 0 || level > MaxLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, $"Levels are 0…{MaxLevel}.");
        }

        _levels[CornerIndex(x, y)] = (byte)level;
    }

    /// <summary>North-west, north-east, south-west and south-east corner levels of the cell.</summary>
    private (int NorthWest, int NorthEast, int SouthWest, int SouthEast) Corners(CellPos cell)
    {
        if (cell.X < 0 || cell.Y < 0 || cell.X >= Width || cell.Y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(cell), cell, $"Cell is outside the {Width}x{Height} map.");
        }

        return (CornerLevel(cell.X, cell.Y), CornerLevel(cell.X + 1, cell.Y), CornerLevel(cell.X, cell.Y + 1), CornerLevel(cell.X + 1, cell.Y + 1));
    }

    private int CornerIndex(int x, int y)
    {
        if (x < 0 || y < 0 || x > Width || y > Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), (x, y), $"Corner is outside the {Width + 1}x{Height + 1} corner grid.");
        }

        return y * (Width + 1) + x;
    }
}
