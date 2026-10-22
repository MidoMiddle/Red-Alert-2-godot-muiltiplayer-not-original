using Rts.Simulation.Combat;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Combat;

/// <summary>
/// The defenses of the Union (docs/implementation/M4_combat.md, 4.17): a powered defense that drains power is off while its owner is
/// low on power and back when the power is; the sentry bunker of power 0 fires on; a sentry bunker is no wall.
/// </summary>
public sealed class DefenseTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    private static GameSimulation Match() =>
        new(new SimulationSettings(1, new MapGrid(40, 40, TestRules.Standard.Land)), TestRules.Standard);

    private static Building Build(GameSimulation simulation, string id, int x, int y) =>
        simulation.World.CreateBuilding(Player1, TestRules.BuildingDef(id), new CellPos(x, y), ready: true, null);

    /// <summary>Plays <paramref name="ticks"/> ticks and counts the shots of <paramref name="shooter"/>.</summary>
    private static int Shots(GameSimulation simulation, Entity shooter, int ticks)
    {
        var shots = 0;
        for (var i = 0; i < ticks; i++)
        {
            simulation.Step();
            shots += simulation.LastTickEvents.OfType<WeaponFired>().Count(fired => fired.Shooter == shooter.Id);
        }

        return shots;
    }

    [Fact]
    public void A_tesla_coil_is_silent_on_low_power_and_fires_again_when_the_power_is_back()
    {
        var simulation = Match();
        var coil = Build(simulation, "tesla_coil", 10, 10);                       // drains 75, nothing produces
        var tank = simulation.World.SpawnUnit(Player2, UnitType.HeavyTank, new CellPos(16, 11).Center);
        tank.Stance = Stance.HoldPosition;                                         // it stays where it is put

        Assert.Equal(0, Shots(simulation, coil, 40));
        Assert.True(simulation.World.Player(Player1).LowPower);
        Assert.False(Defenses.IsOnline(simulation.World, coil));
        Assert.Null(coil.Target);

        Build(simulation, "reactor", 2, 2);                                         // 150 against 75
        Assert.True(Shots(simulation, coil, 40) > 0);
        Assert.True(Defenses.IsOnline(simulation.World, coil));
        Assert.True(tank.Hp < tank.MaxHp || !tank.IsAlive);
    }

    [Fact]
    public void A_sentry_bunker_fires_on_low_power_because_it_drains_none()
    {
        var simulation = Match();
        Build(simulation, "barracks", 2, 2);                                        // drains 10: low power
        var bunker = Build(simulation, "sentry_bunker", 10, 10);
        var conscript = simulation.World.SpawnUnit(Player2, UnitType.Conscript, new CellPos(13, 10).Center);
        conscript.Stance = Stance.HoldPosition;

        Assert.True(Shots(simulation, bunker, 40) > 0);
        Assert.True(simulation.World.Player(Player1).LowPower);
        Assert.True(Defenses.IsOnline(simulation.World, bunker));
    }

    [Fact]
    public void A_flak_cannon_is_off_on_low_power()
    {
        var simulation = Match();
        var flak = Build(simulation, "flak_cannon", 10, 10);
        simulation.Step();

        Assert.False(Defenses.IsOnline(simulation.World, flak));

        Build(simulation, "reactor", 2, 2);
        simulation.Step();

        Assert.True(Defenses.IsOnline(simulation.World, flak));
    }

    [Fact]
    public void A_building_that_is_not_powered_works_on_low_power()
    {
        var simulation = Match();
        var barracks = Build(simulation, "barracks", 2, 2);
        simulation.Step();

        Assert.True(simulation.World.Player(Player1).LowPower);
        Assert.True(Defenses.IsOnline(simulation.World, barracks));
    }

    [Fact]
    public void A_sentry_bunker_of_one_cell_is_no_wall_and_cannot_be_laid_in_a_line()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(simulation, "construction_yard", 2, 2);
        var bunker = TestRules.Standard.Database.IndexOfBuilding("sentry_bunker");

        Assert.False(CommandProcessor.Apply(world, new PlaceWallsCommand(Player1, 0, 0, bunker, [new CellPos(6, 2), new CellPos(7, 2)])));
        Assert.False(TestRules.BuildingDef("sentry_bunker").Wall);
        Assert.True(TestRules.BuildingDef("wall").Wall);
    }
}
