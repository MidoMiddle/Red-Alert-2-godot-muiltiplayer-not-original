using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Tests.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Load;

/// <summary>
/// A group order measured to the end (docs/implementation/M2_unit_control.md, 4.7): when 90 % of the group stand, when the last
/// one does, how many of the planned places end up taken by the group (whoever stands there: units may swap places), and how
/// far each unit stands from the nearest planned place.
/// </summary>
internal sealed record GroupRun(int Tick90, int TickAll, int PlacesTaken, int Places, int FarthestOff)
{
    public const int MaxTicks = 4000;

    public override string ToString() =>
        $"90% standing at tick {Tick90}, all at {TickAll}; places taken {PlacesTaken}/{Places}; farthest from a place {FarthestOff}";

    /// <summary>Orders <paramref name="units"/> to <paramref name="target"/> with one command and runs until all stand.</summary>
    public static GroupRun Order(GameSimulation simulation, IReadOnlyList<Unit> units, WorldPos target)
    {
        var commands = OrderAll(simulation, units, target);
        simulation.Enqueue(commands);
        return Run(simulation, units);
    }

    public static MoveCommand OrderAll(GameSimulation simulation, IReadOnlyList<Unit> units, WorldPos target) =>
        new(units[0].Owner, simulation.CurrentTick, 9000, units.Select(u => u.Id).ToImmutableArray(), target);

    /// <summary>Steps the order tick, takes the planned places, then runs until every unit of <paramref name="units"/> stands.</summary>
    public static GroupRun Run(GameSimulation simulation, IReadOnlyList<Unit> units)
    {
        simulation.Step();
        var places = units.Select(u => u.Orders[^1].Point).ToList();
        var tick90 = -1;
        var tick = 1;
        for (; tick < MaxTicks; tick++)
        {
            var moving = units.Count(IsBusy);
            if (tick90 < 0 && moving * 10 <= units.Count)
            {
                tick90 = tick;
            }

            if (moving == 0)
            {
                break;
            }

            simulation.Step();
            if (tick % 50 == 0)
            {
                ReservationTests.AssertOccupancy(simulation.World);
            }
        }

        Assert.True(tick < MaxTicks, $"The group still moves after {MaxTicks} ticks.");
        ReservationTests.AssertOccupancy(simulation.World);
        var positions = units.Select(u => u.Position).ToHashSet();
        var taken = places.Count(positions.Contains);
        var farthest = units.Max(u => places.Min(p => Chebyshev(u.Position.ToCell(), p.ToCell())));
        return new GroupRun(tick90, tick, taken, places.Count, farthest);
    }

    public static bool IsBusy(Unit unit) => unit.MoveTarget is not null || unit.PathPending || unit.Path is not null || unit.HoldTicks > 0;

    private static int Chebyshev(CellPos a, CellPos b) => System.Math.Max(System.Math.Abs(a.X - b.X), System.Math.Abs(a.Y - b.Y));
}
