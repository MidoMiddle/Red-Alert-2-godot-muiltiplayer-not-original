using Rts.AI.View;
using Rts.Simulation.Ai;
using Rts.Simulation.Commands;

namespace Rts.AI;

/// <summary>
/// What a module of a bot gets on its turn (docs/implementation/M5_first_bot.md, 4.5, step 3): the world through the bot's eyes, its
/// memory, and the way to give commands for the next tick. Not the world itself: a module cannot learn more than its player could.
/// </summary>
public sealed class AiContext
{
    private AiTurn _turn = null!;

    internal AiContext(AiWorldView view, AiMemory memory)
    {
        View = view;
        Memory = memory;
    }

    public AiWorldView View { get; }

    public AiMemory Memory { get; }

    /// <summary>What the modules agree on: the buildings wanted now.</summary>
    public AiPlan Plan { get; } = new();

    /// <summary>The tick just executed.</summary>
    public int Tick => _turn.Tick;

    /// <summary>The tick the commands of this turn execute on.</summary>
    public int ExecuteTick => _turn.ExecuteTick;

    /// <summary>The number for the next command of the bot.</summary>
    public uint NextSequence() => _turn.NextSequence();

    /// <summary>Gives a command of the bot's player for <see cref="ExecuteTick"/> (<see cref="AiTurn.Issue"/>).</summary>
    public void Issue(Command command) => _turn.Issue(command);

    internal void Begin(AiTurn turn) => _turn = turn;
}
