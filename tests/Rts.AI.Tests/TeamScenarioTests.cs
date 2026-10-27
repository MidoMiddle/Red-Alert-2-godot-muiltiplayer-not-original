using Rts.AI.Modules;
using Rts.AI.Squads;
using Rts.Data.Tests;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Matches;
using Rts.Simulation.Replays;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// The team game on Four Corners (docs/implementation/M8_ai_difficulty.md, 4.8, step 11, «done when»): two allied bots set out together
/// — the «Easy» one with the «Medium» one, before its own window — and come to help each other; a bot reads a person of its team, who
/// writes nothing on the board: it helps at their base and joins their attack. The people of the other team do nothing but what a test
/// gives them.
/// </summary>
[Trait("Category", "Slow")]
public sealed class TeamScenarioTests
{
    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));

    /// <summary>The tick the first squad of the bot of <paramref name="slot"/> set out on, and to where.</summary>
    private static (int Tick, CellPos? Target)? SetOut(TeamMatch match, int slot) =>
        match.Bot(slot).Context?.Plan.Squads.FirstOrDefault(squad => squad.State is SquadState.Moving or SquadState.Fighting) is { } squad
            ? (match.Simulation.CurrentTick, squad.TargetCell)
            : null;

    private sealed record Together(TeamMatch Match, int Medium, int Easy, CellPos? MediumTarget, CellPos? EasyTarget);

    private static readonly Lazy<Together> Attack = new(() =>
    {
        var match = new TeamMatch(new Dictionary<int, AiDifficulty> { [1] = AiDifficulty.Medium, [2] = AiDifficulty.Easy });
        (int Tick, CellPos? Target)? medium = null, easy = null;
        while (match.Simulation.CurrentTick < 12 * 900 && (medium is null || easy is null))
        {
            match.Step();
            medium ??= SetOut(match, 1);
            easy ??= SetOut(match, 2);
        }

        return new Together(match, medium?.Tick ?? -1, easy?.Tick ?? -1, medium?.Target, easy?.Target);
    });

    [Fact]
    public void The_easy_ally_sets_out_with_the_medium_bot_within_thirty_seconds_and_before_its_own_window()
    {
        var run = Attack.Value;
        var easyWindow = MatchBots.SettingsOf(AiDifficulty.Easy, "union", GameDataFiles.Rules).FirstAttackFrom;

        Assert.True(run.Medium > 0, "the medium bot set out");
        Assert.True(run.Easy > 0, "the easy bot set out");
        Assert.True(IntMath.Abs(run.Easy - run.Medium) <= SquadManager.WaitForAllies, $"set out at {run.Medium} and {run.Easy}");
        Assert.True(run.Easy < easyWindow, $"the easy bot set out at {run.Easy}, its own window opens at {easyWindow}");
        Assert.True(Distance(run.MediumTarget!.Value, run.EasyTarget!.Value) <= 16, $"targets {run.MediumTarget} and {run.EasyTarget}");
    }

    [Fact]
    public void A_match_of_two_teams_plays_back_with_the_bots_run_again()
    {
        var match = Attack.Value.Match;
        var replay = ReplayFormat.Read(ReplayFormat.Write(match.Recorder.Build()), GameDataFiles.Rules.MapTypes);

        Assert.Equal(
            ReplayStatus.Match,
            ReplayPlayer.VerifyWithBots(replay, GameDataFiles.Rules, MatchBots.For(replay.Settings.Match!, GameDataFiles.Rules)).Status);
    }

    /// <summary>
    /// At 5:00 the enemy of <paramref name="enemySlot"/> gets six tanks at the base of <paramref name="attacked"/> and attacks it; plays
    /// two minutes more. The squad the bot of <paramref name="helper"/> sent to help, as it went, its army then, and how near the
    /// attacked start its units came.
    /// </summary>
    private static (Squad? Help, int HelpUnits, int Army, int Nearest) Helps(TeamMatch match, int attacked, int helper, int enemySlot = 3)
    {
        while (match.Simulation.CurrentTick < 5 * 900)
        {
            match.Step();
        }

        var start = match.Start(attacked);
        var towards = match.Start(enemySlot);
        var near = new CellPos(start.X + IntMath.Sign(towards.X - start.X) * 8, start.Y + IntMath.Sign(towards.Y - start.Y) * 8);
        match.Give(enemySlot, UnitType.HeavyTank, 6, near);
        match.Step();
        match.AttackMove(enemySlot, start);

        Squad? help = null;
        var (units, army, nearest) = (0, 0, int.MaxValue);
        while (match.Simulation.CurrentTick < 7 * 900)
        {
            match.Step();
            var plan = match.Bot(helper).Context!.Plan;
            if (help is null && plan.Squads.FirstOrDefault(squad => squad.HelpCell is not null) is { } sent)
            {
                (help, units, army) = (sent, sent.Units.Count, plan.Squads.Sum(squad => squad.Units.Count));
            }

            if (help is not null)
            {
                foreach (var id in help.Units)
                {
                    if (match.World.TryGet(id, out var unit))
                    {
                        nearest = IntMath.Min(nearest, Distance(((Unit)unit).Position.ToCell(), start));
                    }
                }
            }
        }

        return (help, units, army, nearest);
    }

    [Fact]
    public void An_allied_bot_sends_at_most_two_fifths_of_its_army_to_the_base_of_its_ally_under_attack()
    {
        var match = new TeamMatch(new Dictionary<int, AiDifficulty> { [1] = AiDifficulty.Medium, [2] = AiDifficulty.Medium });

        var (help, units, army, nearest) = Helps(match, attacked: 1, helper: 2);

        Assert.NotNull(help);
        Assert.InRange(units, 1, army * SquadManager.HelpPercent / 100);
        Assert.True(nearest <= 20, $"the help came {nearest} cells near the attacked start");
    }

    [Fact]
    public void A_bot_comes_to_help_at_the_base_of_a_person_of_its_team()
    {
        var match = new TeamMatch(new Dictionary<int, AiDifficulty> { [2] = AiDifficulty.Medium });
        match.GiveBase(1);

        var (help, units, _, nearest) = Helps(match, attacked: 1, helper: 2);

        Assert.NotNull(help);
        Assert.True(units > 0);
        Assert.True(nearest <= 20, $"the help came {nearest} cells near the person's start");
    }

    [Fact]
    public void A_bot_joins_the_attack_of_eight_tanks_of_a_person_of_its_team_before_its_own_window()
    {
        var match = new TeamMatch(new Dictionary<int, AiDifficulty> { [2] = AiDifficulty.Medium });
        var window = match.Bot(2).Settings.FirstAttackFrom;
        while (match.Simulation.CurrentTick < 4 * 900)
        {
            match.Step();
        }

        match.Give(1, UnitType.HeavyTank, 8, match.Start(1));
        match.Step();
        match.AttackMove(1, match.Start(4));

        (int Tick, CellPos? Target)? setOut = null;
        while (match.Simulation.CurrentTick < window && setOut is null)
        {
            match.Step();
            setOut = SetOut(match, 2);
        }

        Assert.True(setOut is not null, $"the bot did not set out before its window at {window}");
    }
}
