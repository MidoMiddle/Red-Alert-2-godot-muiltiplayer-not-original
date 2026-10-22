using System.Collections.Immutable;
using Rts.Simulation.Ai;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>A bot whose turn is a function, for the tests of the turn.</summary>
internal sealed class LambdaBot(PlayerId player, Action<AiTurn> think) : IAiController
{
    public PlayerId Player { get; } = player;

    public void Think(AiTurn turn) => think(turn);
}

/// <summary>
/// A bot that gives commands, to follow them from its turn through the queue into a replay: on its first turn it spawns a heavy
/// tank, then every 15 ticks it sends its units to the next of four points.
/// </summary>
internal sealed class WalkerBot(PlayerId player) : IAiController
{
    private static readonly WorldPos[] Points =
    [
        new CellPos(10, 10).Center, new CellPos(50, 12).Center, new CellPos(48, 50).Center, new CellPos(12, 46).Center,
    ];

    private int _next;
    private bool _spawned;

    public PlayerId Player { get; } = player;

    public void Think(AiTurn turn)
    {
        if (!_spawned)
        {
            turn.Issue(new SpawnTestUnitCommand(Player, turn.ExecuteTick, turn.NextSequence(), UnitType.HeavyTank, new CellPos(30, 30).Center));
            _spawned = true;
            return;
        }

        if (turn.Tick % 15 != 0)
        {
            return;
        }

        var units = turn.World.Entities.OfType<Unit>().Where(unit => unit.IsAlive && unit.Owner == Player).Select(unit => unit.Id).ToImmutableArray();
        if (units.Length > 0)
        {
            turn.Issue(new MoveCommand(Player, turn.ExecuteTick, turn.NextSequence(), units, Points[_next++ % Points.Length]));
        }
    }
}
