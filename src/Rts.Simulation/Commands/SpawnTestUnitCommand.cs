using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>Debug command of M0: creates a test unit for the player at the given point.</summary>
public sealed record SpawnTestUnitCommand(PlayerId Player, int ExecuteTick, uint Sequence, WorldPos Position)
    : Command(Player, ExecuteTick, Sequence)
{
    public override CommandType Type => CommandType.SpawnTestUnit;
}
