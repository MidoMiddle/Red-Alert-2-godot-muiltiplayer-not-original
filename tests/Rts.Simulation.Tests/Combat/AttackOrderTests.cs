using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Combat;

/// <summary>
/// The orders to attack (docs/implementation/M4_combat.md, 4.12): an attack on an enemy, a forced attack on anything or the ground,
/// what is refused; a plain move that ignores the enemies, an attack-move that fights its way to the point, a stop.
/// </summary>
public sealed class AttackOrderTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    private static GameSimulation Match() =>
        new(new SimulationSettings(1, new MapGrid(40, 40, TestRules.Standard.Land)), TestRules.Standard);

    private static Unit Spawn(GameSimulation simulation, PlayerId owner, UnitType type, int x, int y) =>
        simulation.World.SpawnUnit(owner, type, new CellPos(x, y).Center);

    private static uint _sequence;

    private static AttackCommand Attack(GameSimulation simulation, Unit[] units, EntityId target) =>
        new(Player1, simulation.CurrentTick, _sequence++, [.. units.Select(u => u.Id)], target);

    /// <summary>Plays up to <paramref name="ticks"/> ticks, until <paramref name="done"/>; gives the events of those ticks.</summary>
    private static List<SimEvent> Play(GameSimulation simulation, int ticks, Func<bool>? done = null)
    {
        var events = new List<SimEvent>();
        for (var i = 0; i < ticks && done?.Invoke() != true; i++)
        {
            simulation.Step();
            events.AddRange(simulation.LastTickEvents);
        }

        return events;
    }

    [Fact]
    public void An_ordered_tank_drives_to_a_far_enemy_keeps_to_it_and_then_chooses_by_itself()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
        var engineer = Spawn(simulation, Player2, UnitType.Engineer, 22, 10);          // 12 cells: beyond the sight of 8
        var conscript = Spawn(simulation, Player2, UnitType.Conscript, 10, 13);        // 3 cells, threat 10 against 5
        simulation.Enqueue(Attack(simulation, [tank], engineer.Id));
        simulation.Step();

        var events = Play(simulation, 1500, () =>
        {
            Assert.True(!engineer.IsAlive || tank.Target == engineer.Id, $"tick {simulation.CurrentTick}: target {tank.Target}");
            return !engineer.IsAlive;
        });

        Assert.Contains(events, e => e is EntityDestroyed destroyed && destroyed.Entity == engineer.Id && destroyed.Killer == tank.Id);
        Assert.True(tank.Position.X > new CellPos(15, 10).Center.X, "the tank drove toward the engineer");

        // The order is over: the tank chooses the conscript by itself.
        Play(simulation, 2 * TargetingSystem.Period);
        Assert.Equal(TargetOrder.None, tank.TargetOrder);
        Assert.Equal(conscript.Id, tank.Target);
    }

    [Fact]
    public void An_attack_on_anything_but_a_living_enemy_the_player_could_target_is_refused()
    {
        var simulation = Match();
        var world = simulation.World;
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
        var friend = Spawn(simulation, Player1, UnitType.Conscript, 11, 10);
        var neutral = Spawn(simulation, PlayerId.Neutral, UnitType.Conscript, 12, 10);
        var dead = Spawn(simulation, Player2, UnitType.Conscript, 13, 10);
        dead.MarkDead();
        var wall = world.CreateBuilding(Player2, TestRules.BuildingDef("wall"), new CellPos(14, 12), true, null);
        var shot = world.Launch(Player2, friend.Id, 0, tank.Id, new CellPos(15, 10).Center, tank.Position);

        foreach (var target in new[] { friend.Id, neutral.Id, dead.Id, wall.Id, shot.Id, new EntityId(999) })
        {
            Assert.False(CommandProcessor.Apply(world, Attack(simulation, [tank], target)), $"target {target}");
        }

        Assert.Null(tank.Target);
        Assert.Equal(TargetOrder.None, tank.TargetOrder);
    }

    [Fact]
    public void Units_that_cannot_attack_are_left_out_and_a_harvester_keeps_to_its_work()
    {
        var simulation = Match();
        var world = simulation.World;
        var engineer = Spawn(simulation, Player1, UnitType.Engineer, 10, 10);
        var harvester = Spawn(simulation, Player1, UnitType.Harvester, 11, 11);
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 12, 10);
        var enemy = Spawn(simulation, Player2, UnitType.HeavyTank, 18, 10);
        var mode = harvester.Harvester!.Mode;

        Assert.False(CommandProcessor.Apply(world, Attack(simulation, [engineer], enemy.Id)));
        Assert.True(CommandProcessor.Apply(world, Attack(simulation, [engineer, harvester, tank], enemy.Id)));

        Assert.Null(engineer.Target);
        Assert.Equal((enemy.Id, TargetOrder.Attack), (tank.Target, tank.TargetOrder));
        Assert.Equal((enemy.Id, TargetOrder.Attack), (harvester.Target, harvester.TargetOrder));
        Assert.Equal(mode, harvester.Harvester.Mode);
    }

    [Fact]
    public void A_forced_attack_hurts_an_own_unit()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
        var friend = Spawn(simulation, Player1, UnitType.HeavyTank, 13, 10);
        simulation.Enqueue(new ForceAttackCommand(Player1, 0, _sequence++, [tank.Id], friend.Id, WorldPos.Zero));

        var events = Play(simulation, 200);

        Assert.Contains(events, e => e is EntityDamaged damaged && damaged.Entity == friend.Id && damaged.Attacker == tank.Id);
        Assert.Equal(TargetOrder.Force, tank.TargetOrder);
        // The friend is no enemy: it does not fire back.
        Assert.DoesNotContain(events, e => e is WeaponFired fired && fired.Shooter == friend.Id);
    }

    [Fact]
    public void A_forced_attack_on_the_ground_lands_there_with_no_target_until_a_stop()
    {
        var simulation = Match();
        var flak = Spawn(simulation, Player1, UnitType.SupportVehicle, 10, 10);
        var point = new CellPos(13, 10).Center;
        simulation.Enqueue(new ForceAttackCommand(Player1, 0, _sequence++, [flak.Id], null, point));

        var events = Play(simulation, 300);

        var fired = events.OfType<WeaponFired>().Where(e => e.Shooter == flak.Id).ToList();
        Assert.True(fired.Count >= 2, $"{fired.Count} shots");
        Assert.All(fired, e => Assert.Null(e.Target));
        var impacts = events.OfType<ProjectileImpact>().Where(e => e.Shooter == flak.Id).ToList();
        Assert.NotEmpty(impacts);
        Assert.All(impacts, e => Assert.Null(e.Target));
        Assert.All(impacts, e => Assert.InRange(Rts.Simulation.Combat.DamageCalculator.Distance(e.Point, point), 0, WorldPos.LeptonsPerCell));

        simulation.Enqueue(new StopCommand(Player1, simulation.CurrentTick, _sequence++, [flak.Id]));
        var after = Play(simulation, 300);
        Assert.DoesNotContain(after, e => e is WeaponFired shot && shot.Shooter == flak.Id);
        Assert.Null(flak.TargetPoint);
    }

    [Fact]
    public void A_forced_attack_breaks_a_wall_with_a_cannon()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
        var wall = simulation.World.CreateBuilding(Player2, TestRules.BuildingDef("wall"), new CellPos(14, 10), true, null);
        var hp = wall.Hp;
        simulation.Enqueue(new ForceAttackCommand(Player1, 0, _sequence++, [tank.Id], wall.Id, WorldPos.Zero));

        Play(simulation, 200);

        Assert.True(wall.Hp < hp || !wall.IsAlive);
    }

    [Fact]
    public void A_plain_move_past_an_enemy_holds_fire_until_it_arrives()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
        var engineer = Spawn(simulation, Player2, UnitType.Engineer, 12, 20);
        var end = new CellPos(10, 25).Center;
        simulation.Enqueue(new MoveCommand(Player1, 0, _sequence++, [tank.Id], end));

        var events = Play(simulation, 800);

        var arrived = events.OfType<UnitArrived>().First(e => e.Unit == tank.Id && e.Position == end).Tick;
        var shots = events.OfType<WeaponFired>().Where(e => e.Shooter == tank.Id).ToList();
        Assert.NotEmpty(shots);
        Assert.All(shots, e => Assert.True(e.Tick > arrived, $"shot at {e.Tick}, arrived at {arrived}"));
        Assert.Equal(engineer.Id, shots[0].Target);
    }

    [Fact]
    public void An_attack_move_stops_for_the_enemies_on_the_way_and_then_goes_on_to_its_point()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 5);
        var first = Spawn(simulation, Player2, UnitType.Engineer, 12, 14);
        var second = Spawn(simulation, Player2, UnitType.Engineer, 8, 16);
        var end = new CellPos(10, 30).Center;
        simulation.Enqueue(new AttackMoveCommand(Player1, 0, _sequence++, [tank.Id], end));

        var events = Play(simulation, 3000, () => tank.Position == end && tank.MoveTarget is null);

        var killed = events.OfType<EntityDestroyed>().Where(e => e.Killer == tank.Id).Select(e => e.Entity).ToHashSet();
        Assert.Equal(new HashSet<EntityId> { first.Id, second.Id }, killed);
        Assert.Contains(events, e => e is UnitArrived arrived && arrived.Unit == tank.Id && arrived.Position == end);
        Assert.Empty(tank.Orders);
        Assert.False(tank.Engaged);
    }

    [Fact]
    public void A_stop_forgets_the_target_and_the_order_and_guards_where_it_stands()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
        var enemy = Spawn(simulation, Player2, UnitType.Engineer, 30, 10);
        simulation.Enqueue(Attack(simulation, [tank], enemy.Id));
        Play(simulation, 40);
        Assert.NotEqual(new CellPos(10, 10).Center, tank.Position);

        simulation.Enqueue(new StopCommand(Player1, simulation.CurrentTick, _sequence++, [tank.Id]));
        Play(simulation, 1);

        Assert.Equal((null, TargetOrder.None, false), (tank.Target, tank.TargetOrder, tank.Engaged));
        Assert.Equal(tank.Position, tank.Anchor);
        Assert.Null(tank.MoveTarget);
    }
}
