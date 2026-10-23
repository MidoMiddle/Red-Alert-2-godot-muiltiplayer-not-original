using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.AI.View;
using Rts.Simulation.Ai;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.AI;

/// <summary>
/// One bot (docs/implementation/M5_first_bot.md, 3, 4.3, 4.5): on every turn it looks at the world through its <see cref="AiWorldView"/>,
/// brings its <see cref="AiMemory"/> up to date, then lets the modules whose tick has come think, in the order of <see cref="AiModuleKind"/>;
/// their commands go out through its <see cref="ActionBudget"/> at the end of the turn (M8 step 3).
/// At most one module of a kind. Without modules it only looks and remembers.
/// </summary>
public sealed class AiPlayer : IAiController
{
    private readonly IAiModule[] _modules;
    private readonly CellPos? _start;
    private readonly ImmutableArray<CellPos> _mapStarts;
    private readonly ulong _seed;
    private AiContext? _context;

    /// <summary>
    /// The bot of <paramref name="player"/>. <paramref name="start"/> and <paramref name="mapStarts"/> — its start and every start of the
    /// map — come from the match setup; a bot outside a match knows none. <paramref name="seed"/>, the seed of the match, draws its mistakes
    /// (M8 step 3): the same on every machine and when the bots of a replay run again.
    /// </summary>
    public AiPlayer(
        PlayerId player, AiSettings settings, IReadOnlyList<IAiModule>? modules = null, CellPos? start = null, ImmutableArray<CellPos> mapStarts = default,
        ulong seed = 0)
    {
        Player = player;
        Settings = settings;
        _start = start;
        _mapStarts = mapStarts;
        _seed = seed;
        var ordered = new List<IAiModule>(modules ?? []);
        ordered.Sort((a, b) => a.Kind.CompareTo(b.Kind));
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].Kind == ordered[i - 1].Kind)
            {
                throw new ArgumentException($"Two modules of the kind {ordered[i].Kind}.", nameof(modules));
            }
        }

        _modules = ordered.ToArray();
    }

    public PlayerId Player { get; }

    public AiSettings Settings { get; }

    /// <summary>What the bot sees and remembers; null before its first turn.</summary>
    public AiContext? Context => _context;

    /// <summary>
    /// A copy of what the bot thinks, for the debug overlay (docs/implementation/M5_first_bot.md, 4.13, step 13); null before its first
    /// turn. Made from what the bot holds after its last turn: asking changes nothing in the bot or the match.
    /// </summary>
    public Debug.AiDebugSnapshot? Snapshot() => _context is null ? null : Debug.AiDebugSnapshot.Of(_context) with { Profile = Profile };

    /// <summary>
    /// The journal of its decisions (M8 step 12): null — off, the default — or one the debug window, a developer build or the Arena set; it
    /// changes nothing the bot does.
    /// </summary>
    public Debug.AiDecisionLog? Log { get; set; }

    /// <summary>The level and the personality of the bot, <c>ai_hard:balanced</c>, for the debug window (M8 step 12); null when unknown.</summary>
    public string? Profile { get; init; }

    public void Think(AiTurn turn)
    {
        _context ??= new AiContext(new AiWorldView(turn.World, Player, _start, _mapStarts, Settings.PerceptionDelay), new AiMemory(), Settings, _seed);
        _context.Log = Log;
        _context.View.Refresh(turn.World, turn.Tick);
        _context.Memory.Update(_context.View);
        _context.Begin(turn);
        foreach (var module in _modules)
        {
            if (AiSchedule.IsDue(Settings, module.Kind, Player, turn.Tick))
            {
                _context.Module = module.Kind;
                module.Update(_context);
            }
        }

        _context.End();
    }
}
