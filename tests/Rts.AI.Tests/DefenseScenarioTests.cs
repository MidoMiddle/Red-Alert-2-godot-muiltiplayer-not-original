using Rts.AI.Squads;
using Rts.Data.Tests;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Math;
using Rts.Simulation.Replays;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// The defense of the «Medium» bot in a match by the real data on Frozen Crossing (docs/implementation/M5_first_bot.md, step 9): once its
/// first wave is out on the way, the person gets four tanks behind the bot's base and sends them at its yard. The bot goes into the
/// defense, calls the wave back, destroys the tanks and, ten seconds after the last, goes back to what it did.
/// </summary>
[Trait("Category", "Slow")]
public sealed class DefenseScenarioTests
{
    private const int Tanks = 4;

    /// <summary>
    /// What happened: when the base was first hit, when the bot went into the defense and when it called the wave back; the units of
    /// the wave; when the last tank fell and when the defense ended; how far from the start the farthest unit of the wave stood when the
    /// last tank fell.
    /// </summary>
    private sealed record Run(BotMatch Match, int Hit, int Defended, int Recalled, List<EntityId> WaveUnits, int Fallen, int Ended, int Farthest);

    private static readonly Lazy<Run> Once = new(Play);

    private static Run Play()
    {
        var match = new BotMatch();
        var start = match.Setup.Players.First(player => player.Id == BotMatch.Bot).Start;
        var person = match.Setup.Players.First(player => player.Id != BotMatch.Bot).Start;
        // Twelve cells behind the bot's base, on the side away from the person.
        var behind = new CellPos(IntMath.Min(start.X + 12 * IntMath.Sign(start.X - person.X), match.World.Map.Width - 5), start.Y);
        var (given, hit, defended, recalled, fallen, ended, farthest) = (-1, -1, -1, -1, -1, -1, 0);
        Squad? wave = null;
        var units = new List<EntityId>();
        while (match.Simulation.CurrentTick < 12 * 900)
        {
            match.Step();
            var tick = match.Simulation.CurrentTick;
            var plan = match.Brain!.Context!.Plan;
            if (given < 0 && plan.Squads.FirstOrDefault(squad => squad.State == SquadState.Moving && Distance(squad.Centre, start) >= 25) is { } far)
            {
                (wave, given) = (far, tick);
                units.AddRange(far.Units);
                match.GivePerson(UnitType.HeavyTank, Tanks, behind);
            }
            else if (given >= 0 && tick == given + 2)
            {
                match.PersonAttacks(UnitType.HeavyTank, start);
            }

            hit = hit < 0 && given >= 0 && match.World.Player(BotMatch.Bot).LastUnderAttack is { } last ? last : hit;
            defended = defended < 0 && plan.Strategy == StrategyState.Defending ? tick : defended;
            recalled = recalled < 0 && wave is { Recalled: true } ? tick : recalled;
            if (given >= 0 && tick > given + 2 && fallen < 0 && !PersonTanks(match).Any())
            {
                fallen = tick;
                farthest = units.Where(id => match.World.TryGet(id, out var unit) && unit.IsAlive)
                    .Select(id => { match.World.TryGet(id, out var unit); return Distance(((Unit)unit!).Position.ToCell(), start); })
                    .DefaultIfEmpty(0).Max();
            }

            ended = ended < 0 && defended >= 0 && plan.Strategy != StrategyState.Defending ? tick : ended;
        }

        return new Run(match, hit, defended, recalled, units, fallen, ended, farthest);
    }

    private static IEnumerable<Unit> PersonTanks(BotMatch match) =>
        match.World.Entities.OfType<Unit>().Where(unit => unit.IsAlive && unit.Owner != BotMatch.Bot && unit.Type == UnitType.HeavyTank);

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));

    [Fact]
    public void The_bot_defends_its_base_within_two_seconds_of_the_first_hit_and_calls_its_wave_back()
    {
        var run = Once.Value;

        Assert.True(run.Hit > 0, "the base was never hit");
        Assert.InRange(run.Defended - run.Hit, 0, 30);
        Assert.InRange(run.Recalled - run.Hit, 0, 30);
        Assert.Empty(run.Match.RefusedBotCommands);
    }

    [Fact]
    public void The_tanks_fall_the_wave_is_home_and_ten_seconds_later_the_defense_ends()
    {
        var run = Once.Value;

        Assert.True(run.Fallen > 0, "the tanks still stand");
        Assert.True(run.WaveUnits.Any(id => run.Match.World.TryGet(id, out var unit) && unit.IsAlive), "the whole wave fell");
        Assert.True(run.Farthest <= 2 * AiSettings.Medium.BaseCells, $"a unit of the wave {run.Farthest} cells from the start");
        Assert.InRange(run.Ended - run.Fallen, 1, AiSettings.Medium.ThreatClearTicks + 10);
        Assert.NotEqual(StrategyState.Defending, run.Match.Brain!.Context!.Plan.Strategy);
    }

    [Fact]
    public void A_match_with_a_defense_plays_the_same_twice_and_its_replay_with_the_bot_run_again()
    {
        var match = Once.Value.Match;
        var again = Play().Match;
        var replay = ReplayFormat.Read(ReplayFormat.Write(match.Recorder.Build()));

        Assert.Equal(StateHasher.Compute(match.World), StateHasher.Compute(again.World));
        Assert.Equal(
            ReplayStatus.Match,
            ReplayPlayer.VerifyWithBots(replay, GameDataFiles.Rules, player => MatchBots.Create(replay.Settings.Match!, player, GameDataFiles.Rules)).Status);
    }
}
