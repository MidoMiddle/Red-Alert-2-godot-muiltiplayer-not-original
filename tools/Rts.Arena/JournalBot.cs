using System.Text;
using Rts.AI;
using Rts.Simulation.Ai;
using Rts.Simulation.World;

namespace Rts.Arena;

/// <summary>
/// A bot of a kept match as its replay plays back with the bots run again (docs/implementation/M8_ai_difficulty.md, 4.10, step 9): it
/// thinks as <see cref="Bot"/> does and writes what it thinks — the snapshot of the overlay F12 — into its journal once a minute, and
/// between them every decision it takes with its reason (step 12, <see cref="Rts.AI.Debug.AiDecisionLog"/>).
/// </summary>
public sealed class JournalBot : IAiController
{
    public JournalBot(AiPlayer bot)
    {
        Bot = bot;
        var log = new Rts.AI.Debug.AiDecisionLog();
        log.Recorded += decision => Journal.Append("  ").Append(decision).Append('\n');
        bot.Log = log;
    }

    /// <summary>A line every minute of the match.</summary>
    public const int EveryTicks = 900;

    public AiPlayer Bot { get; }

    public PlayerId Player => Bot.Player;

    public StringBuilder Journal { get; } = new();

    public void Think(AiTurn turn)
    {
        Bot.Think(turn);
        if (turn.Tick % EveryTicks == 0 && Bot.Snapshot() is { } snapshot)
        {
            Journal.Append("== tick ").Append(turn.Tick).Append('\n').Append(snapshot.ToText()).Append("\n\n");
        }
    }
}
