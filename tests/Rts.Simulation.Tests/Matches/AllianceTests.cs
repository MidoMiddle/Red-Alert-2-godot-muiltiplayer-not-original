using Rts.Simulation.Combat;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Matches;

/// <summary>
/// Allies (docs/implementation/M8_ai_difficulty.md, 4.8, step 11): the players of one team of the match setup see what each other sees
/// and are not enemies — no unit picks an ally, an order to attack one is refused while a forced attack is not, a tank does not crush the
/// infantry of an ally, and a hit of an ally raises no «base under attack». Players 1 and 2 are a team, 3 is the enemy; the units stand
/// in the middle of the map, far from the starts.
/// </summary>
public sealed class AllianceTests
{
    private static readonly PlayerId First = new(1);
    private static readonly PlayerId Ally = new(2);
    private static readonly PlayerId Enemy = new(3);

    private static MatchSetup Teams() =>
        MatchSetupTests.OneOnOne() with
        {
            Players =
            [
                MatchSetupTests.Player(1, PlayerKind.Human, 10, 10) with { Team = 1 },
                MatchSetupTests.Player(2, PlayerKind.Ai, 50, 50) with { Team = 1 },
                MatchSetupTests.Player(3, PlayerKind.Human, 50, 10) with { Team = 2 },
            ],
        };

    private static GameSimulation Start() => new(SimulationSettings.ForMatch(Teams(), new MapGrid(64, 64)), TestRules.Standard);

    private static Unit Spawn(GameSimulation simulation, PlayerId owner, UnitType type, int x, int y) =>
        simulation.World.SpawnUnit(owner, type, new CellPos(x, y).Center);

    private static List<SimEvent> Play(GameSimulation simulation, int ticks)
    {
        var events = new List<SimEvent>();
        for (var i = 0; i < ticks; i++)
        {
            simulation.Step();
            events.AddRange(simulation.LastTickEvents);
        }

        return events;
    }

    [Fact]
    public void The_players_of_a_team_are_allies_and_alone_a_player_is_its_own_only()
    {
        var world = Start().World;
        Assert.Equal(0b011, world.AlliesOf(First));
        Assert.Equal(0b011, world.AlliesOf(Ally));
        Assert.Equal(0b100, world.AlliesOf(Enemy));
        Assert.True(world.AreAllies(First, Ally));
        Assert.True(world.AreAllies(First, First));
        Assert.False(world.AreAllies(First, Enemy));
        Assert.False(world.AreAllies(First, PlayerId.Neutral));

        var alone = new GameSimulation(SimulationSettings.ForMatch(MatchSetupTests.OneOnOne(), new MapGrid(64, 64)), TestRules.Standard).World;
        Assert.Equal(0b01, alone.AlliesOf(First));
        Assert.False(alone.AreAllies(First, Ally));
    }

    [Fact]
    public void A_player_sees_the_enemy_its_ally_sees()
    {
        var simulation = Start();
        Spawn(simulation, Ally, UnitType.HeavyTank, 30, 30);
        var conscript = Spawn(simulation, Enemy, UnitType.Conscript, 32, 30);
        conscript.Stance = Stance.HoldPosition;

        Play(simulation, 2);

        Assert.True(Vision.Sees(simulation.World, First, conscript));
        Assert.False(Vision.Sees(simulation.World, Enemy, simulation.World.Entities.OfType<Unit>().First(unit => unit.Owner == First)));
    }

    [Fact]
    public void A_unit_never_picks_an_ally_and_fires_at_the_enemy()
    {
        var simulation = Start();
        var tank = Spawn(simulation, First, UnitType.HeavyTank, 30, 30);
        var ally = Spawn(simulation, Ally, UnitType.Conscript, 31, 30);
        var enemy = Spawn(simulation, Enemy, UnitType.Conscript, 30, 34);
        (ally.Stance, enemy.Stance) = (Stance.HoldPosition, Stance.HoldPosition);
        var full = ally.Hp;

        Play(simulation, 90);

        Assert.Equal(full, ally.Hp);
        Assert.True(enemy.Hp < full, "the tank fired at the enemy next to the ally");
    }

    [Fact]
    public void An_order_to_attack_an_ally_is_refused_and_a_forced_attack_is_not()
    {
        var simulation = Start();
        var tank = Spawn(simulation, First, UnitType.HeavyTank, 30, 30);
        var ally = Spawn(simulation, Ally, UnitType.HeavyTank, 33, 30);
        var attack = new AttackCommand(First, 0, 0, [tank.Id], ally.Id);
        var forced = new ForceAttackCommand(First, 1, 1, [tank.Id], ally.Id, ally.Position);
        simulation.Enqueue(attack);
        simulation.Enqueue(forced);
        var full = ally.Hp;

        Play(simulation, 90);

        Assert.Contains(attack, simulation.RefusedCommands);
        Assert.DoesNotContain(forced, simulation.RefusedCommands);
        Assert.True(ally.Hp < full, "the forced attack hits the ally");
    }

    [Fact]
    public void A_tank_does_not_crush_the_infantry_of_an_ally()
    {
        var simulation = Start();
        var tank = Spawn(simulation, First, UnitType.HeavyTank, 25, 30);
        var engineer = Spawn(simulation, Ally, UnitType.Engineer, 30, 30);
        simulation.Enqueue(new MoveCommand(First, 0, 0, [tank.Id], new CellPos(35, 30).Center));

        var events = Play(simulation, 300);

        Assert.DoesNotContain(events, e => e is EntityDestroyed { Cause: DeathCause.Crushed });
        Assert.True(engineer.IsAlive);
    }

    [Fact]
    public void A_hit_of_an_ally_raises_no_alarm_and_one_of_the_enemy_does()
    {
        var simulation = Start();
        var world = simulation.World;
        var reactor = world.CreateBuilding(Ally, TestRules.BuildingDef("reactor"), new CellPos(30, 30), ready: true, null);
        var tank = Spawn(simulation, First, UnitType.HeavyTank, 30, 35);
        simulation.Enqueue(new ForceAttackCommand(First, 0, 0, [tank.Id], reactor.Id, Aim.CentreOf(reactor)));
        var full = reactor.Hp;

        var events = Play(simulation, 120);

        Assert.True(reactor.Hp < full, "the forced attack hits the building of the ally");
        Assert.DoesNotContain(events, e => e is UnderAttack);

        var enemy = Spawn(simulation, Enemy, UnitType.HeavyTank, 34, 30);
        simulation.Enqueue(new ForceAttackCommand(Enemy, simulation.CurrentTick, 0, [enemy.Id], reactor.Id, Aim.CentreOf(reactor)));
        Assert.Contains(Play(simulation, 120), e => e is UnderAttack { Player.Value: 2 });
    }
}
