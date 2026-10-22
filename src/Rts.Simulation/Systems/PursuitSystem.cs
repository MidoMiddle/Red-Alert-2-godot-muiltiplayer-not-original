using Rts.Simulation.Combat;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Movement;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Moving to fight (docs/implementation/M4_combat.md, 4.11, 4.12), after <see cref="TargetingSystem"/> and before
/// <see cref="WeaponSystem"/>, in id order, for every armed unit that is not a harvester and not on a leg of a plain move:
/// <list type="bullet">
/// <item>A target beyond the reach of its weapons: the unit drives to it when it may — by an order of the player, on an attack-move,
/// aggressive or guarding — asking a path on the first tick and then again on its tick of <see cref="TargetingSystem.Period"/> when
/// the target moved more than a cell off the last one. Holding its position, it never moves.</item>
/// <item>A target within reach: a unit that drove to it or along an attack-move stops, keeping its route.</item>
/// <item>No target after a fight: an attack-move goes on to the point of its leg, a guarding unit drives back to its anchor when it is
/// more than a cell off, an aggressive one stays where the fight ended.</item>
/// </list>
/// A unit in a fight is <see cref="Unit.Engaged"/>: arriving where it drove does not move its route on to the next leg.
/// </summary>
public static class PursuitSystem
{
    /// <summary>How far, in cells, a guarding unit follows a target from its anchor (4.11).</summary>
    public const int GuardLeash = 6;

    public static void Update(GameWorld world)
    {
        var rules = world.Rules;
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true, Harvester: null } unit || unit.Weapons.Length == 0 || unit.Owner.IsNeutral
                || TargetingSystem.IsMoving(unit))
            {
                continue;
            }

            if (GoalOf(world, unit) is not { } goal)
            {
                EndFight(world, unit);
                continue;
            }

            var weapons = TargetingSystem.WeaponsOf(unit);
            if (WeaponSystem.Reaches(rules, weapons, DamageCalculator.Distance(unit.Position, goal)))
            {
                if (unit.ChaseCell is not null || (unit.IsAttackMoving && !unit.Engaged && (unit.MoveTarget is not null || unit.PathPending)))
                {
                    UnitOrders.Halt(world, unit);
                }

                unit.Engaged = true;
                unit.ChaseCell = null;
                continue;
            }

            var mayFollow = unit.TargetOrder != TargetOrder.None || unit.IsAttackMoving || unit.Stance != Stance.HoldPosition;
            if (!mayFollow)
            {
                continue;
            }

            var cell = goal.ToCell();
            if (unit.ChaseCell is not { } last || (TargetingSystem.IsLookTick(world, unit.Id) && Chebyshev(cell, last) > 1))
            {
                UnitOrders.MoveTo(world, unit, goal);
                unit.ChaseCell = cell;
            }

            unit.Engaged = true;
        }
    }

    /// <summary>Where the unit fires at: the nearest point of its living target, the point of a forced attack, or none.</summary>
    private static WorldPos? GoalOf(GameWorld world, Unit unit)
    {
        if (unit.Target is { } id && world.TryGet(id, out var target) && target.IsAlive && target is Unit or Building)
        {
            return Aim.NearestPoint(target, unit.Position);
        }

        return unit.TargetPoint;
    }

    /// <summary>The fight is over: on along the attack-move, back to the anchor when guarding, or a stop where it ended.</summary>
    private static void EndFight(GameWorld world, Unit unit)
    {
        if (!unit.Engaged)
        {
            return;
        }

        var followed = unit.ChaseCell is not null;
        unit.Engaged = false;
        unit.ChaseCell = null;
        if (unit.IsAttackMoving)
        {
            UnitOrders.MoveTo(world, unit, unit.Orders[unit.OrderIndex].Point);
        }
        else if (unit.Stance == Stance.Guard && Chebyshev(unit.Position.ToCell(), unit.Anchor.ToCell()) > 1)
        {
            UnitOrders.MoveTo(world, unit, unit.Anchor);
        }
        else if (followed)
        {
            UnitOrders.Halt(world, unit);
        }
    }

    private static int Chebyshev(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}
