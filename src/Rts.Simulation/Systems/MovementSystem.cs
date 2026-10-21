using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Moves test units straight toward their target by <see cref="TestUnit.Speed"/> leptons per tick.
/// Pure integer math: the step is <c>delta * speed / |delta|</c> with an integer square root and
/// round-to-nearest components, so every platform lands on the same lepton and diagonal moves keep
/// the full speed. A rounded component never exceeds the remaining delta, so units never overshoot.
/// </summary>
public static class MovementSystem
{
    public static void Update(GameWorld world, List<SimEvent> events)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is TestUnit { IsAlive: true, MoveTarget: { } target } unit)
            {
                MoveTowards(world, unit, target, events);
            }
        }
    }

    private static void MoveTowards(GameWorld world, TestUnit unit, WorldPos target, List<SimEvent> events)
    {
        var delta = target - unit.Position;
        var distanceSquared = delta.LengthSquared;
        var speed = (long)unit.Speed;

        if (distanceSquared <= speed * speed)
        {
            Arrive(world, unit, target, events);
            return;
        }

        // distance >= speed here, so each component of the step is no longer than the remaining delta.
        var distance = IntMath.Sqrt(distanceSquared);
        var stepX = IntMath.DivRound(delta.X * speed, distance);
        var stepY = IntMath.DivRound(delta.Y * speed, distance);

        if (stepX == 0 && stepY == 0)
        {
            // Very slow unit on a diagonal: truncation would freeze it. Take one lepton along the longer axis.
            if (IntMath.Abs(delta.X) >= IntMath.Abs(delta.Y))
            {
                stepX = IntMath.Sign(delta.X);
            }
            else
            {
                stepY = IntMath.Sign(delta.Y);
            }
        }

        unit.Position += new WorldPos((int)stepX, (int)stepY);
        if (unit.Position == target)
        {
            Arrive(world, unit, target, events);
        }
    }

    private static void Arrive(GameWorld world, TestUnit unit, WorldPos target, List<SimEvent> events)
    {
        unit.Position = target;
        unit.MoveTarget = null;
        events.Add(new UnitArrived(world.CurrentTick, unit.Id, target));
    }
}
