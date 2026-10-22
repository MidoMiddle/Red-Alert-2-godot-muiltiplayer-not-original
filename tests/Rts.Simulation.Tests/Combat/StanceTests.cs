using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Combat;

/// <summary>
/// The stances (docs/implementation/M4_combat.md, 4.11): an aggressive unit follows what it sees, a guarding one follows within its
/// leash and comes back, one that holds its position never moves; a target that left the sight is dropped.
/// </summary>
public sealed class StanceTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    private static GameSimulation Match() =>
        new(new SimulationSettings(1, new MapGrid(40, 40, TestRules.Standard.Land)), TestRules.Standard);

    private static Unit Spawn(GameSimulation simulation, PlayerId owner, UnitType type, int x, int y) =>
        simulation.World.SpawnUnit(owner, type, new CellPos(x, y).Center);

    private static void SetStance(GameSimulation simulation, Unit unit, Stance stance) =>
        Assert.True(CommandProcessor.Apply(simulation.World, new SetStanceCommand(Player1, 0, 0, [unit.Id], stance)));

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
    public void An_aggressive_unit_drives_to_an_enemy_in_its_sight_and_stays_where_the_fight_ended()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
        var engineer = Spawn(simulation, Player2, UnitType.Engineer, 17, 10);       // 7 cells: beyond 5.75, within the sight of 8

        var events = Play(simulation, 800);

        Assert.Contains(events, e => e is EntityDestroyed destroyed && destroyed.Entity == engineer.Id && destroyed.Killer == tank.Id);
        Assert.True(tank.Position.X > new CellPos(10, 10).Center.X, "the tank drove toward the engineer");
        var stopped = tank.Position;
        Play(simulation, 100);
        Assert.Equal(stopped, tank.Position);
        Assert.False(tank.Engaged);
    }

    [Fact]
    public void A_unit_that_holds_its_position_never_moves_and_looks_only_within_its_range()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
        SetStance(simulation, tank, Stance.HoldPosition);
        var far = Spawn(simulation, Player2, UnitType.Engineer, 17, 10);            // 7 cells: in sight, out of range

        var events = Play(simulation, 300);

        Assert.Equal(new CellPos(10, 10).Center, tank.Position);
        Assert.Null(tank.Target);
        Assert.DoesNotContain(events, e => e is WeaponFired fired && fired.Shooter == tank.Id);

        var near = Spawn(simulation, Player2, UnitType.Engineer, 14, 10);           // 4 cells: in range
        events = Play(simulation, 300);
        Assert.Contains(events, e => e is EntityDestroyed destroyed && destroyed.Entity == near.Id && destroyed.Killer == tank.Id);
        Assert.Equal(new CellPos(10, 10).Center, tank.Position);
        Assert.True(far.IsAlive);
    }

    [Fact]
    public void A_guarding_unit_follows_an_enemy_and_comes_back_to_its_anchor()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);
        SetStance(simulation, tank, Stance.Guard);
        var engineer = Spawn(simulation, Player2, UnitType.Engineer, 18, 10);      // 8 cells: the edge of its sight

        var farthest = 0;
        for (var tick = 0; tick < 600 && engineer.IsAlive; tick++)
        {
            simulation.Step();
            farthest = System.Math.Max(farthest, tank.Position.X);
        }

        Assert.False(engineer.IsAlive);
        Assert.True(farthest >= new CellPos(12, 10).Center.X, $"the tank drove toward the engineer, to {farthest}");

        Play(simulation, 600);
        // Back within a cell of its anchor: farther off, it drives back.
        Assert.InRange(tank.Position.ToCell().X, 9, 11);
        Assert.InRange(tank.Position.ToCell().Y, 9, 11);
        Assert.Equal(new CellPos(10, 10).Center, tank.Anchor);
    }

    [Fact]
    public void A_guarding_unit_takes_nothing_beyond_its_leash_from_the_anchor()
    {
        var world = new GameWorld(seed: 1, widthCells: 40, heightCells: 40, TestRules.Standard);
        var tank = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(20, 10).Center);
        var enemy = world.SpawnUnit(Player2, UnitType.Engineer, new CellPos(25, 10).Center);   // 5 cells off the tank
        tank.Stance = Stance.Guard;
        tank.Anchor = new CellPos(10, 10).Center;                                                // 15 cells off the anchor: beyond 6 + 5.75

        VisibilitySystem.Update(world);
        world.CurrentTick = (TargetingSystem.Period - (tank.Id.Value % TargetingSystem.Period)) % TargetingSystem.Period;
        TargetingSystem.Update(world);
        Assert.Null(tank.Target);

        tank.Stance = Stance.Aggressive;
        TargetingSystem.Update(world);
        Assert.Equal(enemy.Id, tank.Target);
    }

    [Fact]
    public void A_target_that_left_the_sight_is_dropped()
    {
        var world = new GameWorld(seed: 1, widthCells: 40, heightCells: 40, TestRules.Standard);
        var tank = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(10, 10).Center);
        var enemy = world.SpawnUnit(Player2, UnitType.Engineer, new CellPos(22, 10).Center);    // 12 cells: beyond the sight of 8
        tank.Target = enemy.Id;

        VisibilitySystem.Update(world);
        world.CurrentTick = (TargetingSystem.Period - (tank.Id.Value % TargetingSystem.Period)) % TargetingSystem.Period;
        TargetingSystem.Update(world);

        Assert.Null(tank.Target);
    }

    [Fact]
    public void A_harvester_and_an_unarmed_unit_keep_their_stance()
    {
        var simulation = Match();
        var harvester = Spawn(simulation, Player1, UnitType.Harvester, 10, 10);
        var engineer = Spawn(simulation, Player1, UnitType.Engineer, 12, 10);

        Assert.False(CommandProcessor.Apply(simulation.World, new SetStanceCommand(Player1, 0, 0, [harvester.Id, engineer.Id], Stance.Guard)));
        Assert.False(CommandProcessor.Apply(simulation.World, new SetStanceCommand(Player1, 0, 0, [harvester.Id], (Stance)7)));

        Assert.Equal((Stance.Aggressive, Stance.Aggressive), (harvester.Stance, engineer.Stance));
    }

    [Fact]
    public void An_unknown_stance_is_refused()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, 10, 10);

        Assert.False(CommandProcessor.Apply(simulation.World, new SetStanceCommand(Player1, 0, 0, [tank.Id], (Stance)3)));
        Assert.Equal(Stance.Aggressive, tank.Stance);
    }
}
