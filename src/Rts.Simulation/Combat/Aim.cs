using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Simulation.Combat;

/// <summary>Where shots go from and to (docs/implementation/M4_combat.md, 4.7, 4.8).</summary>
public static class Aim
{
    /// <summary>The point of the target nearest to <paramref name="from"/>: a unit's position, the nearest point of a building's footprint.</summary>
    public static WorldPos NearestPoint(Entity target, WorldPos from)
    {
        if (target is not Building building)
        {
            return ((Unit)target).Position;
        }

        var left = building.Cell.X * WorldPos.LeptonsPerCell;
        var top = building.Cell.Y * WorldPos.LeptonsPerCell;
        var right = ((building.Cell.X + building.Width) * WorldPos.LeptonsPerCell) - 1;
        var bottom = ((building.Cell.Y + building.Height) * WorldPos.LeptonsPerCell) - 1;
        return new WorldPos(IntMath.Clamp(from.X, left, right), IntMath.Clamp(from.Y, top, bottom));
    }

    /// <summary>The centre of a building's footprint: where a defense fires from.</summary>
    public static WorldPos CentreOf(Building building) =>
        new(
            (building.Cell.X * WorldPos.LeptonsPerCell) + (building.Width * WorldPos.LeptonsPerCell / 2),
            (building.Cell.Y * WorldPos.LeptonsPerCell) + (building.Height * WorldPos.LeptonsPerCell / 2));

    /// <summary>A length in cells as leptons, rounded down.</summary>
    public static int Leptons(Fixed cells) => (int)((cells.Raw * WorldPos.LeptonsPerCell) >> Fixed.FractionalBits);
}
