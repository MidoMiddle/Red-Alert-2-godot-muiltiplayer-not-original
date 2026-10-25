using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rts.Simulation.Replays;
using Rts.Simulation.Rules;

namespace Rts.Net.Desync;

/// <summary>Whether a dump agrees with the replay computed again: the name of the dump, and the fields that differ.</summary>
public sealed record DesyncVerdict(string Name, ImmutableArray<DumpDifference> Differences)
{
    public bool Agrees => Differences.IsEmpty;

    public override string ToString() =>
        Agrees ? $"{Name}: agrees with the replay — this computer computed right."
        : $"{Name}: differs from the replay — this computer went wrong. {DumpDiff.Report(Differences, shown: 5)}";
}

/// <summary>
/// The second mode of <c>tools/Rts.DesyncDiff</c> (docs/implementation/M9_network.md, 4.10): the replay of a desync — the commands every
/// computer executed are the same, that is what lockstep guarantees — computed again headless up to the tick of the dumps, dumped,
/// and compared with each computer's dump. The one that agrees computed right; the one that differs went wrong, and where.
/// </summary>
public static class DesyncReplay
{
    public static IReadOnlyList<DesyncVerdict> Check(Replay replay, GameRules rules, IReadOnlyList<(string Name, string Dump)> dumps)
    {
        var simulation = ReplayPlayer.CreateSimulation(replay, rules);
        var tick = dumps.Select(dump => DumpDiff.TickOf(dump.Dump)).FirstOrDefault(found => found is not null) ?? replay.TickCount;
        while (simulation.CurrentTick < tick)
        {
            simulation.Step();
        }

        var computed = StateDump.Write(simulation.World);
        return dumps.Select(dump => new DesyncVerdict(dump.Name, DumpDiff.Compare(dump.Dump, computed))).ToList();
    }
}
