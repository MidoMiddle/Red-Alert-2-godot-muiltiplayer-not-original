using Rts.AI.Squads;
using Rts.Simulation.Hashing;

namespace Rts.AI.Tests;

/// <summary>
/// The snapshot of the debug overlay (docs/implementation/M5_first_bot.md, 4.13, step 13): what it shows of the bot on Frozen Crossing
/// with its first wave out, that it is a copy, and that asking for it changes nothing in the match.
/// </summary>
[Trait("Category", "Slow")]
public sealed class AiDebugSnapshotTests
{
    /// <summary>
    /// The match played until the first squad is out (about 9:40) and the strategist, which thinks every 3 s, has seen it: the snapshot
    /// then.
    /// </summary>
    private static readonly Lazy<(BotMatch Match, Debug.AiDebugSnapshot Snapshot)> WaveOut = new(() =>
    {
        var match = new BotMatch();
        while (match.Brain!.Context?.Plan is not { Strategy: StrategyState.Attacking } plan || !plan.Squads.Any(squad => squad.State == SquadState.Moving))
        {
            match.Step();
        }

        return (match, match.Brain.Snapshot()!);
    });

    [Fact]
    public void Before_its_first_turn_a_bot_has_nothing_to_show()
    {
        Assert.Null(new BotMatch().Brain!.Snapshot());
    }

    [Fact]
    public void With_its_wave_out_it_shows_the_attack_the_squads_the_plans_and_the_memory()
    {
        var (match, snapshot) = WaveOut.Value;
        var plan = match.Brain!.Context!.Plan;
        var memory = match.Brain.Context.Memory;

        Assert.Equal(StrategyState.Attacking, snapshot.Strategy);
        Assert.Equal(BotMatch.Bot, snapshot.Player);
        Assert.Equal(
            plan.Squads.Select(squad => (squad.Number, squad.State, squad.Units.Count, squad.TargetCell)),
            snapshot.Squads.Select(squad => (squad.Number, squad.State, squad.Units, squad.Target)));
        Assert.NotNull(snapshot.GatherPoint);
        Assert.NotNull(snapshot.Scout);
        Assert.Equal(match.World.Player(BotMatch.Bot).Credits, snapshot.Credits);
        Assert.True(snapshot.Harvesters > 0);
        Assert.NotNull(snapshot.LastPlacement);
        Assert.Equal(memory.EnemyStrength(match.Brain.Context.View), snapshot.EnemyStrength);
        Assert.Equal(memory.Buildings.Count, snapshot.EnemyBuildings.Length);

        var text = snapshot.ToText();
        Assert.Contains("Attacking", text);
        Assert.Contains("squad 1 Moving", text);
        Assert.Contains("credits ", text);
    }

    [Fact]
    public void A_snapshot_is_a_copy_the_bot_goes_on_without_it()
    {
        var match = new BotMatch();
        match.PlayTo(3);
        var before = match.Brain!.Snapshot()!;
        var text = before.ToText();

        match.PlayTo(4);

        Assert.Equal(text, before.ToText());
        Assert.NotEqual(before.Tick, match.Brain.Snapshot()!.Tick);
    }

    [Fact]
    public void Asking_for_the_snapshot_every_tick_changes_nothing_in_the_match()
    {
        var watched = new BotMatch();
        while (watched.Simulation.CurrentTick < 6 * 900)
        {
            watched.Step();
            _ = watched.Brain!.Snapshot()?.ToText();
        }

        var alone = new BotMatch().PlayTo(6);

        Assert.Equal(StateHasher.Compute(alone.World), StateHasher.Compute(watched.World));
    }
}
