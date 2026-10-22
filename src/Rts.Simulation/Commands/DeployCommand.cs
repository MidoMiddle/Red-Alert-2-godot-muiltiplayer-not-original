using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>
/// Turns the player's MCV into the construction yard of its faction (docs/implementation/M3_base_economy.md, 4.9): the footprint has its
/// centre in the MCV's cell and follows <see cref="PlacementRules"/>, without the need of a base and with the MCV itself out of the way.
/// </summary>
public sealed record DeployCommand(PlayerId Player, int ExecuteTick, uint Sequence, EntityId Unit)
    : Command(Player, ExecuteTick, Sequence)
{
    public override CommandType Type => CommandType.Deploy;
}
