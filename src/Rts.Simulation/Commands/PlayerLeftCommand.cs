using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>
/// The player left a network match (docs/implementation/M9_network.md, 4.11, step 10): quit, lost or dropped by the others. Not the
/// player's own: every computer puts it in on the same tick, the one the host announced. The player is defeated, but what is theirs
/// stays on the map, idle (<see cref="Matches.VictorySystem.Abandon"/>). Refused as <see cref="SurrenderCommand"/> is.
/// </summary>
public sealed record PlayerLeftCommand(PlayerId Player, int ExecuteTick, uint Sequence) : Command(Player, ExecuteTick, Sequence)
{
    public override CommandType Type => CommandType.PlayerLeft;
}
