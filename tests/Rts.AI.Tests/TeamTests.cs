using System.Collections.Immutable;
using Rts.AI.Team;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// The team of a bot (docs/implementation/M8_ai_difficulty.md, 4.8, step 11): the board of the bots of a team — the attack of the team
/// by strength and player, the answers, what grows old — and the coordinator that reads it and the people of the team for the squads.
/// </summary>
public sealed class TeamTests
{
    private static readonly PlayerId Me = Scene.Me;
    private static readonly PlayerId Ally = new(3);
    private static readonly PlayerId Third = new(4);
    private static readonly CellPos Target = new(40, 40);

    private static TeamBlackboard Board() => new([Me, Ally, Third]);

    [Fact]
    public void The_attack_of_the_team_is_the_strongest_with_those_who_join_and_of_two_as_strong_that_of_the_lower_player()
    {
        var board = Board();
        Assert.Null(board.TeamAttack());

        board.Announce(new AttackIntent(Ally, Target, 500, 0));
        board.Announce(new AttackIntent(Third, new CellPos(10, 50), 700, 0));
        Assert.Equal(Third, board.TeamAttack()!.Player);

        board.Answer(Ally, Me, joins: true, 300);
        Assert.Equal(Ally, board.TeamAttack()!.Player);

        board.Answer(Ally, Me, joins: false, 300);
        board.Announce(new AttackIntent(Ally, Target, 700, 0));
        Assert.Equal(Ally, board.TeamAttack()!.Player);
    }

    [Fact]
    public void An_attack_waits_for_every_other_bot_and_a_new_one_asks_anew()
    {
        var board = Board();
        board.Announce(new AttackIntent(Ally, Target, 500, 0));
        board.Answer(Ally, Me, joins: true, 100);
        Assert.False(board.AllAnswered(Ally));

        board.Answer(Ally, Third, joins: false, 0);
        Assert.True(board.AllAnswered(Ally));

        board.Announce(new AttackIntent(Ally, Target, 600, 10));
        Assert.False(board.Answered(Ally, Me));

        board.Withdraw(Ally);
        Assert.Null(board.TeamAttack());
    }

    [Fact]
    public void What_is_older_than_a_minute_is_forgotten()
    {
        var board = Board();
        board.Announce(new AttackIntent(Ally, Target, 500, 100));
        board.Request(new DefenseRequest(Third, Target, 400, 100));

        board.Forget(100 + TeamBlackboard.StaleTicks);
        Assert.NotNull(board.TeamAttack());
        Assert.Single(board.Requests);

        board.Forget(101 + TeamBlackboard.StaleTicks);
        Assert.Null(board.TeamAttack());
        Assert.Empty(board.Requests);
    }

    /// <summary>A turn of the coordinator of <see cref="Scene.Me"/> with its squad at home of <paramref name="units"/> out of 8 needed.</summary>
    private static AiPlan Coordinate(Scene scene, TeamCoordinator coordinator, int units, AiThreat? threat = null)
    {
        scene.Look();
        var context = scene.Context();
        (context.Plan.GatheringUnits, context.Plan.GatheringStrength, context.Plan.NeededUnits) = (units, units * 100, 8);
        context.Plan.Threat = threat;
        coordinator.Update(context);
        return context.Plan;
    }

    [Fact]
    public void A_bot_answers_the_attack_of_an_ally_and_joins_it_with_half_the_units_it_needs()
    {
        var board = Board();
        var coordinator = new TeamCoordinator(board, [], Scene.Settings);
        board.Announce(new AttackIntent(Ally, Target, 500, 0));
        var scene = new Scene();

        Assert.Null(Coordinate(scene, coordinator, units: 3).JoinAttack);
        Assert.True(board.Answered(Ally, Me));

        Assert.Equal(Target, Coordinate(scene, coordinator, units: 4).JoinAttack);
        Assert.Equal(Ally, board.TeamAttack()!.Player);
    }

    [Fact]
    public void A_bot_whose_base_is_threatened_neither_joins_nor_helps()
    {
        var board = Board();
        var coordinator = new TeamCoordinator(board, [], Scene.Settings);
        board.Announce(new AttackIntent(Ally, Target, 500, 0));
        board.Request(new DefenseRequest(Ally, new CellPos(56, 8), 400, 0));

        var plan = Coordinate(new Scene(), coordinator, units: 8, threat: new AiThreat(new CellPos(8, 8), new CellPos(8, 8), 300));

        Assert.Null(plan.JoinAttack);
        Assert.Null(plan.HelpAt);
        Assert.False(board.Answered(Ally, Me));
    }

    [Fact]
    public void A_bot_helps_an_ally_within_reach_of_its_start_and_not_itself()
    {
        var board = Board();
        var coordinator = new TeamCoordinator(board, [], Scene.Settings);
        var scene = new Scene();
        board.Request(new DefenseRequest(Me, new CellPos(8, 8), 400, 0));
        Assert.Null(Coordinate(scene, coordinator, units: 0).HelpAt);

        board.Request(new DefenseRequest(Third, new CellPos(8 + TeamCoordinator.HelpCells + 1, 8), 400, 0));
        Assert.Null(Coordinate(scene, coordinator, units: 0).HelpAt);

        board.Request(new DefenseRequest(Ally, new CellPos(56, 8), 400, 0));
        Assert.Equal(new CellPos(56, 8), Coordinate(scene, coordinator, units: 0).HelpAt);
    }

    /// <summary>
    /// A match of a scene: the bot (1) and a person (3) are a team against a person (2); the bot's start is the first corner, the ally's
    /// the third (56, 8).
    /// </summary>
    private static Scene WithPerson(out TeamCoordinator coordinator)
    {
        MatchPlayer Player(int slot, PlayerKind kind, int team, CellPos start) =>
            new(slot, kind, AiDifficulty.Medium, "union", slot - 1, team, slot, start);

        var match = new MatchSetup(
            "clear",
            7,
            new MatchOptions(),
            [Player(1, PlayerKind.Ai, 1, Scene.Corners[0]), Player(2, PlayerKind.Human, 2, Scene.Corners[1]), Player(3, PlayerKind.Human, 1, Scene.Corners[2])],
            Scene.Corners);
        coordinator = new TeamCoordinator(null, [new HumanAlly(Ally, Scene.Corners[2])], Scene.Settings);
        return new Scene(match: match);
    }

    [Fact]
    public void Armed_enemies_at_the_buildings_of_a_person_of_the_team_call_the_bot_to_help()
    {
        var scene = WithPerson(out var coordinator);
        scene.Build(Ally, "reactor", 54, 12);
        Assert.Null(Coordinate(scene, coordinator, units: 0).HelpAt);

        scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 53, 10);
        scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 55, 12);

        Assert.Equal(new CellPos(54, 11), Coordinate(scene, coordinator, units: 0).HelpAt);
    }

    [Fact]
    public void Six_armed_units_of_a_person_of_the_team_on_the_move_out_of_their_base_are_an_attack_the_bot_joins()
    {
        var scene = WithPerson(out var coordinator);
        var tanks = new List<EntityId>();
        for (var i = 0; i < TeamCoordinator.HumanAttackUnits; i++)
        {
            tanks.Add(scene.Spawn(Ally, UnitType.HeavyTank, 30 + i, 30).Id);
        }

        Assert.Null(Coordinate(scene, coordinator, units: 4).JoinAttack);

        scene.Simulation.Enqueue(new MoveCommand(Ally, scene.Simulation.CurrentTick, 0, [.. tanks.Skip(1)], Scene.Corners[1].Center));
        scene.Simulation.Step();
        Assert.Null(Coordinate(scene, coordinator, units: 4).JoinAttack);

        scene.Simulation.Enqueue(new MoveCommand(Ally, scene.Simulation.CurrentTick, 1, [.. tanks], Scene.Corners[1].Center));
        scene.Simulation.Step();
        Assert.Null(Coordinate(scene, coordinator, units: 3).JoinAttack);
        Assert.NotNull(Coordinate(scene, coordinator, units: 4).JoinAttack);
    }
}
