using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// Stage 3d of filling the game: the demolition truck, the combat drone that eats a vehicle from inside, the transports, the IFV that fires
/// its passenger's weapon, the robot tank that works only with a drone control hub. By the game data, without the fog of war; two people
/// play, no bot.
/// </summary>
public sealed class CarriersAndDronesTests
{
    private static readonly PlayerId Allied = new(1);
    private static readonly PlayerId Union = new(2);

    private static GameSimulation Match()
    {
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "carriers",
                    29,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                new MapGrid(64, 64)),
            GameDataFiles.Rules);
        simulation.World.VisionOf(Allied).ExploreAll();
        simulation.World.VisionOf(Union).ExploreAll();
        return simulation;
    }

    private static Unit Spawn(GameWorld world, PlayerId player, UnitType type, int x, int y) =>
        world.SpawnUnit(player, type, new CellPos(x, y).Center, snapToSubcell: true);

    private static Building Build(GameWorld world, PlayerId player, string id, int x, int y)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(player, def, new CellPos(x, y), true, null);
    }

    private static List<SimEvent> Run(GameSimulation simulation, int ticks, Func<bool>? until = null)
    {
        var events = new List<SimEvent>();
        for (var tick = 0; tick < ticks && until?.Invoke() != true; tick++)
        {
            simulation.Step();
            events.AddRange(simulation.LastTickEvents);
        }

        return events;
    }

    [Fact]
    public void The_demolition_truck_goes_off_only_when_ordered_and_leaves_some_toxin()
    {
        var simulation = Match();
        var world = simulation.World;
        var truck = Spawn(world, Union, UnitType.DemolitionTruck, 30, 30);
        var mcv = Spawn(world, Allied, UnitType.AllianceMcv, 33, 30);
        Run(simulation, 60);
        Assert.True(truck.IsAlive);
        Assert.Null(truck.Target);

        simulation.Enqueue(new AttackCommand(Union, simulation.CurrentTick, 0, [truck.Id], mcv.Id));
        Run(simulation, 120, () => mcv.Hp < mcv.MaxHp);
        Run(simulation, 2);

        Assert.False(truck.IsAlive);
        Assert.True(mcv.Hp < mcv.MaxHp);
        Assert.True(world.RadiationAt(new CellPos(33, 30)) > 0 || world.RadiationAt(new CellPos(32, 30)) > 0);
    }

    [Fact]
    public void The_drone_goes_into_a_vehicle_eats_it_and_comes_out_when_it_falls()
    {
        var simulation = Match();
        var world = simulation.World;
        var drone = Spawn(world, Union, UnitType.TerrorDrone, 30, 30);
        var mcv = Spawn(world, Allied, UnitType.AllianceMcv, 32, 30);
        Run(simulation, 120, () => mcv.Parasite != 0);

        Assert.False(drone.IsAlive);
        Assert.Equal(UnitType.TerrorDrone, mcv.Parasite);
        var full = mcv.Hp;
        Run(simulation, 61);
        Assert.Equal(full - 50, mcv.Hp);

        world.SetHp(mcv, 10, null);
        var events = Run(simulation, 61);
        Assert.False(mcv.IsAlive);
        Assert.Single(world.Entities.OfType<Unit>(), unit => unit.IsAlive && unit.Type == UnitType.TerrorDrone && unit.Owner == Union);
        Assert.Contains(events, e => e is EntityDestroyed destroyed && destroyed.Entity == mcv.Id && destroyed.KillerOwner == Union);
    }

    [Fact]
    public void The_drone_kills_a_soldier_at_once_and_a_workshop_pulls_it_out_of_a_vehicle()
    {
        var simulation = Match();
        var world = simulation.World;
        var drone = Spawn(world, Union, UnitType.TerrorDrone, 30, 30);
        var soldier = Spawn(world, Allied, UnitType.AllianceRifleman, 31, 30);
        Run(simulation, 60, () => !soldier.IsAlive);
        Assert.False(soldier.IsAlive);
        Assert.True(drone.IsAlive);

        Build(world, Allied, "alliance_power_plant", 44, 40);
        Build(world, Allied, "alliance_service_depot", 40, 40);
        var tank = Spawn(world, Allied, UnitType.AllianceLightTank, 43, 41);
        simulation.Enqueue(new AttackCommand(Union, simulation.CurrentTick, 0, [drone.Id], tank.Id));
        Run(simulation, 300, () => tank.Parasite != 0);
        Assert.Equal(UnitType.TerrorDrone, tank.Parasite);

        var events = Run(simulation, 200, () => tank.Parasite == 0);
        Assert.Equal(0, (int)tank.Parasite);
        Assert.Contains(events, e => e is ParasiteKilled killed && killed.Host == tank.Id);
        Assert.DoesNotContain(world.Entities.OfType<Unit>(), unit => unit.IsAlive && unit.Type == UnitType.TerrorDrone);
    }

    [Fact]
    public void Soldiers_and_a_tank_board_the_landing_craft_and_come_out_on_the_order()
    {
        var simulation = Match();
        var world = simulation.World;
        var craft = Spawn(world, Allied, UnitType.AllianceAmphibiousTransport, 30, 30);
        var riflemen = new[] { Spawn(world, Allied, UnitType.AllianceRifleman, 26, 30), Spawn(world, Allied, UnitType.AllianceRifleman, 26, 31), Spawn(world, Allied, UnitType.AllianceRifleman, 26, 32) };
        var tank = Spawn(world, Allied, UnitType.AllianceLightTank, 34, 33);
        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [.. riflemen.Select(r => r.Id), tank.Id], craft.Id));

        Run(simulation, 300, () => craft.Passengers.Count == 4);
        Assert.Equal(4, craft.Passengers.Count);
        Assert.Equal(6, TransportSystem.SlotsTaken(world, craft));
        Assert.All(riflemen, rifleman => Assert.False(rifleman.IsAlive));

        simulation.Enqueue(new DeployCommand(Allied, simulation.CurrentTick, 0, craft.Id));
        var events = Run(simulation, 1);
        Assert.Empty(craft.Passengers);
        Assert.Equal(4, events.OfType<UnitUnloaded>().Count());
    }

    [Fact]
    public void The_transport_takes_no_more_than_its_slots()
    {
        var simulation = Match();
        var world = simulation.World;
        var ifv = Spawn(world, Allied, UnitType.AllianceIfv, 30, 30);
        var first = Spawn(world, Allied, UnitType.AllianceRifleman, 27, 30);
        var second = Spawn(world, Allied, UnitType.AllianceRifleman, 27, 31);
        var tank = Spawn(world, Allied, UnitType.AllianceLightTank, 33, 30);

        Assert.False(TransportSystem.CanBoard(world, tank, ifv), "an IFV takes a soldier only");
        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [first.Id, second.Id], ifv.Id));
        Run(simulation, 300);

        Assert.Single(ifv.Passengers);
        Assert.True(first.IsAlive ^ second.IsAlive);
    }

    [Fact]
    public void The_IFV_fires_its_missiles_empty_and_its_passengers_weapon_with_one_inside()
    {
        var simulation = Match();
        var world = simulation.World;
        var rules = world.Rules;
        var ifv = Spawn(world, Allied, UnitType.AllianceIfv, 20, 20);
        Assert.Equal(rules.Database.IndexOfWeapon("alliance_ifv_missiles"), rules.WeaponsOf(ifv)[0]);

        ifv.Passengers.Add(new Occupant(rules.Unit(UnitType.AllianceSniper), 125));
        Assert.Equal(rules.Database.IndexOfWeapon("ifv_sniper"), rules.WeaponsOf(ifv)[0]);

        var engineer = Spawn(world, Union, UnitType.Engineer, 32, 20);
        Run(simulation, 120, () => !engineer.IsAlive);
        Assert.False(engineer.IsAlive);
    }

    [Fact]
    public void The_robot_tank_works_only_while_a_powered_hub_stands()
    {
        var simulation = Match();
        var world = simulation.World;
        var robot = Spawn(world, Allied, UnitType.AllianceRobotTank, 30, 30);
        var mcv = Spawn(world, Union, UnitType.Mcv, 33, 30);
        var at = robot.Position;
        simulation.Enqueue(new MoveCommand(Allied, simulation.CurrentTick, 0, [robot.Id], new CellPos(30, 20).Center));
        Run(simulation, 60);

        Assert.False(world.Player(Allied).RobotsOnline);
        Assert.Equal(at, robot.Position);
        Assert.Equal(mcv.MaxHp, mcv.Hp);

        Build(world, Allied, "alliance_power_plant", 44, 40);
        Build(world, Allied, "alliance_robot_control_center", 40, 40);
        Run(simulation, 120);
        Assert.True(world.Player(Allied).RobotsOnline);
        Assert.NotEqual(at, robot.Position);
    }
}
