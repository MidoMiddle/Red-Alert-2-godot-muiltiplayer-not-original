using System;
using Rts.Simulation.Commands;
using Rts.Simulation.World;

namespace Rts.Simulation.Ai;

/// <summary>
/// One turn of a bot (docs/implementation/M5_first_bot.md, 4.2): the world after tick <see cref="Tick"/>, to read only, and the way to
/// give commands, which execute on <see cref="ExecuteTick"/> through the same queue and the same checks as the commands of a person.
/// A bot changes the world only by its commands. One turn object per bot, reused from tick to tick.
/// </summary>
public sealed class AiTurn
{
    /// <summary>
    /// Where the numbers of a bot's commands start: the upper half of the range, so that they never meet those of a person who plays
    /// the same player with the debug switch (F11) on the same tick.
    /// </summary>
    public const uint FirstSequence = 0x8000_0000;

    private readonly GameSimulation _simulation;
    private uint _nextSequence = FirstSequence;

    internal AiTurn(GameSimulation simulation, PlayerId player)
    {
        _simulation = simulation;
        Player = player;
    }

    /// <summary>The player of the bot.</summary>
    public PlayerId Player { get; }

    /// <summary>The world after tick <see cref="Tick"/>. To read only: from step 3 on, the modules of a bot see it through its eyes.</summary>
    public GameWorld World => _simulation.World;

    /// <summary>The tick just executed.</summary>
    public int Tick { get; internal set; }

    /// <summary>The tick the commands of this turn execute on.</summary>
    public int ExecuteTick => Tick + 1;

    /// <summary>The number for the next command of the bot.</summary>
    public uint NextSequence() => _nextSequence++;

    /// <summary>
    /// Gives a command for <see cref="ExecuteTick"/>. A command of another player or for another tick is a mistake in the code of the bot,
    /// not in the game, and throws.
    /// </summary>
    public void Issue(Command command)
    {
        if (command.Player != Player)
        {
            throw new ArgumentException($"A bot of {Player} cannot give a command of {command.Player}.", nameof(command));
        }

        if (command.ExecuteTick != ExecuteTick)
        {
            throw new ArgumentException($"A command of a bot executes on tick {ExecuteTick}, not {command.ExecuteTick}.", nameof(command));
        }

        _simulation.Enqueue(command);
    }
}
