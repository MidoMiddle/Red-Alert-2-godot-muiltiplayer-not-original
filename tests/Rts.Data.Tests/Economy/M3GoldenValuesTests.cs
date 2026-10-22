using Rts.Data.Maps;
using Rts.Data.Tests.Maps;
using Rts.MapTool;
using Rts.Simulation;
using Rts.Simulation.Replays;

namespace Rts.Data.Tests.Economy;

/// <summary>
/// The golden values of M3 (docs/implementation/M3_base_economy.md, section 6, «Детерминизм»): the end-to-end run on game/data and
/// Frozen Crossing must come out the same bit for bit on every platform of CI, and its replay must play back the same. The hash of
/// game/data itself is pinned by <see cref="Rules.GameDataTests"/>.
///
/// Rule, as for the golden values of M0: a value changes only together with a deliberate change of the rules or the data, in the same
/// commit, with the reason in the comment. A value that changes "by itself" is a determinism bug.
/// </summary>
public sealed class M3GoldenValuesTests
{
    private static readonly Lazy<EconomyReport> Run = new(() =>
        EconomyRun.Play(
            RtsMapReader.Read(File.ReadAllText(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps", "frozen-crossing.rtsmap"))),
            GameDataFiles.Rules,
            record: true));

    [Fact]
    public void The_state_after_five_minutes_of_the_run()
    {
        var fifth = Run.Value.Minutes.Single(minute => minute.Minute == 5);

        // M3 step 15, rules v25, game/data 314b59aebbc04ca9.
        Assert.Equal((6322, 0x8ea7aa9cd42a517aUL), (fifth.Credits, fifth.Checksum));
        // The first tank at 4:16 (tick 3851).
        Assert.Equal(3851, Run.Value.TankTicks[0]);
    }

    [Fact]
    public void The_replay_of_the_run_plays_back_the_same()
    {
        var replay = ReplayFormat.Read(ReplayFormat.Write(Run.Value.Replay!));

        var verification = ReplayPlayer.Verify(replay, GameDataFiles.Rules);

        Assert.Equal(ReplayStatus.Match, verification.Status);
        Assert.Equal(SimulationVersion.Current, replay.SimVersion);
        Assert.Equal(GameDataFiles.Current.Hash, replay.DataHash);
    }
}
