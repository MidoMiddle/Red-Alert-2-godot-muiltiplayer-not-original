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
/// The loose ends of stages 2 and 3 of filling the game: the super heavy tank heals itself, the rocket artillery backs off a target too
/// near, the IFV with an engineer mends vehicles, the dolphin frees a ship from the squid, a player builds no more jets than its pads, and
/// the aircraft carrier's Hornets fly out, strike and land back. By the game data, without the fog of war; two people play, no bot.
/// </summary>
public sealed class LooseEndsTests
{
    private static readonly PlayerId Allied = new(1);
    private static readonly PlayerId Union = new(2);

    private static GameSimulation Match(bool sea = false)
    {
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "ends",
                    41,
                    new MatchOptions(StartingCredits: 30_000, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                new MapGrid(64, 64)),
            GameDataFiles.Rules);
        var world = simulation.World;
        if (sea)
        {
            for (var y = 20; y <= 44; y++)
            {
                for (var x = 6; x <= 58; x++)
                {
                    world.Map.SetLand(new CellPos(x, y), LandType.Deep);
                }
            }
        }

        world.VisionOf(Allied).ExploreAll();
        world.VisionOf(Union).ExploreAll();
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
    public void The_super_heavy_tank_heals_itself_a_little_every_second()
    {
        var simulation = Match();
        var world = simulation.World;
        var tank = Spawn(world, Union, UnitType.ApocalypseTank, 30, 30);
        world.SetHp(tank, 500, null);
        Run(simulation, 61);

        Assert.InRange(tank.Hp, 506, 510);
    }

    [Fact]
    public void The_rocket_artillery_backs_off_a_target_too_near_and_then_fires()
    {
        var simulation = Match();
        var world = simulation.World;
        var launcher = Spawn(world, Union, UnitType.RocketArtillery, 30, 30);
        var mcv = Spawn(world, Allied, UnitType.AllianceMcv, 33, 30);
        simulation.Enqueue(new AttackCommand(Union, simulation.CurrentTick, 0, [launcher.Id], mcv.Id));

        var events = Run(simulation, 600, () => mcv.Hp < mcv.MaxHp);
        Assert.Single(events.OfType<WeaponFired>(), e => e.Shooter == launcher.Id);
        Assert.True(launcher.Position.ToCell().X <= 28, "it backed off to the west");
        Assert.True(mcv.Hp < mcv.MaxHp);
    }

    [Fact]
    public void The_IFV_with_an_engineer_mends_the_vehicles_of_its_team_and_hurts_no_enemy()
    {
        var simulation = Match();
        var world = simulation.World;
        var ifv = Spawn(world, Allied, UnitType.AllianceIfv, 30, 30);
        ifv.Passengers.Add(new Occupant(world.Rules.Unit(UnitType.AllianceEngineer), 75));
        var tank = Spawn(world, Allied, UnitType.AllianceLightTank, 32, 30);
        world.SetHp(tank, 100, null);
        var enemy = Spawn(world, Union, UnitType.Mcv, 30, 33);

        Run(simulation, 200, () => tank.Hp > 100);
        Assert.True(tank.Hp > 100);
        Assert.Equal(enemy.MaxHp, enemy.Hp);
    }

    [Fact]
    public void The_dolphin_frees_a_ship_from_the_squid()
    {
        var simulation = Match(sea: true);
        var world = simulation.World;
        Spawn(world, Union, UnitType.GiantSquid, 30, 30);
        var ship = Spawn(world, Allied, UnitType.AllianceAegisCruiser, 31, 30);
        Run(simulation, 120, () => ship.Parasite != 0);
        Assert.Equal(UnitType.GiantSquid, ship.Parasite);

        Spawn(world, Allied, UnitType.AllianceDolphin, 34, 30);
        var events = Run(simulation, 200, () => ship.Parasite == 0);

        Assert.Equal(0, (int)ship.Parasite);
        Assert.Contains(events, e => e is ParasiteFreed freed && freed.Host == ship.Id);
        Assert.Contains(world.Entities.OfType<Unit>(), unit => unit.IsAlive && unit.Type == UnitType.GiantSquid);
    }

    [Fact]
    public void A_player_builds_no_more_jets_than_the_pads_of_its_air_commands()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Allied, "alliance_power_plant", 6, 10);
        Build(world, Allied, "alliance_air_force_command", 10, 10);
        Spawn(world, Allied, UnitType.AllianceHarrier, 20, 20);
        TechTree.Update(world);
        var jet = world.Rules.Database.IndexOfUnit("alliance_black_eagle");

        Assert.Equal(3, AmmoSystem.FreePads(world, Allied));
        simulation.Enqueue(new ProduceCommand(Allied, simulation.CurrentTick, 0, ProductionKind.Unit, jet, 5));
        Run(simulation, 1);
        Assert.Equal(3, world.Player(Allied).Queue(Rts.Simulation.Rules.ProductionCategory.Aircraft).Count);
        Assert.Equal(0, AmmoSystem.FreePads(world, Allied));
    }

    [Fact]
    public void The_carrier_launches_its_Hornets_which_strike_and_land_back_and_one_lost_is_replaced()
    {
        var simulation = Match(sea: true);
        var world = simulation.World;
        var carrier = Spawn(world, Allied, UnitType.AllianceAircraftCarrier, 12, 30);
        var target = Spawn(world, Union, UnitType.Dreadnought, 30, 30);
        Assert.Equal(3, carrier.SpawnsDocked);
        simulation.Enqueue(new AttackCommand(Allied, simulation.CurrentTick, 0, [carrier.Id], target.Id));

        var events = Run(simulation, 120, () => carrier.SpawnsDocked == 0);
        Assert.Equal(3, events.OfType<SpawnLaunched>().Count());
        var hornets = world.Entities.OfType<Unit>().Where(unit => unit.IsAlive && unit.Mother == carrier.Id).ToList();
        Assert.Equal(3, hornets.Count);

        Run(simulation, 300, () => target.Hp < target.MaxHp);
        Assert.True(target.Hp < target.MaxHp, "the Hornets struck");

        // One is shot down on the way back; the others land.
        simulation.Enqueue(new StopCommand(Allied, simulation.CurrentTick, 1, [carrier.Id]));
        world.SetHp(hornets[0], 1, null);
        world.Hits.Add(new Rts.Simulation.Combat.Hit(hornets[0].Id, target.Id, Union, world.Rules.Database.IndexOfWeapon("flak_cannon_gun"), hornets[0].Position, hornets[0].Id));
        Run(simulation, 300, () => carrier.SpawnsDocked == 2);
        Assert.Equal(2, carrier.SpawnsDocked);
        Assert.DoesNotContain(world.Entities.OfType<Unit>(), unit => unit.IsAlive && unit.Mother == carrier.Id);

        Run(simulation, 27 * 15 + 1);
        Assert.Equal(3, carrier.SpawnsDocked);
    }
}
