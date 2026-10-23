using System.Collections.Generic;
using Rts.AI.View;
using Rts.Simulation.Ai;
using Rts.Simulation.Commands;
using Rts.Simulation.Random;

namespace Rts.AI;

/// <summary>
/// What a module of a bot gets on its turn (docs/implementation/M5_first_bot.md, 4.5, step 3): the world through the bot's eyes, its
/// memory, and the way to give commands for the next tick. Not the world itself: a module cannot learn more than its player could.
/// Since M8 step 3 its commands wait their turn in the <see cref="Budget"/>, and a module takes the best of its choices through
/// <see cref="Choose{T}"/>, which errs as often as the level does.
/// </summary>
public sealed class AiContext
{
    private readonly DeterministicRandom _random;
    private AiTurn _turn = null!;

    internal AiContext(AiWorldView view, AiMemory memory, AiSettings? settings = null, ulong seed = 0)
    {
        View = view;
        Memory = memory;
        Settings = settings ?? AiSettings.Medium;
        Budget = new ActionBudget(Settings.ActionsPerSecond);
        _random = new DeterministicRandom(seed, RandomStream + (ulong)view.Me.Value);
    }

    /// <summary>The stream of the random numbers of the bots: one per player, apart from the streams of the simulation.</summary>
    public const ulong RandomStream = 0xA1;

    public AiWorldView View { get; }

    public AiMemory Memory { get; }

    public AiSettings Settings { get; }

    /// <summary>What the modules agree on: the buildings wanted now.</summary>
    public AiPlan Plan { get; } = new();

    /// <summary>The commands of the bot waiting for their turn (M8 step 3).</summary>
    public ActionBudget Budget { get; }

    /// <summary>The module thinking now; its commands wait by its priority.</summary>
    public AiModuleKind Module { get; internal set; }

    /// <summary>The tick just executed.</summary>
    public int Tick => _turn.Tick;

    /// <summary>The tick the commands of this turn execute on — if their turn comes now.</summary>
    public int ExecuteTick => _turn.ExecuteTick;

    /// <summary>The number for the next command of the bot.</summary>
    public uint NextSequence() => _turn.NextSequence();

    /// <summary>
    /// Gives a command of the bot's player: it waits in the <see cref="Budget"/> and goes out at the end of the turn, or later, with its
    /// tick and number then (M8 step 3).
    /// </summary>
    public void Issue(Command command) => Budget.Add(command, Module, Tick);

    /// <summary>
    /// The choice of a module among <paramref name="ranked"/>, the best first (M8, 4.3): the best, or — as often as
    /// <see cref="AiSettings.MistakePercent"/> says — one of the three best at random. A single choice is no choice: no number is drawn.
    /// </summary>
    public T Choose<T>(IReadOnlyList<T> ranked) => ranked[AiChoice.Pick(ranked.Count, Settings.MistakePercent, _random)];

    internal void Begin(AiTurn turn) => _turn = turn;

    /// <summary>The commands whose turn has come go out (M8 step 3).</summary>
    internal void End() => Budget.Release(_turn, View);
}
