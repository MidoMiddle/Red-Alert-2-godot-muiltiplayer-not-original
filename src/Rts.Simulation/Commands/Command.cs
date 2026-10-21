using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>
/// Everything that changes the world is a command. The command stream is both the network protocol
/// and the replay format, so commands are immutable and fully described by their fields.
/// </summary>
/// <param name="Player">Player who issued the command.</param>
/// <param name="ExecuteTick">Tick on which the command is applied.</param>
/// <param name="Sequence">Per-player increasing number; together with <paramref name="Player"/> it orders commands within a tick.</param>
public abstract record Command(PlayerId Player, int ExecuteTick, uint Sequence)
{
    public abstract CommandType Type { get; }
}
