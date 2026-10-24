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
        // M4 step 2, rules v26: the hit points of the units are hashed; the run is the same, the credits too.
        // M4 step 3, rules v27: the targets and weapons of the units are hashed; the run is the same.
        // M4 step 7, rules v31: the stance and the anchor of every unit are hashed; the run is the same, the credits too.
        // M4 step 11, rules v35: what every player sees is hashed; the run is the same, the credits too.
        // M4 step 13, rules v36: the tick of the last attack notice of every player is hashed; the run is the same, the credits too.
        // M5 step 10, rules v38: the tick every player lost the match on is hashed (none, no match); the run is the same, the credits too.
        // M8 step 2, rules v39: the bonuses of every player and the hundredths of income owed are hashed (none); the run is the same, the credits too.
        // M8 step 10b, the same rules: Frozen Crossing has its drills — an ore drill at home, a gem drill in the middle — so the fields
        // grow back while the harvester works them: 650 more credits by minute 5.
        // The Alliance, rules v42: the same run and the same credits; the checksum changes, as the numbers of the buildings and units in
        // the data do — those of the Alliance sort before the Union's.
        // Every unit of both factions, rules v43: the same credits again; the numbers of the units in the data move once more.
        // Stage 2a, rules v44: the naval queue is hashed; the run and its credits are the same.
        // Stage 2b, rules v45: the aircraft queue is hashed too; the run and its credits are the same.
        // Frozen Crossing redrawn on a 90x90 grid with a playable area (M7): another field, another road home; 1000 credits fewer by
        // minute 5.
        // Stage 2e, rules v48: the same run and credits; the soldiers inside buildings and where soldiers go are hashed.
        // Stage 3a, rules v49: the same run and credits; the temporal beam that holds an entity is hashed.
        // Stage 3b, rules v50: the same run and credits; bombs, the toxin on the ground, deploying, jumps and blackouts are hashed.
        // Stage 3d, rules v52: the same run and credits; passengers and parasites are hashed.
        // Stage 3f, rules v54: the same run and credits; the ammo of every unit is hashed.
        // Rules v55: the same run and credits; a carrier's Hornets are hashed.
        // Stage 2d, rules v56: the same run and credits; the charges, the cover and the storms are hashed.
        // Rules v57 (loose ends, stage 1): the same run and credits; the paradrop plane and the derrick's income are hashed.
        // Rules v58 (loose ends, stage 2): the same run and credits; Frozen Crossing has a repair hut by each of its bridges.
        Assert.Equal((5972, 0x3a7fe02ff6bb18bUL), (fifth.Credits, fifth.Checksum));
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
