using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Combat;

/// <summary>
/// Aiming and firing (docs/implementation/M4_combat.md, 4.6, 4.7): how often a weapon fires, how the turret and the hull aim, the range,
/// volleys, reloading without a target, which weapon fires, defenses. A shot is a <see cref="WeaponFired"/> event in this step.
/// </summary>
public sealed class WeaponSystemTests
{
    private static readonly PlayerId Player1 = new(1);
    // The targets belong to nobody: they neither fire back nor are chosen by the targeting of step 6, so each test sees one shooter.
    private static readonly PlayerId Nobody = PlayerId.Neutral;

    // The Rhino's cannon of the test rules (game/data): reload 65 ticks, range 5.75 cells = 1472 leptons.
    private const int CannonReload = 65;

    private static GameSimulation Match(GameRules? rules = null) =>
        new(new SimulationSettings(1, new MapGrid(40, 30, (rules ?? TestRules.Standard).Land)), rules ?? TestRules.Standard);

    private static Unit Spawn(GameSimulation simulation, PlayerId owner, UnitType type, WorldPos position) =>
        simulation.World.SpawnUnit(owner, type, position);

    /// <summary>Plays <paramref name="ticks"/> ticks and gives the ticks of the shots of <paramref name="shooter"/>.</summary>
    private static List<(int Tick, int Slot)> Shots(GameSimulation simulation, Entity shooter, int ticks)
    {
        var shots = new List<(int, int)>();
        for (var i = 0; i < ticks; i++)
        {
            simulation.Step();
            shots.AddRange(simulation.LastTickEvents.OfType<WeaponFired>().Where(fired => fired.Shooter == shooter.Id).Select(fired => (fired.Tick, fired.Slot)));
        }

        return shots;
    }

    [Fact]
    public void A_rhino_fires_at_a_standing_target_once_a_reload()
    {
        // A construction yard to the east, of 500 hit points in the test rules, falls to the tenth shell (54 each against concrete, RA2 AP).
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, new CellPos(10, 10).Center);
        var target = simulation.World.CreateBuilding(Nobody, TestRules.BuildingDef("construction_yard"), new CellPos(14, 9), true, null);
        tank.Target = target.Id;

        var shots = Shots(simulation, tank, 700);

        // The turret turns 2048 a tick from north to east, 16384: within 2048 of it after 7 ticks.
        Assert.Equal(6, shots[0].Tick);
        Assert.Equal(10, shots.Count);
        Assert.False(target.IsAlive);
        Assert.All(shots.Zip(shots.Skip(1)), pair => Assert.Equal(CannonReload, pair.Second.Tick - pair.First.Tick));
        Assert.All(shots, shot => Assert.Equal(0, shot.Slot));
    }

    [Fact]
    public void A_shot_names_the_weapon_it_was_fired_with()
    {
        // The client plays the sound of each weapon by its place in the rules (game/data/audio/bindings.yaml).
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, new CellPos(10, 10).Center);
        var target = simulation.World.CreateBuilding(Nobody, TestRules.BuildingDef("construction_yard"), new CellPos(14, 9), true, null);
        tank.Target = target.Id;

        WeaponFired? shot = null;
        for (var i = 0; i < 10 && shot is null; i++)
        {
            simulation.Step();
            shot = simulation.LastTickEvents.OfType<WeaponFired>().FirstOrDefault(fired => fired.Shooter == tank.Id);
        }

        Assert.NotNull(shot);
        Assert.Equal("heavy_tank_cannon", TestRules.Standard.Database.Weapons[shot.Weapon].Id);
    }

    [Fact]
    public void The_turret_turns_at_its_rate_and_the_hull_stays()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, new CellPos(10, 10).Center);
        var target = Spawn(simulation, Nobody, UnitType.Conscript, new CellPos(14, 10).Center);
        tank.Target = target.Id;

        var shots = Shots(simulation, tank, 3);

        Assert.Empty(shots);
        Assert.Equal(BAngle.FromUnits(3 * 2048), tank.TurretFacing);
        Assert.Equal(BAngle.North, tank.Facing);
    }

    [Fact]
    public void Infantry_turns_at_once_and_fires_on_the_first_tick()
    {
        var simulation = Match();
        var conscript = Spawn(simulation, Player1, UnitType.Conscript, new CellPos(10, 10).Center);
        var target = Spawn(simulation, Nobody, UnitType.Conscript, new CellPos(10, 13).Center);
        conscript.Target = target.Id;

        var shots = Shots(simulation, conscript, 1);

        Assert.Equal([(0, 0)], shots);
        Assert.Equal(BAngle.South, conscript.Facing);
    }

    [Theory]
    [InlineData(1024, true)]    // the rifle reaches 4 cells
    [InlineData(1025, false)]
    public void A_weapon_fires_only_within_its_range(int distance, bool fires)
    {
        var simulation = Match();
        var conscript = Spawn(simulation, Player1, UnitType.Conscript, new CellPos(10, 10).Center);
        var target = Spawn(simulation, Nobody, UnitType.Conscript, new CellPos(10, 10).Center + new WorldPos(distance, 0));
        conscript.Target = target.Id;

        Assert.Equal(fires, Shots(simulation, conscript, 2).Count > 0);
    }

    [Theory]
    [InlineData(1472, true)]    // 5.75 cells to the nearest point of the reactor
    [InlineData(1473, false)]
    public void The_range_to_a_building_is_to_the_nearest_point_of_its_footprint(int distance, bool fires)
    {
        var simulation = Match();
        var reactor = simulation.World.CreateBuilding(Nobody, TestRules.BuildingDef("reactor"), new CellPos(20, 10), true, null);
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, new WorldPos((20 * 256) - distance, (10 * 256) + 300));
        tank.TurretFacing = BAngle.East;
        tank.Target = reactor.Id;

        Assert.Equal(fires, Shots(simulation, tank, 2).Count > 0);
    }

    [Fact]
    public void A_volley_fires_its_shots_burst_delay_apart_and_the_next_one_a_reload_after_the_first()
    {
        var rules = TestRules.WithWeapon("heavy_tank_cannon", cannon => cannon with { Burst = 2, BurstDelay = 3 });
        var simulation = Match(rules);
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, new CellPos(10, 10).Center);
        var target = Spawn(simulation, Nobody, UnitType.Conscript, new CellPos(10, 6).Center);
        tank.Target = target.Id;

        var shots = Shots(simulation, tank, 140).Select(shot => shot.Tick).ToArray();

        Assert.Equal([0, 3, 65, 68, 130, 133], shots);
    }

    [Fact]
    public void A_target_that_dies_breaks_off_the_volley()
    {
        var rules = TestRules.WithWeapon("heavy_tank_cannon", cannon => cannon with { Burst = 3, BurstDelay = 5 });
        var simulation = Match(rules);
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, new CellPos(10, 10).Center);
        var target = Spawn(simulation, Nobody, UnitType.Conscript, new CellPos(10, 6).Center);
        tank.Target = target.Id;
        Shots(simulation, tank, 2);
        Assert.Equal(2, tank.Weapons[0].BurstLeft);

        target.MarkDead();
        simulation.Step();

        Assert.Null(tank.Target);
        Assert.Equal(0, tank.Weapons[0].BurstLeft);
    }

    [Fact]
    public void A_nearer_target_than_the_nearest_range_is_not_fired_at()
    {
        var rules = TestRules.WithWeapon("heavy_tank_cannon", cannon => cannon with { MinRange = Fixed.One });
        var simulation = Match(rules);
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, new CellPos(10, 10).Center);
        var near = Spawn(simulation, Nobody, UnitType.Conscript, new CellPos(10, 10).Center + new WorldPos(0, -255));
        tank.Target = near.Id;

        Assert.Empty(Shots(simulation, tank, 3));
    }

    [Fact]
    public void The_reload_counts_down_without_a_target_and_a_new_target_is_fired_at_once()
    {
        var simulation = Match();
        var conscript = Spawn(simulation, Player1, UnitType.Conscript, new CellPos(10, 10).Center);
        var first = Spawn(simulation, Nobody, UnitType.Conscript, new CellPos(10, 12).Center);
        var second = Spawn(simulation, Nobody, UnitType.Conscript, new CellPos(11, 12).Center);
        conscript.Target = first.Id;
        Assert.Single(Shots(simulation, conscript, 1));

        conscript.Target = null;
        Shots(simulation, conscript, 30);
        conscript.Target = second.Id;

        // The rifle reloads in 25 ticks; they went by without a target.
        Assert.Equal([(31, 0)], Shots(simulation, conscript, 1));
    }

    [Fact]
    public void Of_two_weapons_the_one_that_hits_ground_fires_at_a_ground_target()
    {
        var simulation = Match();
        var flakTrack = Spawn(simulation, Player1, UnitType.SupportVehicle, new CellPos(10, 10).Center);
        var target = Spawn(simulation, Nobody, UnitType.Conscript, new CellPos(10, 7).Center);
        flakTrack.Target = target.Id;

        var shots = Shots(simulation, flakTrack, 200);

        Assert.NotEmpty(shots);
        Assert.All(shots, shot => Assert.Equal(0, shot.Slot));
        Assert.Equal(2, flakTrack.Weapons.Length);
    }

    [Theory]
    [InlineData(0, true)]    // 7 cells from the centre of the coil
    [InlineData(1, false)]
    public void A_tesla_coil_fires_at_a_unit_within_seven_cells(int beyond, bool fires)
    {
        var simulation = Match();
        var coil = simulation.World.CreateBuilding(Player1, TestRules.BuildingDef("tesla_coil"), new CellPos(10, 10), true, null);
        // A reactor powers it: without one the coil would be off for want of power (step 10).
        simulation.World.CreateBuilding(Player1, TestRules.BuildingDef("reactor"), new CellPos(2, 2), true, null);
        var target = Spawn(simulation, Nobody, UnitType.HeavyTank, new WorldPos((10 * 256) + 128 + (7 * 256) + beyond, (10 * 256) + 128));
        coil.Target = target.Id;

        Assert.Equal(fires, Shots(simulation, coil, 1).Count > 0);
    }

    [Fact]
    public void An_unarmed_unit_with_a_target_does_nothing()
    {
        var simulation = Match();
        var engineer = Spawn(simulation, Player1, UnitType.Engineer, new CellPos(10, 10).Center);
        var target = Spawn(simulation, Nobody, UnitType.Conscript, new CellPos(10, 11).Center);
        engineer.Target = target.Id;

        Assert.Empty(engineer.Weapons);
        Assert.Empty(Shots(simulation, engineer, 10));
    }

    [Fact]
    public void A_turret_keeps_on_its_target_while_the_hull_drives_elsewhere()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, new CellPos(10, 10).Center);
        var target = Spawn(simulation, Nobody, UnitType.Conscript, new CellPos(14, 10).Center);
        simulation.Enqueue(new Rts.Simulation.Commands.MoveCommand(Player1, 0, 0, [tank.Id], new CellPos(10, 20).Center));
        simulation.Step();

        // A move forgets the target (step 7); given after it, the target stays while the tank drives.
        tank.Target = target.Id;
        Shots(simulation, tank, 20);

        // The tank drives south; the turret follows the bearing of the target as it changes.
        var bearing = FixedMath.Angle(target.Position.X - tank.Position.X, target.Position.Y - tank.Position.Y);
        Assert.InRange(System.Math.Abs((int)BAngle.Difference(tank.TurretFacing, bearing)), 0, WeaponSystem.AimTolerance);
        Assert.Equal(BAngle.South, tank.Facing);
    }
}
