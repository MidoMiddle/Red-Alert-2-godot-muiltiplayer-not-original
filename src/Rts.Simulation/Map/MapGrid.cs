using System;
using Rts.Simulation.Geometry;

namespace Rts.Simulation.Map;

/// <summary>
/// The map as the simulation sees it (docs/implementation/M2_unit_control.md, 4.3): per cell a logical
/// <see cref="LandType"/> and a static obstacle flag (a tree now; rocks, fences and other map objects later).
/// Structure of arrays indexed <c>y * Width + x</c>, cheap to hash and to scan. Part of the simulation state:
/// trees and bridges will be destroyed during a match.
/// </summary>
public sealed class MapGrid
{
    private readonly LandType[] _land;
    private readonly bool[] _blocked;

    /// <summary>A map of <paramref name="width"/> × <paramref name="height"/> clear cells.</summary>
    public MapGrid(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), $"Map size must be positive, got {width}x{height}.");
        }

        Width = width;
        Height = height;
        _land = new LandType[width * height];
        Array.Fill(_land, LandType.Clear);
        _blocked = new bool[width * height];
    }

    /// <summary>
    /// A map from raw layers, one byte per cell in index order: land type values and obstacle flags (0 or 1).
    /// Used by the replay reader; throws on unknown land types or flags.
    /// </summary>
    public static MapGrid FromLayers(int width, int height, ReadOnlySpan<byte> land, ReadOnlySpan<byte> blocked)
    {
        var grid = new MapGrid(width, height);
        if (land.Length != grid._land.Length || blocked.Length != grid._blocked.Length)
        {
            throw new ArgumentException($"Layers must have {grid._land.Length} cells, got {land.Length} and {blocked.Length}.");
        }

        for (var i = 0; i < land.Length; i++)
        {
            var type = (LandType)land[i];
            if (!LocomotorRules.IsKnown(type) || blocked[i] > 1)
            {
                throw new ArgumentException($"Cell {i} has an unknown land type {land[i]} or obstacle flag {blocked[i]}.");
            }

            grid._land[i] = type;
            grid._blocked[i] = blocked[i] == 1;
        }

        return grid;
    }

    public int Width { get; }

    public int Height { get; }

    public bool Contains(CellPos cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;

    public LandType LandAt(CellPos cell) => _land[IndexOf(cell)];

    /// <summary>True when a static obstacle (a tree) stands on the cell.</summary>
    public bool IsBlocked(CellPos cell) => _blocked[IndexOf(cell)];

    /// <summary>Speed multiplier in percent for <paramref name="locomotor"/> on the cell; 0 when it cannot enter.</summary>
    public int SpeedPercent(CellPos cell, Locomotor locomotor) =>
        !Contains(cell) || IsBlocked(cell) ? 0 : LocomotorRules.SpeedPercent(locomotor, LandAt(cell));

    /// <summary>True when a unit with <paramref name="locomotor"/> may stand on the cell: inside the map, no obstacle, passable land.</summary>
    public bool CanEnter(CellPos cell, Locomotor locomotor) => SpeedPercent(cell, locomotor) > 0;

    /// <summary>An independent copy: the match changes its own map, never the one in the settings.</summary>
    public MapGrid Clone() => FromLayers(Width, Height, LandBytes(), BlockedBytes());

    /// <summary>True when both maps have the same size and the same cells.</summary>
    public bool ContentEquals(MapGrid other) =>
        Width == other.Width
        && Height == other.Height
        && _land.AsSpan().SequenceEqual(other._land)
        && _blocked.AsSpan().SequenceEqual(other._blocked);

    /// <summary>Land layer as bytes in cell index order, the replay layout.</summary>
    public byte[] LandBytes()
    {
        var bytes = new byte[_land.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)_land[i];
        }

        return bytes;
    }

    /// <summary>Obstacle layer as bytes (0 or 1) in cell index order, the replay layout.</summary>
    public byte[] BlockedBytes()
    {
        var bytes = new byte[_blocked.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = _blocked[i] ? (byte)1 : (byte)0;
        }

        return bytes;
    }

    internal void SetLand(CellPos cell, LandType land)
    {
        if (!LocomotorRules.IsKnown(land))
        {
            throw new ArgumentOutOfRangeException(nameof(land), land, "Unknown land type.");
        }

        _land[IndexOf(cell)] = land;
    }

    internal void SetBlocked(CellPos cell, bool blocked) => _blocked[IndexOf(cell)] = blocked;

    /// <summary>Land of every cell in index order, for the state hash.</summary>
    internal ReadOnlySpan<LandType> LandCells => _land;

    /// <summary>Obstacle flag of every cell in index order, for the state hash.</summary>
    internal ReadOnlySpan<bool> BlockedCells => _blocked;

    private int IndexOf(CellPos cell)
    {
        if (!Contains(cell))
        {
            throw new ArgumentOutOfRangeException(nameof(cell), cell, $"Cell is outside the {Width}x{Height} map.");
        }

        return cell.Y * Width + cell.X;
    }
}
