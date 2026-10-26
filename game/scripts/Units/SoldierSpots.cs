using System.Collections.Generic;
using Godot;
using Rts.Simulation.Geometry;
using Rts.Simulation.Movement;
using Rts.Simulation.World;

namespace Rts.Client.Units;

/// <summary>
/// Where a soldier standing on his spot is drawn (the owner, 2026-10-26: a bunch of soldiers looked like one). The rules keep five spots
/// a cell, the centre and the corners (<see cref="Reservations"/>); seen from the camera in the south-east the north-west corner, the centre
/// and the south-east corner stand one behind another. So a soldier standing still on his spot is drawn in a staggered row across the
/// screen instead, about a third of a cell between neighbours, and slides there and back smoothly; one on the move is drawn where he is.
/// The look only: the rules never see it.
/// </summary>
public static class SoldierSpots
{
    /// <summary>How fast the drawn spot follows, in leptons a second.</summary>
    private const float Speed = 512f;

    // Of the rules' spots 0 centre, 1 north-west, 2 north-east, 3 south-west, 4 south-east: the drawn ones, left to right on the screen
    // (south-west, north-west, south-east, centre, north-east), front and back by turns.
    private static readonly Vector2[] Spots = [new(37, -77), new(-77, 37), new(113, -113), new(-113, 113), new(20, 20)];

    private static readonly Dictionary<EntityId, Vector2> Shifts = new();

    /// <summary>The shift the soldier is drawn with now, in leptons (x east, y south); zero for any other unit.</summary>
    public static Vector2 ShiftOf(EntityId unit) => Shifts.TryGetValue(unit, out var shift) ? shift : Vector2.Zero;

    /// <summary>The point the unit is drawn at: a soldier on his spot shifted, any other where it stands.</summary>
    public static WorldPos Drawn(Unit unit, WorldPos at)
    {
        var shift = ShiftOf(unit.Id);
        return shift == Vector2.Zero ? at : new WorldPos(at.X + (int)shift.X, at.Y + (int)shift.Y);
    }

    /// <summary>One frame: the drawn spot of the soldier goes toward its place, the shift of his spot while he stands on it.</summary>
    public static void Step(Unit unit, float delta)
    {
        if (unit.Archetype.Placement != UnitPlacement.Subcell)
        {
            return;
        }

        var target = Vector2.Zero;
        var cell = unit.Position.ToCell();
        if (unit.MoveTarget is null && !unit.PathPending && unit.Subcell is >= 0 and < Reservations.SubcellsPerCell
            && unit.Position == Reservations.SubcellPoint(cell, unit.Subcell))
        {
            var rule = Reservations.SubcellPoint(cell, unit.Subcell) - cell.Center;
            target = Spots[unit.Subcell] - new Vector2(rule.X, rule.Y);
        }

        var now = ShiftOf(unit.Id);
        var next = now.MoveToward(target, Speed * delta);
        if (next == Vector2.Zero)
        {
            Shifts.Remove(unit.Id);
        }
        else
        {
            Shifts[unit.Id] = next;
        }
    }

    /// <summary>A unit gone: its shift too.</summary>
    public static void Forget(EntityId unit) => Shifts.Remove(unit);
}
