using Rts.Simulation.World;

namespace Rts.Simulation.Commands;

/// <summary>
/// The player gives up the match (docs/implementation/M5_first_bot.md, 4.8, step 10): defeated at once, as by
/// <see cref="Matches.VictorySystem"/> — everything of theirs is destroyed. Refused outside a match, for a player not in it and for one
/// already defeated.
/// </summary>
public sealed record SurrenderCommand(PlayerId Player, int ExecuteTick, uint Sequence) : Command(Player, ExecuteTick, Sequence)
{
    public override CommandType Type => CommandType.Surrender;
}
