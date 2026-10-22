using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Combat;

/// <summary>
/// Crushing infantry (docs/implementation/M4_combat.md, 4.13): a crushing vehicle kills the enemy and neutral soldiers of a cell it
/// drives into and takes the cell at once; its own infantry it drives around or makes step aside; a soldier that cannot be crushed or
/// a vehicle that does not crush changes nothing. The tank drives a straight line east along row 10 in every test.
/// </summary>
public sealed class CrushTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);
    private static readonly WorldPos End = new CellPos(20, 10).Center;

    private static GameSimulation Match(GameRules? rules = null) =>
        new(new SimulationSettings(1, new MapGrid(40, 30, (rules ?? TestRules.Standard).Land)), rules ?? TestRules.Standard);

    private static Unit Spawn(GameSimulation simulation, PlayerId owner, UnitType type, int x, int y) =>
        simulation.World.SpawnUnit(owner, type, new CellPos(x, y).Center);

    /// <summary>Orders the tank to <see cref="End"/>; plays until it arrives or 600 ticks pass and gives the events.</summary>
    private static List<SimEvent> Drive(GameSimulation simulation, Unit tank)
    {
        simulation.Enqueue(new MoveCommand(tank.Owner, simulation.CurrentTick, 0, [tank.Id], End));
        var events = new List<SimEvent>();
        for (var tick = 0; tick < 600 && !(tank.Position == End && tank.MoveTarget is null); tick++)
        {
            simulation.Step();
            events.AddRange(simulation.LastTickEvents);
        }

        return events;
    }

    private static IEnumerable<EntityId> Crushed(List<SimEvent> events, Unit tank) =>
        events.OfType<EntityDestroyed>().Where(e => e.Cause == DeathCause.Crushed && e.Killer == tank.Id).Select(e => e.Entity);

    [Fact]
    public void A_tank_crushes_an_enemy_soldier_on_its_way_and_drives_straight_on()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
        var engineer = Spawn(simulation, Player2, UnitType.Engineer, 15, 10);

        var events = Drive(simulation, tank);

        Assert.Equal([engineer.Id], Crushed(events, tank));
        Assert.Equal(End, tank.Position);
        Assert.False(simulation.World.TryGet(engineer.Id, out _), "the crushed soldier left the world");
        // Straight on: the tank never left row 10.
        Assert.DoesNotContain(events, e => e is UnitBlocked);
        Assert.Equal(0, engineer.Hp);
    }

    [Fact]
    public void Every_enemy_soldier_of_a_cell_dies_at_once_and_the_tank_takes_the_cell_on_that_tick()
    {
        var simulation = Match();
        var world = simulation.World;
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
        var soldiers = new[] { Spawn(simulation, Player2, UnitType.Engineer, 13, 10), Spawn(simulation, Player2, UnitType.Engineer, 13, 10), Spawn(simulation, Player2, UnitType.Conscript, 13, 10) };
        foreach (var soldier in soldiers)
        {
            soldier.Stance = Stance.HoldPosition;
        }

        var crushTick = -1;
        var events = new List<SimEvent>();
        simulation.Enqueue(new MoveCommand(Player1, 0, 0, [tank.Id], End));
        for (var tick = 0; tick < 600 && crushTick < 0; tick++)
        {
            simulation.Step();
            events.AddRange(simulation.LastTickEvents);
            if (simulation.LastTickEvents.Any(e => e is EntityDestroyed { Cause: DeathCause.Crushed }))
            {
                crushTick = simulation.LastTickEvents[0].Tick;
            }
        }

        var crushed = events.OfType<EntityDestroyed>().Where(e => e.Cause == DeathCause.Crushed).ToList();
        Assert.Equal(soldiers.Select(s => s.Id).ToHashSet(), crushed.Select(e => e.Entity).ToHashSet());
        Assert.All(crushed, e => Assert.Equal(crushTick, e.Tick));
        Assert.Equal(new CellPos(13, 10), tank.Position.ToCell());
        Assert.Equal(tank.Id, world.Reservations.VehicleAt(new CellPos(13, 10)));
        Assert.Equal(0, world.Reservations.InfantryCount(new CellPos(13, 10)));
    }

    [Fact]
    public void Neutral_and_moving_soldiers_are_crushed_too()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
        var neutral = Spawn(simulation, PlayerId.Neutral, UnitType.Conscript, 13, 10);
        // Head-on along the same row: the walker is still on its way when the tank reaches it.
        var walker = Spawn(simulation, Player2, UnitType.Engineer, 19, 10);
        simulation.Enqueue(new MoveCommand(Player2, 0, 1, [walker.Id], new CellPos(5, 10).Center));

        var events = Drive(simulation, tank);

        Assert.Contains(neutral.Id, Crushed(events, tank));
        Assert.Contains(walker.Id, Crushed(events, tank));
    }

    [Fact]
    public void Own_infantry_on_the_way_is_left_alive_and_the_tank_still_arrives()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
        var friend = Spawn(simulation, Player1, UnitType.Conscript, 15, 10);

        var events = Drive(simulation, tank);

        Assert.Empty(Crushed(events, tank));
        Assert.True(friend.IsAlive);
        Assert.Equal(End, tank.Position);
    }

    [Fact]
    public void A_cell_with_an_own_soldier_among_the_enemies_is_not_crushed()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
        var friend = Spawn(simulation, Player1, UnitType.Engineer, 15, 10);
        var enemy = Spawn(simulation, Player2, UnitType.Engineer, 15, 10);

        var events = Drive(simulation, tank);

        Assert.Empty(Crushed(events, tank));
        Assert.True(friend.IsAlive && enemy.IsAlive);
    }

    [Fact]
    public void A_soldier_that_cannot_be_crushed_is_driven_around()
    {
        var simulation = Match(TestRules.WithUnit(UnitType.Engineer, engineer => engineer with { Crushable = false }));
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
        var engineer = Spawn(simulation, Player2, UnitType.Engineer, 15, 10);

        var events = Drive(simulation, tank);

        Assert.Empty(Crushed(events, tank));
        Assert.True(engineer.IsAlive);
        Assert.Equal(End, tank.Position);
    }

    [Fact]
    public void A_vehicle_that_does_not_crush_drives_around()
    {
        var simulation = Match(TestRules.WithUnit(UnitType.HeavyTank, tank => tank with { Crusher = false }));
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
        var engineer = Spawn(simulation, Player2, UnitType.Engineer, 15, 10);

        var events = Drive(simulation, tank);

        Assert.Empty(Crushed(events, tank));
        Assert.True(engineer.IsAlive);
        Assert.Equal(End, tank.Position);
    }

    [Fact]
    public void The_same_crush_gives_the_same_state()
    {
        ulong Run()
        {
            var simulation = Match();
            var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
            Spawn(simulation, Player2, UnitType.Engineer, 13, 10);
            Spawn(simulation, Player2, UnitType.Engineer, 13, 10);
            Spawn(simulation, PlayerId.Neutral, UnitType.Conscript, 16, 10);
            Drive(simulation, tank);
            return StateHasher.Compute(simulation.World);
        }

        Assert.Equal(Run(), Run());
    }
}
