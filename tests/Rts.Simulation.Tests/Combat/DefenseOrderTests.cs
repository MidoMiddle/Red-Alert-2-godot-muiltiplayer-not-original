using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Combat;

/// <summary>
/// A defense fires where the player tells it (RA2): an attack ordered to a selected defense keeps its fire on that target until it falls
/// or leaves the range, a stop clears it, and a defense takes no target out of its range or that it cannot hit.
/// </summary>
public sealed class DefenseOrderTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    private static uint _sequence;

    private static GameSimulation Match() =>
        new(new SimulationSettings(1, new MapGrid(40, 40, TestRules.Standard.Land)), TestRules.Standard);

    private static Building Build(GameSimulation simulation, string id, int x, int y) =>
        simulation.World.CreateBuilding(Player1, TestRules.BuildingDef(id), new CellPos(x, y), ready: true, null);

    private static Unit Spawn(GameSimulation simulation, PlayerId owner, UnitType type, int x, int y)
    {
        var unit = simulation.World.SpawnUnit(owner, type, new CellPos(x, y).Center);
        unit.Stance = Stance.HoldPosition;
        return unit;
    }

    // A tick first, for the player to see what is there.
    private static bool Attack(GameSimulation simulation, Entity defense, Entity target)
    {
        simulation.Step();
        return CommandProcessor.Apply(simulation.World, new AttackCommand(Player1, simulation.CurrentTick, _sequence++, [defense.Id], target.Id));
    }

    /// <summary>Plays <paramref name="ticks"/> ticks; the targets <paramref name="shooter"/> fired at.</summary>
    private static List<EntityId?> Fired(GameSimulation simulation, Entity shooter, int ticks)
    {
        var targets = new List<EntityId?>();
        for (var i = 0; i < ticks; i++)
        {
            simulation.Step();
            targets.AddRange(simulation.LastTickEvents.OfType<WeaponFired>().Where(fired => fired.Shooter == shooter.Id).Select(fired => fired.Target));
        }

        return targets;
    }

    [Fact]
    public void An_ordered_bunker_fires_at_a_far_tank_in_range_instead_of_a_near_one()
    {
        var simulation = Match();
        var bunker = Build(simulation, "sentry_bunker", 10, 10);
        var near = Spawn(simulation, Player2, UnitType.HeavyTank, 12, 10);
        var tank = Spawn(simulation, Player2, UnitType.HeavyTank, 15, 10);
        simulation.Step();
        simulation.Step();
        simulation.Step();
        simulation.Step();
        Assert.Equal(near.Id, bunker.Target);                                       // by itself it takes the near one

        Assert.True(Attack(simulation, bunker, tank));
        Assert.Equal(TargetOrder.Attack, bunker.TargetOrder);
        var nearHp = near.Hp;

        var fired = Fired(simulation, bunker, 80);
        Assert.NotEmpty(fired);
        Assert.All(fired, target => Assert.Equal(tank.Id, target));
        Assert.Equal(tank.Id, bunker.Target);
        Assert.True(tank.Hp < tank.MaxHp);
        Assert.Equal(nearHp, near.Hp);
    }

    [Fact]
    public void The_order_ends_when_the_target_falls_and_the_bunker_chooses_by_itself_again()
    {
        var simulation = Match();
        var bunker = Build(simulation, "sentry_bunker", 10, 10);
        var near = Spawn(simulation, Player2, UnitType.Conscript, 12, 10);
        var far = Spawn(simulation, Player2, UnitType.Conscript, 14, 11);
        Assert.True(Attack(simulation, bunker, far));

        for (var i = 0; i < 600 && far.IsAlive; i++)
        {
            Assert.Equal(far.Id, bunker.Target);
            simulation.Step();
        }

        Assert.False(far.IsAlive);
        Fired(simulation, bunker, 8);
        Assert.Equal(TargetOrder.None, bunker.TargetOrder);
        Assert.Equal(near.Id, bunker.Target);
    }

    [Fact]
    public void The_order_ends_when_the_target_leaves_the_range()
    {
        var simulation = Match();
        var bunker = Build(simulation, "sentry_bunker", 10, 10);
        var tank = Spawn(simulation, Player2, UnitType.HeavyTank, 14, 10);
        Spawn(simulation, Player1, UnitType.Engineer, 24, 12);                     // an own spotter: it is not the fog that ends it
        Assert.True(Attack(simulation, bunker, tank));

        simulation.Enqueue(new MoveCommand(Player2, simulation.CurrentTick, _sequence++, [tank.Id], new CellPos(25, 10).Center));
        for (var i = 0; i < 400 && bunker.TargetOrder != TargetOrder.None; i++)
        {
            simulation.Step();
        }

        Assert.Equal(TargetOrder.None, bunker.TargetOrder);
        Assert.Null(bunker.Target);
        Assert.True(tank.IsAlive);
        Assert.True(tank.Position.X > new CellPos(15, 10).Center.X);
    }

    [Fact]
    public void A_stop_clears_the_order()
    {
        var simulation = Match();
        var bunker = Build(simulation, "sentry_bunker", 10, 10);
        var tank = Spawn(simulation, Player2, UnitType.HeavyTank, 14, 10);
        Assert.True(Attack(simulation, bunker, tank));

        Assert.True(CommandProcessor.Apply(simulation.World, new StopCommand(Player1, simulation.CurrentTick, _sequence++, [bunker.Id])));

        Assert.Equal(TargetOrder.None, bunker.TargetOrder);
        Assert.Null(bunker.Target);
    }

    [Fact]
    public void A_bunker_takes_no_order_out_of_its_range()
    {
        var simulation = Match();
        var bunker = Build(simulation, "sentry_bunker", 10, 10);
        var tank = Spawn(simulation, Player2, UnitType.HeavyTank, 18, 10);          // 8 cells, the range is 5.5
        Spawn(simulation, Player1, UnitType.Engineer, 17, 12);                     // the player sees it

        Assert.False(Attack(simulation, bunker, tank));
        Assert.Equal(TargetOrder.None, bunker.TargetOrder);
    }

    [Fact]
    public void A_flak_cannon_takes_no_order_on_a_tank()
    {
        var simulation = Match();
        Build(simulation, "reactor", 2, 2);
        var flak = Build(simulation, "flak_cannon", 10, 10);
        var tank = Spawn(simulation, Player2, UnitType.HeavyTank, 13, 10);
        simulation.Step();

        Assert.False(Attack(simulation, flak, tank));
        Assert.Equal(TargetOrder.None, flak.TargetOrder);
    }

    [Fact]
    public void A_forced_attack_turns_a_bunker_on_an_own_unit_but_not_on_the_ground()
    {
        var simulation = Match();
        var bunker = Build(simulation, "sentry_bunker", 10, 10);
        var own = Spawn(simulation, Player1, UnitType.Conscript, 12, 10);
        simulation.Step();

        Assert.False(CommandProcessor.Apply(simulation.World,
            new ForceAttackCommand(Player1, simulation.CurrentTick, _sequence++, [bunker.Id], null, new CellPos(12, 12).Center)));
        Assert.True(CommandProcessor.Apply(simulation.World,
            new ForceAttackCommand(Player1, simulation.CurrentTick, _sequence++, [bunker.Id], own.Id, own.Position)));

        Assert.Equal(TargetOrder.Force, bunker.TargetOrder);
        Assert.Contains(own.Id, Fired(simulation, bunker, 60));
    }
}
