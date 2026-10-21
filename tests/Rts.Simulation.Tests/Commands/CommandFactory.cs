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

    public static MoveCommand Move(int player, int tick, uint sequence, int[] units, int x = 640, int y = 640, bool queue = false) =>
        new(new PlayerId(player), tick, sequence, Ids(units), new WorldPos(x, y), queue);

    public static AttackMoveCommand AttackMove(int player, int tick, uint sequence, int[] units, int x = 640, int y = 640, bool queue = false) =>
        new(new PlayerId(player), tick, sequence, Ids(units), new WorldPos(x, y), queue);

    public static StopCommand Stop(int player, int tick, uint sequence, int[] units) => new(new PlayerId(player), tick, sequence, Ids(units));

    public static ScatterCommand Scatter(int player, int tick, uint sequence, int[] units) => new(new PlayerId(player), tick, sequence, Ids(units));

    public static WaypointsCommand Waypoints(int player, int tick, uint sequence, int[] units, (int X, int Y)[] points, bool loop = false) =>
        new(new PlayerId(player), tick, sequence, Ids(units), points.Select(p => new WorldPos(p.X, p.Y)).ToImmutableArray(), loop);

    private static ImmutableArray<EntityId> Ids(int[] units) => units.Select(id => new EntityId(id)).ToImmutableArray();
}
