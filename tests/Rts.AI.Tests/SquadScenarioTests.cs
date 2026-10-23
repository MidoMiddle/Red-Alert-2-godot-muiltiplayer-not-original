using Rts.AI.Squads;
using Rts.Data.Tests;
using Rts.Simulation.Hashing;
using Rts.Simulation.Math;
using Rts.Simulation.Replays;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// The squads of the «Medium» bot in a match by the real data on Frozen Crossing (docs/implementation/M5_first_bot.md, step 8): a squad
/// gathers at its base, sets out and destroys the units of a person who does nothing; against a person who gets six tanks as it sets
/// out it is beaten, turns back and its survivors join the next squad.
/// </summary>
public sealed class SquadScenarioTests
{
    private static readonly PlayerId Person = new(1);

    /// <summary>
    /// What happened in a match: when the first squad set out and with how many units, how near the person's start the bot's army came
    /// before (the scout aside), and — when the person got tanks — the units of the squad as it turned back, beaten, and when.
    /// </summary>
    private sealed record Run(BotMatch Match, int SetOut, int Units, int NearestBefore, int? Beaten, List<EntityId> Survivors, SquadState? Joined);

    private static readonly Lazy<Run> Attack = new(() => Play(minutes: 12, tanks: false));

    private static readonly Lazy<Run> Defended = new(() => Play(minutes: 13, tanks: true));

    private static Run Play(int minutes, bool tanks)
    {
        var match = new BotMatch();
        var start = match.Setup.Players.First(player => player.Id == Person).Start;
        var (setOut, units, nearest) = (-1, 0, int.MaxValue);
        int? beaten = null;
        var survivors = new List<EntityId>();
        Squad? back = null;
        SquadState? joined = null;
        while (match.Simulation.CurrentTick < minutes * 900)
        {
            match.Step();
            var plan = match.Brain!.Context!.Plan;
            if (setOut < 0)
            {
                foreach (var unit in match.UnitsOfBot(unit => unit.Archetype.Armed && unit.Harvester is null && unit.Id != plan.Scout))
                {
                    var cell = unit.Position.ToCell();
                    nearest = IntMath.Min(nearest, IntMath.Max(IntMath.Abs(cell.X - start.X), IntMath.Abs(cell.Y - start.Y)));
                }

                if (plan.Squads.FirstOrDefault(squad => squad.State == SquadState.Moving) is { } moving)
                {
                    (setOut, units) = (match.Simulation.CurrentTick, moving.Units.Count);
                    if (tanks)
                    {
                        match.GivePerson(UnitType.HeavyTank, 6);
                    }
                }
            }

            if (beaten is null && plan.Squads.FirstOrDefault(squad => squad.Beaten) is { } turned)
            {
                (beaten, back) = (match.Simulation.CurrentTick, turned);
                survivors.AddRange(turned.Units);
            }

            // The turn the beaten squad is gone: what the squad its survivors are in then does.
            if (back is not null && !plan.Squads.Contains(back))
            {
                joined = plan.Squads.FirstOrDefault(squad => survivors.Any(squad.Units.Contains))?.State;
                back = null;
            }
        }

        return new Run(match, setOut, units, nearest, beaten, survivors, joined);
    }

    [Fact]
    public void A_squad_gathers_at_the_base_sets_out_and_destroys_the_units_of_the_person()
    {
        var run = Attack.Value;
        var match = run.Match;

        Assert.InRange(run.SetOut, 1, 11 * 900);
        Assert.True(run.Units >= AiSettings.Medium.MinAttackArmy, $"{run.Units} units set out");
        // Until then the army kept at home, on its own half of the map: no unit went to the person alone.
        Assert.True(run.NearestBefore > 30, $"{run.NearestBefore} cells from the person's start");
        Assert.DoesNotContain(match.World.Entities.OfType<Unit>(), unit => unit.IsAlive && unit.Owner == Person);
        Assert.Empty(match.RefusedBotCommands);
    }

    [Fact]
    public void Beaten_it_turns_back_and_its_survivors_join_the_squad_gathering_at_home()
    {
        var run = Defended.Value;
        var plan = run.Match.Brain!.Context!.Plan;

        Assert.NotNull(run.Beaten);
        Assert.NotEmpty(run.Survivors);
        Assert.DoesNotContain(plan.Squads, squad => squad.Beaten);
        // As the beaten squad went, its survivors were in the squad gathering at home.
        Assert.True(run.Joined is SquadState.Gathering or SquadState.Ready, $"the survivors joined a squad {run.Joined?.ToString() ?? "-"}");
        Assert.Empty(run.Match.RefusedBotCommands);
    }

    [Fact]
    public void A_match_with_squads_plays_the_same_twice_and_its_replay_with_the_bot_run_again()
    {
        var match = Defended.Value.Match;
        var again = Play(minutes: 13, tanks: true).Match;
        var replay = ReplayFormat.Read(ReplayFormat.Write(match.Recorder.Build()));

        Assert.Equal(StateHasher.Compute(match.World), StateHasher.Compute(again.World));
        Assert.Equal(
            ReplayStatus.Match,
            ReplayPlayer.VerifyWithBots(replay, GameDataFiles.Rules, player => MatchBots.Create(replay.Settings.Match!, player, GameDataFiles.Rules)).Status);
    }
}
