using Rts.Simulation.Combat;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Movement;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The ammo of aircraft (stage 3f of filling the game: the strike jets, RA2 <c>Ammo</c>), after the movement. A unit whose type carries
/// ammo spends one with every volley it starts (<see cref="WeaponSystem"/>) and starts none without. Out of ammo, once its last volley is
/// over, it forgets its target and flies to the nearest ready building of its owner that rearms aircraft; beside it — within a cell of the
/// footprint — it loads a volley every <see cref="RearmTicks"/> ticks up to its type's ammo. Without such a building it waits where it is.
/// </summary>
public static class AmmoSystem
{
    /// <summary>The ticks to load a volley (RA2 <c>ReloadRate=.3</c> minutes).</summary>
    public const int RearmTicks = 270;

    public static void Update(GameWorld world)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true, Archetype.Ammo: > 0 } unit || unit.Ammo >= unit.Archetype.Ammo
                || unit.Weapons is [{ BurstLeft: > 0 }, ..])
            {
                continue;
            }

            if (PadOf(world, unit) is not { } pad)
            {
                continue;
            }

            if (Beside(pad, unit.Position.ToCell()))
            {
                if (++unit.RearmTicks >= RearmTicks)
                {
                    unit.Ammo++;
                    unit.RearmTicks = 0;
                }
            }
            else if (unit.Ammo == 0 && unit.MoveTarget != Aim.CentreOf(pad))
            {
                UnitOrders.ForgetTarget(unit);
                UnitOrders.MoveTo(world, unit, Aim.CentreOf(pad));
            }
        }
    }

    // The nearest ready building of the owner that rearms aircraft, by its centre, then by id.
    private static Building? PadOf(GameWorld world, Unit unit)
    {
        Building? best = null;
        var bestDistance = int.MaxValue;
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, IsReady: true, Def.RearmsAircraft: true } pad && pad.Owner == unit.Owner)
            {
                var distance = DamageCalculator.Distance(unit.Position, Aim.CentreOf(pad));
                if (distance < bestDistance)
                {
                    (best, bestDistance) = (pad, distance);
                }
            }
        }

        return best;
    }

    private static bool Beside(Building pad, CellPos cell) =>
        IntMath.Max(
            IntMath.Max(pad.Cell.X - cell.X, cell.X - pad.FarCell.X),
            IntMath.Max(pad.Cell.Y - cell.Y, cell.Y - pad.FarCell.Y)) <= 1;
}
