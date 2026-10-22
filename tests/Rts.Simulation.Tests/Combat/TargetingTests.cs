using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Combat;

/// <summary>
/// The choice of targets (docs/implementation/M4_combat.md, 4.10): threat, who fires at me, the share against the armor, distance, the
/// current target, the smaller id; who looks and how far; the spread over ticks. Scores are worked out in the comments.
/// </summary>
public sealed class TargetingTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    private static GameWorld World() => new(seed: 1, widthCells: 40, heightCells: 40, TestRules.Standard);

    private static Unit Spawn(GameWorld world, PlayerId owner, UnitType type, int x, int y) =>
        world.SpawnUnit(owner, type, new CellPos(x, y).Center);

    /// <summary>Runs the targeting on a tick where the unit looks again.</summary>
    private static void Look(GameWorld world, Entity entity)
    {
        world.CurrentTick = (TargetingSystem.Period - (entity.Id.Value % TargetingSystem.Period)) % TargetingSystem.Period;
        TargetingSystem.Update(world);
    }

    [Fact]
    public void The_more_dangerous_enemy_comes_first()
    {
        var world = World();
        var tank = Spawn(world, Player1, UnitType.HeavyTank, 10, 10);
        Spawn(world, Player2, UnitType.Conscript, 13, 10);
        var teslaTank = Spawn(world, Player2, UnitType.TeslaTank, 10, 13);

        Look(world, tank);

        // Conscript: 100 + 25 / 2 − 15 = 97; tesla tank: 350 + 100 / 2 − 15 = 385.
        Assert.Equal(teslaTank.Id, tank.Target);
    }

    [Fact]
    public void An_enemy_that_fires_at_me_comes_first_among_equals()
    {
        var world = World();
        var tank = Spawn(world, Player1, UnitType.HeavyTank, 10, 10);
        var quiet = Spawn(world, Player2, UnitType.SupportVehicle, 13, 10);
        var shooting = Spawn(world, Player2, UnitType.SupportVehicle, 10, 13);
        shooting.Target = tank.Id;

        Look(world, tank);

        // Both: 150 + 100 / 2 − 15 = 185; the one firing at me 50 more, though its id is the larger.
        Assert.Equal(shooting.Id, tank.Target);
        Assert.True(quiet.Id.Value < shooting.Id.Value);
    }

    [Fact]
    public void Of_two_equal_threats_the_one_my_weapon_hurts_more_comes_first()
    {
        // A rocket does 100 % against heavy armor and 25 % against none: two enemies of equal threat.
        var world = World();
        var rocketeer = Spawn(world, Player1, UnitType.Rocketeer, 10, 10);
        var tank = Spawn(world, Player2, UnitType.SupportVehicle, 13, 10);     // heavy armor, threat 15
        Spawn(world, Player2, UnitType.Rocketeer, 10, 13);                     // no armor (25 %), threat 15

        Look(world, rocketeer);

        // Flak track: 150 + 100 / 2 − 15 = 185; rocket trooper: 150 + 25 / 2 − 15 = 147.
        Assert.Equal(tank.Id, rocketeer.Target);
    }

    [Fact]
    public void Of_two_equal_enemies_the_nearer_one_comes_first()
    {
        var world = World();
        var tank = Spawn(world, Player1, UnitType.HeavyTank, 10, 10);
        Spawn(world, Player2, UnitType.Conscript, 15, 10);
        var near = Spawn(world, Player2, UnitType.Conscript, 10, 13);

        Look(world, tank);

        Assert.Equal(near.Id, tank.Target);
    }

    [Fact]
    public void The_current_target_holds_until_another_is_better_by_more_than_20()
    {
        var world = World();
        var tank = Spawn(world, Player1, UnitType.HeavyTank, 10, 10);
        var current = Spawn(world, Player2, UnitType.Conscript, 13, 10);       // 100 + 12 − 15 + 20 = 117
        Spawn(world, Player2, UnitType.Conscript, 11, 10);                     // 100 + 12 − 5 = 107: not enough
        tank.Target = current.Id;

        Look(world, tank);
        Assert.Equal(current.Id, tank.Target);

        var rocketeer = Spawn(world, Player2, UnitType.Rocketeer, 10, 12);     // 150 + 12 − 10 = 152: better by 35
        Look(world, tank);
        Assert.Equal(rocketeer.Id, tank.Target);
    }

    [Fact]
    public void Equal_scores_go_to_the_smaller_id()
    {
        var world = World();
        var tank = Spawn(world, Player1, UnitType.HeavyTank, 10, 10);
        var first = Spawn(world, Player2, UnitType.Conscript, 13, 10);
        var second = Spawn(world, Player2, UnitType.Conscript, 7, 10);

        Look(world, tank);

        Assert.Equal(first.Id, tank.Target);
        Assert.True(first.Id.Value < second.Id.Value);
    }

    [Fact]
    public void A_unit_looks_again_only_on_its_tick_of_four()
    {
        var world = World();
        var tank = Spawn(world, Player1, UnitType.HeavyTank, 10, 10);
        var enemy = Spawn(world, Player2, UnitType.Conscript, 13, 10);
        var looked = new List<int>();
        for (var tick = 0; tick < 12; tick++)
        {
            world.CurrentTick = tick;
            tank.Target = null;
            TargetingSystem.Update(world);
            if (tank.Target == enemy.Id)
            {
                looked.Add(tick);
            }
        }

        Assert.All(looked, tick => Assert.Equal(0, (tick + tank.Id.Value) % 4));
        Assert.Equal(3, looked.Count);
    }

    [Fact]
    public void Unarmed_units_walls_friends_and_neutrals_are_left_alone()
    {
        var world = World();
        var engineer = Spawn(world, Player1, UnitType.Engineer, 10, 10);
        var tank = Spawn(world, Player1, UnitType.HeavyTank, 20, 10);
        world.CreateBuilding(Player2, TestRules.BuildingDef("wall"), new CellPos(22, 10), true, null);
        Spawn(world, Player1, UnitType.Conscript, 20, 12);
        Spawn(world, PlayerId.Neutral, UnitType.Conscript, 18, 10);
        Spawn(world, Player2, UnitType.Conscript, 11, 10);

        Look(world, engineer);
        Look(world, tank);

        Assert.Null(engineer.Target);
        Assert.Null(tank.Target);
    }

    [Fact]
    public void An_anti_aircraft_gun_does_not_look_for_ground_targets()
    {
        var world = World();
        var flak = world.CreateBuilding(Player1, TestRules.BuildingDef("flak_cannon"), new CellPos(10, 10), true, null);
        Spawn(world, Player2, UnitType.HeavyTank, 13, 10);

        Look(world, flak);

        Assert.Null(flak.Target);
    }

    [Fact]
    public void A_harvester_looks_only_within_its_range_and_a_rhino_within_its_sight()
    {
        var world = World();
        var harvester = Spawn(world, Player1, UnitType.Harvester, 10, 10);
        var tank = Spawn(world, Player1, UnitType.HeavyTank, 10, 20);
        Spawn(world, Player2, UnitType.Conscript, 17, 10);                       // 7 cells: beyond 5.5, within sight
        var nearTank = Spawn(world, Player2, UnitType.Conscript, 17, 20);        // 7 cells: beyond 5.75, within sight 8

        Look(world, harvester);
        Look(world, tank);

        Assert.Null(harvester.Target);
        Assert.Equal(nearTank.Id, tank.Target);
    }

    [Fact]
    public void Two_rhinos_find_each_other_and_fight_until_one_falls()
    {
        var simulation = new GameSimulation(new SimulationSettings(1, new MapGrid(40, 40, TestRules.Standard.Land)), TestRules.Standard);
        var first = simulation.World.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(10, 10).Center);
        var second = simulation.World.SpawnUnit(Player2, UnitType.HeavyTank, new CellPos(14, 10).Center);
        var destroyed = new List<EntityId>();
        for (var tick = 0; tick < 800 && destroyed.Count == 0; tick++)
        {
            simulation.Step();
            destroyed.AddRange(simulation.LastTickEvents.OfType<EntityDestroyed>().Select(e => e.Entity));
        }

        Assert.NotEmpty(destroyed);
        Assert.Subset(new HashSet<EntityId> { first.Id, second.Id }, destroyed.ToHashSet());
    }

    [Fact]
    public void A_tesla_coil_fires_by_itself_at_a_tank_that_comes_near()
    {
        var simulation = new GameSimulation(new SimulationSettings(1, new MapGrid(40, 40, TestRules.Standard.Land)), TestRules.Standard);
        var coil = simulation.World.CreateBuilding(Player1, TestRules.BuildingDef("tesla_coil"), new CellPos(10, 10), true, null);
        // A reactor powers it: without one the coil would be off for want of power (step 10).
        simulation.World.CreateBuilding(Player1, TestRules.BuildingDef("reactor"), new CellPos(2, 2), true, null);
        var tank = simulation.World.SpawnUnit(Player2, UnitType.HeavyTank, new CellPos(16, 11).Center);
        var fired = false;
        for (var tick = 0; tick < 8 && !fired; tick++)
        {
            simulation.Step();
            fired = simulation.LastTickEvents.OfType<WeaponFired>().Any(e => e.Shooter == coil.Id && e.Target == tank.Id);
        }

        Assert.True(fired);
    }

    [Fact]
    public void The_same_battle_ends_the_same_way()
    {
        ulong Battle()
        {
            var simulation = new GameSimulation(new SimulationSettings(5, new MapGrid(40, 40, TestRules.Standard.Land)), TestRules.Standard);
            foreach (var (owner, type, x, y) in new[]
            {
                (Player1, UnitType.HeavyTank, 10, 10), (Player1, UnitType.Conscript, 10, 12), (Player1, UnitType.Rocketeer, 11, 12),
                (Player2, UnitType.TeslaTank, 15, 10), (Player2, UnitType.SupportVehicle, 15, 12), (Player2, UnitType.Conscript, 16, 11),
            })
            {
                simulation.World.SpawnUnit(owner, type, new CellPos(x, y).Center);
            }

            for (var tick = 0; tick < 600; tick++)
            {
                simulation.Step();
            }

            return StateHasher.Compute(simulation.World);
        }

        Assert.Equal(Battle(), Battle());
    }
}
