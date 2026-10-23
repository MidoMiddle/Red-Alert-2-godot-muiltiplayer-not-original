using System;
using System.Collections.Generic;

namespace Rts.AI.Debug;

/// <summary>A decision of a bot: the tick, the module that took it, what it did and why.</summary>
public sealed record AiDecision(int Tick, AiModuleKind Module, string Text)
{
    /// <summary>One line: <c>12:04 Squads: set out …</c>.</summary>
    public override string ToString() => $"{Tick / 900}:{Tick % 900 / 15:00} {Module}: {Text}";
}

/// <summary>
/// The decisions of a bot with their reasons (docs/implementation/M8_ai_difficulty.md, 4.9, step 12) — a wave set out, a squad turned
/// back, a threat at the base, an outing of the scout, a building placed — for the debug window and the journals: the last
/// <see cref="Capacity"/> in order, and every one to <see cref="Recorded"/>, the file of a developer build. Only while a bot has one
/// (<see cref="AiPlayer.Log"/>): the modules write through <c>context.Log?.Add(…)</c>, so without it not even the text is made. It
/// changes nothing the bot does.
/// </summary>
public sealed class AiDecisionLog
{
    public const int Capacity = 50;

    private readonly Queue<AiDecision> _recent = new();

    /// <summary>The last decisions, the oldest first.</summary>
    public IReadOnlyCollection<AiDecision> Recent => _recent;

    /// <summary>Every decision as it is taken.</summary>
    public event Action<AiDecision>? Recorded;

    internal void Add(int tick, AiModuleKind module, string text)
    {
        var decision = new AiDecision(tick, module, text);
        if (_recent.Count == Capacity)
        {
            _recent.Dequeue();
        }

        _recent.Enqueue(decision);
        Recorded?.Invoke(decision);
    }
}
