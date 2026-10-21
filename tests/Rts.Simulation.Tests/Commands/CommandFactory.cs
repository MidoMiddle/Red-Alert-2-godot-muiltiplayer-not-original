using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Commands;

/// <summary>Short constructors for commands in tests.</summary>
internal static class CommandFactory
{
    public static SpawnTestUnitCommand Spawn(int player, int tick, uint sequence, int x = 128, int y = 128, UnitType type = UnitType.Conscript) =>
        new(new PlayerId(player), tick, sequence, type, new WorldPos(x, y));

    public static MoveUnitsCommand Move(int player, int tick, uint sequence, int[] units, int x = 640, int y = 640) =>
        new(new PlayerId(player), tick, sequence, units.Select(id => new EntityId(id)).ToImmutableArray(), new WorldPos(x, y));
}
