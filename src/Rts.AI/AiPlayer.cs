using System;
using System.Collections.Generic;
using Rts.Simulation.Ai;
using Rts.Simulation.World;

namespace Rts.AI;

/// <summary>
/// One bot (docs/implementation/M5_first_bot.md, 3, 4.3): on every turn it lets the modules whose tick has come think, in the order of
/// <see cref="AiModuleKind"/>. At most one module of a kind. Without modules it is the empty bot of step 1: it thinks and does nothing.
/// </summary>
public sealed class AiPlayer : IAiController
{
    private readonly IAiModule[] _modules;

    public AiPlayer(PlayerId player, AiSettings settings, IReadOnlyList<IAiModule>? modules = null)
    {
        Player = player;
        Settings = settings;
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

    public void Think(AiTurn turn)
    {
        foreach (var module in _modules)
        {
            if (AiSchedule.IsDue(Settings, module.Kind, Player, turn.Tick))
            {
                module.Update(turn);
            }
        }
    }
}
