using System;
using Rts.Simulation.Geometry;

namespace Rts.Simulation.Visibility;

/// <summary>
/// What one player sees (docs/implementation/M4_combat.md, 4.14): for every cell how many of its units and buildings see it now, and
/// whether it was ever seen. A cell is <see cref="CellVisibility.Visible"/> while the count is above 0, <see cref="CellVisibility.Fog"/>
/// once seen and no longer, <see cref="CellVisibility.Shroud"/> until first seen; what was seen stays explored. Kept up to date a circle
/// at a time by <see cref="Systems.VisibilitySystem"/>. Part of the simulation state.
/// </summary>
public sealed class VisibilityGrid
{
    private readonly ushort[] _counts;
    private readonly ulong[] _explored;

    internal VisibilityGrid(int width, int height)
    {
        Width = width;
        Height = height;
        _counts = new ushort[width * height];
        _explored = new ulong[(width * height + 63) / 64];
    }

    public int Width { get; }

    public int Height { get; }

    public CellVisibility StateAt(CellPos cell) =>
        IsVisible(cell) ? CellVisibility.Visible : IsExplored(cell) ? CellVisibility.Fog : CellVisibility.Shroud;

    /// <summary>True while one of the player's units or buildings sees the cell; false off the map.</summary>
    public bool IsVisible(CellPos cell) => Contains(cell) && _counts[IndexOf(cell)] > 0;

    /// <summary>True once the cell was seen; false off the map.</summary>
    public bool IsExplored(CellPos cell)
    {
        if (!Contains(cell))
        {
            return false;
        }

        var index = IndexOf(cell);
        return (_explored[index >> 6] & (1UL << (index & 63))) != 0;
    }

    /// <summary>How many of the player's units and buildings see the cell.</summary>
    public int CountAt(CellPos cell) => Contains(cell) ? _counts[IndexOf(cell)] : 0;

    /// <summary>The counts in row order, for the state hash and the tests.</summary>
    internal ReadOnlySpan<ushort> Counts => _counts;

    /// <summary>The explored marks, 64 cells a word in row order, for the state hash.</summary>
    internal ReadOnlySpan<ulong> Explored => _explored;

    /// <summary>A source starts to see the cells within <paramref name="radius"/> of <paramref name="centre"/>; they are explored.</summary>
    internal void Reveal(CellPos centre, int radius)
    {
        foreach (var offset in SightCircles.Of(radius))
        {
            var cell = new CellPos(centre.X + offset.X, centre.Y + offset.Y);
            if (Contains(cell))
            {
                var index = IndexOf(cell);
                _counts[index]++;
                _explored[index >> 6] |= 1UL << (index & 63);
            }
        }
    }

    /// <summary>A source no longer sees the cells within <paramref name="radius"/> of <paramref name="centre"/>.</summary>
    internal void Hide(CellPos centre, int radius)
    {
        foreach (var offset in SightCircles.Of(radius))
        {
            var cell = new CellPos(centre.X + offset.X, centre.Y + offset.Y);
            if (Contains(cell))
            {
                _counts[IndexOf(cell)]--;
            }
        }
    }

    private bool Contains(CellPos cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;

    private int IndexOf(CellPos cell) => cell.Y * Width + cell.X;
}
