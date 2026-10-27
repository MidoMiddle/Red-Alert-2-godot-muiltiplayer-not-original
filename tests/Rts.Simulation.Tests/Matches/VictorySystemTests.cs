using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Replays;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Matches;

/// <summary>
/// Victory and defeat (docs/implementation/M5_first_bot.md, 4.8, step 10): who loses in the normal and the short game, when it is
/// checked, what the defeated leave, when the match ends, surrender, and the commands of the defeated; one who left a network match
/// (M9 step 10).
/// </summary>
public sealed class VictorySystemTests
{
    private static readonly PlayerId Person = new(1);
    private static readonly PlayerId Bot = new(2);

    private static GameSimulation Start(MatchSetup? setup = null) =>
        new(SimulationSettings.ForMatch(setup ?? MatchSetupTests.OneOnOne(), new MapGrid(64, 64)), TestRules.Standard);

    private static MatchSetup ShortGame() =>
        MatchSetupTests.OneOnOne() with { Options = new MatchOptions(ShortGame: true) };

    /// <summary>Kills the units of <paramref name="player"/> that <paramref name="which"/> picks, out of the world at once.</summary>
    private static void Kill(GameSimulation simulation, PlayerId player, Func<Unit, bool>? which = null)
    {
        foreach (var unit in simulation.World.Entities.OfType<Unit>().Where(unit => unit.Owner == player && (which?.Invoke(unit) ?? true)).ToList())
        {
            unit.MarkDead();
        }

        simulation.World.RemoveDead();
    }

    /// <summary>Plays until <paramref name="tick"/> has been played; the events of all those ticks. Tick 0 is a check too.</summary>
    private static List<SimEvent> PlayThrough(GameSimulation simulation, int tick)
    {
        var events = new List<SimEvent>();
        while (simulation.CurrentTick <= tick)
        {
            simulation.Step();
            events.AddRange(simulation.LastTickEvents);
        }

        return events;
    }

    private static bool Owns(GameWorld world, PlayerId player) => world.Entities.Any(entity => entity.IsAlive && entity.Owner == player);

    [Fact]
    public void In_the_normal_game_units_keep_a_player_in_and_with_nothing_left_they_lose_at_the_next_check()
    {
        var simulation = Start();
        Kill(simulation, Bot, unit => unit.Type == UnitType.Mcv);
        PlayThrough(simulation, 30);
        Assert.Null(simulation.World.Player(Bot).DefeatedTick);

        Kill(simulation, Bot);
        PlayThrough(simulation, 44);
        Assert.Null(simulation.World.Player(Bot).DefeatedTick);

        var events = PlayThrough(simulation, 45);
        Assert.Equal(45, simulation.World.Player(Bot).DefeatedTick);
        Assert.Contains(new PlayerDefeated(45, Bot), events);
        Assert.Null(simulation.World.Player(Person).DefeatedTick);
    }

    [Fact]
    public void Walls_keep_nobody_in_and_fall_with_their_owner()
    {
        var simulation = Start();
        PlayThrough(simulation, 0);
        Kill(simulation, Bot);
        var wall = simulation.World.CreateBuilding(Bot, TestRules.BuildingDef("wall"), new CellPos(40, 40), ready: true, null);

        PlayThrough(simulation, 15);

        Assert.Equal(15, simulation.World.Player(Bot).DefeatedTick);
        Assert.False(wall.IsAlive);
        Assert.False(simulation.World.Map.HasBuilding(new CellPos(40, 40)));
    }

    [Fact]
    public void In_the_short_game_only_buildings_and_the_mcv_keep_a_player_in_and_the_units_left_are_destroyed()
    {
        var simulation = Start(ShortGame());
        PlayThrough(simulation, 0);
        // The person keeps only the MCV: still in. The bot keeps only its conscripts: out.
        Kill(simulation, Person, unit => unit.Type != UnitType.Mcv);
        Kill(simulation, Bot, unit => unit.Type == UnitType.Mcv);
        var conscripts = simulation.World.Entities.OfType<Unit>().Where(unit => unit.Owner == Bot).Select(unit => unit.Id).ToList();

        var events = PlayThrough(simulation, 15);

        Assert.Null(simulation.World.Player(Person).DefeatedTick);
        Assert.Equal(15, simulation.World.Player(Bot).DefeatedTick);
        Assert.False(Owns(simulation.World, Bot));
        var destroyed = events.OfType<EntityDestroyed>().Where(death => conscripts.Contains(death.Entity)).ToList();
        Assert.Equal(conscripts.Count, destroyed.Count);
        Assert.All(destroyed, death => Assert.Equal(DeathCause.Defeat, death.Cause));
    }

    [Fact]
    public void One_against_one_the_match_ends_with_the_one_left_and_is_checked_no_more()
    {
        var simulation = Start();
        PlayThrough(simulation, 0);
        Kill(simulation, Bot);

        var events = PlayThrough(simulation, 15);

        var ended = Assert.Single(events.OfType<MatchEnded>());
        Assert.Equal([Person], ended.Winners.AsEnumerable());
        Assert.Equal(new MatchResult(15, [Person]), simulation.World.Result, new ResultComparer());

        // After the end nobody loses any more.
        Kill(simulation, Person);
        PlayThrough(simulation, 60);
        Assert.Null(simulation.World.Player(Person).DefeatedTick);
    }

    [Fact]
    public void Players_of_one_team_win_together_and_neutral_buildings_count_for_nobody()
    {
        MatchSetup setup = MatchSetupTests.OneOnOne() with
        {
            Players =
            [
                MatchSetupTests.Player(1, PlayerKind.Human, 10, 10) with { Team = 1 },
                MatchSetupTests.Player(2, PlayerKind.Ai, 50, 50) with { Team = 2 },
                MatchSetupTests.Player(3, PlayerKind.Ai, 50, 10) with { Team = 1 },
            ],
        };
        var simulation = Start(setup);
        simulation.World.CreateBuilding(PlayerId.Neutral, TestRules.BuildingDef("reactor"), new CellPos(30, 30), ready: true, null);
        PlayThrough(simulation, 0);

        // The ally falls: the match goes on.
        Kill(simulation, new PlayerId(3));
        PlayThrough(simulation, 15);
        Assert.Equal(15, simulation.World.Player(new PlayerId(3)).DefeatedTick);
        Assert.Null(simulation.World.Result);

        // The enemy falls: the person wins, the fallen ally not among the winners.
        Kill(simulation, Bot);
        PlayThrough(simulation, 30);
        Assert.Equal([Person], simulation.World.Result!.Winners.AsEnumerable());
    }

    [Fact]
    public void Both_falling_on_one_check_is_a_draw()
    {
        var simulation = Start();
        Kill(simulation, Person);
        Kill(simulation, Bot);

        PlayThrough(simulation, 15);

        Assert.Empty(simulation.World.Result!.Winners);
    }

    [Fact]
    public void A_surrender_defeats_at_once_the_match_ends_at_the_check_and_the_defeated_give_no_more_commands()
    {
        var simulation = Start();
        var units = simulation.World.Entities.OfType<Unit>().Where(unit => unit.Owner == Bot).Select(unit => unit.Id).ToImmutableArray();
        simulation.Enqueue(new SurrenderCommand(Bot, 5, 0));
        simulation.Enqueue(new SurrenderCommand(Bot, 6, 1));
        simulation.Enqueue(new MoveCommand(Bot, 6, 2, units, new CellPos(30, 30).Center));

        var events = PlayThrough(simulation, 6);

        Assert.Equal(5, simulation.World.Player(Bot).DefeatedTick);
        Assert.Contains(new PlayerDefeated(5, Bot), events);
        Assert.False(Owns(simulation.World, Bot));
        Assert.Null(simulation.World.Result);
        Assert.Equal(2, simulation.RefusedCommands.Count(command => command.Player == Bot));

        PlayThrough(simulation, 15);
        Assert.Equal([Person], simulation.World.Result!.Winners.AsEnumerable());

        // After the end the winner cannot surrender either.
        simulation.Enqueue(new SurrenderCommand(Person, 20, 0));
        PlayThrough(simulation, 20);
        Assert.Null(simulation.World.Player(Person).DefeatedTick);
    }

    [Fact]
    public void Outside_a_match_nobody_loses_and_nobody_can_surrender()
    {
        var simulation = new GameSimulation(new SimulationSettings(7, new MapGrid(64, 64)), TestRules.Standard);
        simulation.Enqueue(new SurrenderCommand(Person, 0, 0));

        PlayThrough(simulation, 30);

        Assert.Single(simulation.RefusedCommands);
        Assert.All(simulation.World.Players, player => Assert.Null(player.DefeatedTick));
        Assert.Null(simulation.World.Result);
    }

    [Fact]
    public void A_match_with_a_surrender_plays_back_from_its_replay()
    {
        var simulation = Start();
        var recorder = new ReplayRecorder(simulation);
        simulation.Enqueue(new SurrenderCommand(Bot, 40, 0));
        for (var tick = 0; tick < 120; tick++)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        var replay = ReplayFormat.Read(ReplayFormat.Write(recorder.Build()), TestRules.Standard.MapTypes);

        Assert.Contains(replay.Commands, command => command is SurrenderCommand);
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(replay, TestRules.Standard).Status);
    }

    [Fact]
    public void One_who_left_is_defeated_but_what_is_theirs_stays_idle()
    {
        var simulation = Start();
        simulation.Enqueue(new SpawnTestUnitCommand(Person, 0, 0, UnitType.HeavyTank, new CellPos(30, 30).Center));
        simulation.Enqueue(new SpawnTestUnitCommand(Bot, 0, 0, UnitType.HeavyTank, new CellPos(33, 30).Center));
        PlayThrough(simulation, 0);
        var owned = simulation.World.Entities.Count(entity => entity.IsAlive && entity.Owner == Bot);
        var units = simulation.World.Entities.OfType<Unit>().Where(unit => unit.Owner == Bot).Select(unit => unit.Id).ToImmutableArray();
        simulation.Enqueue(new PlayerLeftCommand(Bot, 1, 0));
        simulation.Enqueue(new MoveCommand(Bot, 2, 0, units, new CellPos(20, 20).Center));

        var events = PlayThrough(simulation, 1);

        Assert.Equal(1, simulation.World.Player(Bot).DefeatedTick);
        Assert.Contains(new PlayerDefeated(1, Bot), events);
        Assert.Equal(owned, simulation.World.Entities.Count(entity => entity.IsAlive && entity.Owner == Bot));

        // Its tank does not fire at the enemy tank beside it, and its orders are refused; the match ends with the one left standing.
        events = PlayThrough(simulation, 15);
        var tank = simulation.World.Entities.OfType<Unit>().Single(unit => unit.Owner == Person && unit.Type == UnitType.HeavyTank);
        Assert.Equal(tank.MaxHp, tank.Hp);
        Assert.All(simulation.World.Entities.OfType<Unit>().Where(unit => unit.Owner == Bot), unit => Assert.Null(unit.Target));
        Assert.Contains(simulation.RefusedCommands, command => command is MoveCommand { Player.Value: 2 });
        Assert.Equal([Person], simulation.World.Result!.Winners.AsEnumerable());
    }

    [Fact]
    public void One_who_left_cannot_leave_twice_nor_after_the_end()
    {
        var simulation = Start();
        simulation.Enqueue(new PlayerLeftCommand(Bot, 1, 0));
        simulation.Enqueue(new PlayerLeftCommand(Bot, 2, 0));
        PlayThrough(simulation, 15);
        simulation.Enqueue(new PlayerLeftCommand(Person, 16, 0));
        PlayThrough(simulation, 16);

        Assert.Equal(2, simulation.RefusedCommands.Count);
        Assert.Null(simulation.World.Player(Person).DefeatedTick);
    }

    [Fact]
    public void A_match_with_a_player_who_left_plays_back_from_its_replay()
    {
        var simulation = Start();
        var recorder = new ReplayRecorder(simulation);
        simulation.Enqueue(new PlayerLeftCommand(Bot, 40, 0));
        for (var tick = 0; tick < 120; tick++)
        {
            simulation.Step();
            recorder.AfterStep();
        }

        var replay = ReplayFormat.Read(ReplayFormat.Write(recorder.Build()), TestRules.Standard.MapTypes);

        Assert.Contains(replay.Commands, command => command is PlayerLeftCommand);
        Assert.Equal(ReplayStatus.Match, ReplayPlayer.Verify(replay, TestRules.Standard).Status);
    }

    /// <summary>A result by its tick and winners: the record compares its array by reference.</summary>
    private sealed class ResultComparer : IEqualityComparer<MatchResult?>
    {
        public bool Equals(MatchResult? x, MatchResult? y) =>
            x is not null && y is not null && x.Tick == y.Tick && x.Winners.SequenceEqual(y.Winners);

        public int GetHashCode(MatchResult? obj) => obj?.Tick ?? 0;
    }
}
