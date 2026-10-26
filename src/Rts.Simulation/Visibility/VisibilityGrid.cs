using System;
using Rts.Simulation.Geometry;

namespace Rts.Simulation.Visibility;

/// <summary>
/// What one player sees (docs/implementation/M4_combat.md, 4.14): for every cell how many of its units and buildings see it now, and
/// whether it was ever seen. A cell is <see cref="CellVisibility.Visible"/> while the count is above 0, <see cref="CellVisibility.Fog"/>
/// once seen and no longer, <see cref="CellVisibility.Shroud"/> until first seen; what was seen stays explored. Kept up to date a circle
/// at a time by <see cref="Systems.VisibilitySystem"/>. It also counts, for every cell, how many of the player's veil generators veil it
/// from the players outside its team (stage 2d); <see cref="Vision"/> reads that. Part of the simulation state.
/// </summary>
public sealed class VisibilityGrid
{
    private readonly ushort[] _counts;
    private readonly ulong[] _explored;
    private readonly ushort[] _veils;

    internal VisibilityGrid(int width, int height)
    {
        Width = width;
        Height = height;
        _counts = new ushort[width * height];
        _explored = new ulong[(width * height + 63) / 64];
        _veils = new ushort[width * height];
    }

    public int Width { get; }

    /// <summary>
    /// How many times the grid changed — a circle seen or let go, a veil, the map explored or forgotten: the fog on screen works its
    /// cells out again only when a grid of the match changed (2026-10-26). Derived, not hashed: the grid's cells are.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>How many veil generators veil cells from this grid's players' enemies now (stage 2d); none, no cell is veiled.</summary>
    public int VeilSources { get; private set; }

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

    /// <summary>True while one of the player's veil generators veils the cell from the players outside its team; false off the map.</summary>
    public bool IsVeiled(CellPos cell) => Contains(cell) && _veils[IndexOf(cell)] > 0;

    /// <summary>The counts in row order, for the state hash and the tests.</summary>
    internal ReadOnlySpan<ushort> Counts => _counts;

    /// <summary>The explored marks, 64 cells a word in row order, for the state hash.</summary>
    internal ReadOnlySpan<ulong> Explored => _explored;

    /// <summary>How many veil generators veil each cell, in row order, for the state hash.</summary>
    internal ReadOnlySpan<ushort> Veils => _veils;

    /// <summary>Every cell of the map is explored (the orbital uplink, stage 2d); what is in sight does not change.</summary>
    internal void ExploreAll()
    {
        Version++;
        Array.Fill(_explored, ulong.MaxValue);
        var extra = _explored.Length * 64 - Width * Height;
        if (extra > 0)
        {
            _explored[^1] = ulong.MaxValue >> extra;
        }
    }

    /// <summary>The player forgets the map it explored (stage 3b: an infiltrator in its radar): only what it sees now stays explored.</summary>
    internal void ForgetExplored()
    {
        Version++;
        Array.Clear(_explored);
        for (var index = 0; index < _counts.Length; index++)
        {
            if (_counts[index] > 0)
            {
                _explored[index >> 6] |= 1UL << (index & 63);
            }
        }
    }

    /// <summary>A veil generator starts (<paramref name="by"/> 1) or stops (−1) veiling the cells within <paramref name="radius"/>.</summary>
    internal void Veil(CellPos centre, int radius, int by)
    {
        Version++;
        VeilSources += by;
        foreach (var offset in SightCircles.Of(radius))
        {
            var cell = new CellPos(centre.X + offset.X, centre.Y + offset.Y);
            if (Contains(cell))
            {
                _veils[IndexOf(cell)] = (ushort)(_veils[IndexOf(cell)] + by);
            }
        }
    }

    /// <summary>A source starts to see the cells within <paramref name="radius"/> of <paramref name="centre"/>; they are explored.</summary>
    internal void Reveal(CellPos centre, int radius)
    {
        Version++;
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
        Version++;
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
