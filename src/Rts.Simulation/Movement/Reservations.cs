using System;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Movement;

/// <summary>
/// Who stands in each cell (docs/implementation/M2_unit_control.md, 4.7): one unit per cell, the cell its position is in.
/// Moving into another cell needs that cell free; the move releases the old cell and takes the new one at once. Units move
/// in <see cref="EntityId"/> order, so two units never take the same cell in one tick. Part of the simulation state.
/// Infantry also takes a whole cell until the subcells of M2 step 10.
/// </summary>
public sealed class Reservations
{
    private readonly int[] _owner;

    internal Reservations(int width, int height)
    {
        Width = width;
        Height = height;
        _owner = new int[width * height];
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The unit standing in the cell, or <see cref="EntityId.None"/> when it is free.</summary>
    public EntityId OwnerOf(CellPos cell) => new(_owner[IndexOf(cell)]);

    public bool IsFree(CellPos cell) => _owner[IndexOf(cell)] == 0;

    /// <summary>Owner of every cell in index order, for the state hash.</summary>
    internal ReadOnlySpan<int> Cells => _owner;

    /// <summary>Takes a free cell. Returns false when somebody else stands there.</summary>
    internal bool TryOccupy(CellPos cell, EntityId unit)
    {
        var index = IndexOf(cell);
        if (_owner[index] != 0 && _owner[index] != unit.Value)
        {
            return false;
        }

        _owner[index] = unit.Value;
        return true;
    }

    /// <summary>Frees the cell if <paramref name="unit"/> holds it.</summary>
    internal void Release(CellPos cell, EntityId unit)
    {
        var index = IndexOf(cell);
        if (_owner[index] == unit.Value)
        {
            _owner[index] = 0;
        }
    }

    private int IndexOf(CellPos cell)
    {
        if (cell.X < 0 || cell.Y < 0 || cell.X >= Width || cell.Y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(cell), cell, $"Cell is outside the {Width}x{Height} map.");
        }

        return cell.Y * Width + cell.X;
    }
}
