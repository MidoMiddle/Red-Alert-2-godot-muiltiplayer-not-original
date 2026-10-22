using Rts.Simulation.Ai;

namespace Rts.AI.Tests;

/// <summary>A module that only writes down the ticks it thought after, into a list it may share with others.</summary>
internal sealed class RecordingModule(AiModuleKind kind, List<(AiModuleKind Kind, int Tick)> log) : IAiModule
{
    public AiModuleKind Kind { get; } = kind;

    public void Update(AiTurn turn) => log.Add((Kind, turn.Tick));
}
