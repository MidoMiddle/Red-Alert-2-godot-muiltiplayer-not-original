using System.Diagnostics;
using Rts.AI;
using Rts.AI.Squads;
using Rts.Simulation.Ai;
using Rts.Simulation.World;

namespace Rts.Arena;

/// <summary>
/// A bot of the Arena as the match sees it: it thinks as <see cref="Bot"/> does, and the Arena learns how long it thought (M8, 4.11) and
/// the tick its first squad set out. Neither goes into the match: the time is the machine's, the tick is read, not changed.
/// </summary>
public sealed class TimedBot(AiPlayer bot) : IAiController
{
    private readonly Stopwatch _watch = new();

    public AiPlayer Bot { get; } = bot;

    public PlayerId Player => Bot.Player;

    /// <summary>The turns the bot had.</summary>
    public int Turns { get; private set; }

    /// <summary>The time of all its turns together.</summary>
    public TimeSpan Total { get; private set; }

    /// <summary>The longest of its turns.</summary>
    public TimeSpan Longest { get; private set; }

    /// <summary>The tick its first squad set out, or -1 while none has.</summary>
    public int FirstAttack { get; private set; } = -1;

    /// <summary>The turns longer than the peak budget of 4.11, <see cref="SlowTurn"/> (step 9).</summary>
    public int SlowTurns { get; private set; }

    /// <summary>The peak budget of a turn of a bot (M8, 4.11): 4 ms.</summary>
    public static readonly TimeSpan SlowTurn = TimeSpan.FromMilliseconds(4);

    public void Think(AiTurn turn)
    {
        _watch.Restart();
        Bot.Think(turn);
        _watch.Stop();
        Turns++;
        Total += _watch.Elapsed;
        Longest = _watch.Elapsed > Longest ? _watch.Elapsed : Longest;
        SlowTurns += _watch.Elapsed > SlowTurn ? 1 : 0;
        if (FirstAttack < 0 && Bot.Context?.Plan.Squads.Any(squad => squad.State == SquadState.Moving) == true)
        {
            FirstAttack = turn.Tick;
        }
    }
}
