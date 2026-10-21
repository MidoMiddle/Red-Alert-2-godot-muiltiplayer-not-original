using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>
/// Debug command: creates a unit of the given type for the player at the given point. Stays until production
/// arrives in M3. An unknown <paramref name="UnitType"/> is rejected by validation, like any bad command.
/// </summary>
public sealed record SpawnTestUnitCommand(PlayerId Player, int ExecuteTick, uint Sequence, UnitType UnitType, WorldPos Position)
    : Command(Player, ExecuteTick, Sequence)
{
    public override CommandType Type => CommandType.SpawnTestUnit;
}
