using Rts.Simulation.Benchmarks;
using Rts.Simulation.Hashing;
using Rts.Simulation.Replays;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Determinism;

/// <summary>
/// The general battle of M4 (docs/implementation/M4_combat.md, section 6, step 15): 150 against 150 with their bases for 3000 ticks
/// gives the same checksums in two runs, and its replay plays it back the same. The state is hashed every <see cref="HashInterval"/>
/// ticks: on every tick the whole state — the sight of eight players included — took the test half a minute.
/// </summary>
public sealed class BattleDeterminismTests
{
    public const int HashInterval = 10;

    private static readonly Rts.Simulation.Map.MapGrid Land = LoadScenario.LoadMap(LoadScenario.RepoRoot(AppContext.BaseDirectory), TestRules.Standard).Grid;

    /// <summary>The checksums of one run, made once and shared with the golden values.</summary>
    internal static readonly Lazy<IReadOnlyList<StateChecksum>> Once = new(() => Run(watch: true));

    /// <summary>The world at the end of <see cref="Once"/> and the count of its events beside its statistics (M5 step 11).</summary>
    private static (GameWorld World, Matches.EventTally Tally)? _watched;

    /// <summary>The checksum of every <see cref="HashInterval"/>-th tick of the battle.</summary>
    internal static IReadOnlyList<StateChecksum> Run(int ticks = BattleScenario.Ticks, bool watch = false)
    {
        var simulation = BattleScenario.Start(Land.Clone(), TestRules.Standard, HashInterval, BattleBases.Build);
        BattleScenario.Charge(simulation);
        if (watch)
        {
            _watched = (simulation.World, Matches.EventTally.Attach(simulation));
        }

        var checksums = new List<StateChecksum>(ticks / HashInterval);
        for (var tick = 0; tick < ticks; tick++)
        {
            simulation.Step();
            if (simulation.CurrentTick % HashInterval == 0)
            {
                checksums.Add(simulation.LastChecksum!.Value);
            }
        }

        return checksums;
    }

    [Fact]
    public void The_battle_gives_the_same_checksums_twice()
    {
        var first = Once.Value;
        var second = Run();

        Assert.Equal(BattleScenario.Ticks / HashInterval, first.Count);
        Assert.Equal(first, second);
    }

    [Fact]
    public void The_statistics_of_the_battle_say_what_its_events_said()
    {
        _ = Once.Value;
        var (world, tally) = _watched!.Value;

        Assert.True(tally.Deaths > 100, $"{tally.Deaths} deaths");
        tally.AssertAgrees(world);
    }

    [Fact]
    public void The_replay_of_the_battle_plays_it_back_the_same()
    {
        var live = BattleScenario.Create(Land.Clone(), TestRules.Standard);
        var recorder = new ReplayRecorder(live);
        live.Step();
        recorder.AfterStep();
        BattleScenario.Charge(live);
        for (var tick = 1; tick < BattleScenario.Ticks; tick++)
        {
            live.Step();
            recorder.AfterStep();
        }

        var replay = ReplayFormat.Read(ReplayFormat.Write(recorder.Build()), TestRules.Standard.MapTypes);
        var result = ReplayPlayer.Verify(replay, TestRules.Standard);

        Assert.Equal(ReplayStatus.Match, result.Status);
        Assert.Equal(new StateChecksum(BattleScenario.Ticks, StateHasher.Compute(live.World)), result.Final);
    }
}
