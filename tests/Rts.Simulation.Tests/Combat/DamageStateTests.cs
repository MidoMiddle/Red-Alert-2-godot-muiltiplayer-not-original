using Rts.Simulation.Combat;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Combat;

/// <summary>
/// Hit points and the states of damage (docs/implementation/M4_combat.md, 4.4): the thresholds, the event when one is crossed, and the
/// hit points units start with.
/// </summary>
public sealed class DamageStateTests
{
    private static readonly PlayerId Player1 = new(1);

    [Theory]
    [InlineData(500, 500, DamageState.Healthy)]
    [InlineData(251, 500, DamageState.Healthy)]
    [InlineData(250, 500, DamageState.Damaged)]     // half is not more than half
    [InlineData(125, 500, DamageState.Damaged)]     // a quarter is not less than a quarter
    [InlineData(124, 500, DamageState.Critical)]
    [InlineData(1, 500, DamageState.Critical)]
    [InlineData(0, 500, DamageState.Destroyed)]
    [InlineData(-5, 500, DamageState.Destroyed)]
    [InlineData(63, 125, DamageState.Healthy)]      // 126 > 125
    [InlineData(62, 125, DamageState.Damaged)]
    [InlineData(32, 125, DamageState.Damaged)]      // 128 is not less than 125
    [InlineData(31, 125, DamageState.Critical)]
    public void The_thresholds_are_a_half_and_a_quarter(int hp, int maxHp, DamageState state)
    {
        Assert.Equal(state, DamageStates.Of(hp, maxHp));
    }

    [Fact]
    public void Damage_reports_each_threshold_once_and_repair_reports_them_back()
    {
        var simulation = new GameSimulation(new SimulationSettings(1, new MapGrid(40, 30, TestRules.Standard.Land)), TestRules.Standard);
        var reactor = simulation.World.CreateBuilding(Player1, TestRules.BuildingDef("reactor"), new CellPos(10, 10), true, null);
        uint sequence = 0;
        var states = new List<DamageState>();

        // 500 hit points: 300 down is 200 (damaged), 100 more is 100 (critical), then a hit that crosses nothing.
        foreach (var amount in new[] { 100, 200, 100, 50 })
        {
            simulation.Enqueue(new DamageMapObjectCommand(Player1, simulation.CurrentTick, sequence++, new CellPos(10, 10), amount));
            simulation.Step();
            states.AddRange(simulation.LastTickEvents.OfType<DamageStateChanged>().Select(changed => changed.State));
        }

        Assert.Equal(50, reactor.Hp);
        Assert.Equal([DamageState.Damaged, DamageState.Critical], states);

        states.Clear();
        simulation.Enqueue(new ToggleRepairCommand(Player1, simulation.CurrentTick, sequence++, reactor.Id));
        simulation.Step();
        states.AddRange(simulation.LastTickEvents.OfType<DamageStateChanged>().Select(changed => changed.State));
        for (var i = 0; i < 1000 && reactor.Repairing; i++)
        {
            simulation.Step();
            states.AddRange(simulation.LastTickEvents.OfType<DamageStateChanged>().Select(changed => changed.State));
        }

        Assert.Equal(500, reactor.Hp);
        Assert.Equal([DamageState.Damaged, DamageState.Healthy], states);
    }

    [Fact]
    public void A_unit_starts_whole_with_the_hit_points_and_armor_of_its_type()
    {
        var world = new GameWorld(seed: 1, widthCells: 20, heightCells: 20, TestRules.Standard);
        var conscript = world.SpawnUnit(Player1, UnitType.Conscript, new CellPos(2, 2).Center);
        var tank = world.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(5, 5).Center);
        var database = TestRules.Standard.Database;

        Assert.Equal((125, 125, DamageState.Healthy), (conscript.Hp, conscript.MaxHp, DamageStates.Of(conscript.Hp, conscript.MaxHp)));
        Assert.Equal(400, tank.MaxHp);
        Assert.Equal(database.IndexOfArmor("flak"), conscript.Archetype.Armor);
        Assert.Equal(database.IndexOfArmor("heavy"), tank.Archetype.Armor);
    }
}
