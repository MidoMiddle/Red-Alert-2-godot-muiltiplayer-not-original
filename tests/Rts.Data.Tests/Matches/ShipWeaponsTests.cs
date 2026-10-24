using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The weapons of the ships (stage 3e of filling the game): the submarine under the water, found only by sensors and hit only by weapons
/// at it; torpedoes at ships only; the squid's grip; the dreadnought's missiles anti-air can bring down; the cruiser at the air only; the
/// carrier's Hornets. A sea of deep water across the middle of the map, without the fog of war; two people play, no bot.
/// </summary>
public sealed class ShipWeaponsTests
{
    private static readonly PlayerId Allied = new(1);
    private static readonly PlayerId Union = new(2);

    private static GameSimulation Match()
    {
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "sea",
                    31,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                new MapGrid(64, 64)),
            GameDataFiles.Rules);
        var world = simulation.World;
        for (var y = 20; y <= 44; y++)
        {
            for (var x = 6; x <= 58; x++)
            {
                world.Map.SetLand(new CellPos(x, y), LandType.Deep);
            }
        }

        world.VisionOf(Allied).ExploreAll();
        world.VisionOf(Union).ExploreAll();
        return simulation;
    }

    private static Unit Spawn(GameWorld world, PlayerId player, UnitType type, int x, int y) =>
        world.SpawnUnit(player, type, new CellPos(x, y).Center, snapToSubcell: true);

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
    public void The_submarine_is_found_only_by_a_sensor_and_hit_only_by_weapons_under_the_water()
    {
        var simulation = Match();
        var world = simulation.World;
        var sub = Spawn(world, Union, UnitType.TyphoonSub, 30, 30);
        var tank = Spawn(world, Allied, UnitType.AllianceLightTank, 30, 18);
        var destroyer = Spawn(world, Allied, UnitType.AllianceDestroyer, 50, 40);
        Run(simulation, 2);

        Assert.False(Vision.Sees(world, Allied, sub), "no sensor in reach");
        Assert.False(TargetingSystem.CanHurt(world.Rules, UnitType.AllianceLightTank, sub));
        Assert.True(TargetingSystem.CanHurt(world.Rules, UnitType.AllianceDestroyer, sub));

        world.SetHp(destroyer, destroyer.MaxHp, null);
        var near = Spawn(world, Allied, UnitType.AllianceDestroyer, 36, 30);
        Run(simulation, 2);
        Assert.True(Vision.Sees(world, Allied, sub));
        Run(simulation, 200, () => sub.Hp < sub.MaxHp);
        Assert.True(sub.Hp < sub.MaxHp, "the depth charges hit it");
        Assert.Equal(tank.MaxHp, tank.Hp);
        Assert.True(near.IsAlive);
    }

    [Fact]
    public void The_destroyer_launches_its_osprey_at_a_submarine_and_takes_it_back_aboard()
    {
        var simulation = Match();
        var world = simulation.World;
        var sub = Spawn(world, Union, UnitType.TyphoonSub, 30, 30);
        var destroyer = Spawn(world, Allied, UnitType.AllianceDestroyer, 36, 30);
        Assert.Equal(1, destroyer.SpawnsDocked);

        var events = Run(simulation, 300, () => sub.Hp < sub.MaxHp);
        var launched = Assert.Single(events.OfType<SpawnLaunched>());
        Assert.True(world.TryGet(launched.Unit, out var entity));
        var osprey = Assert.IsType<Unit>(entity);
        Assert.Equal(UnitType.AllianceOsprey, osprey.Type);
        Assert.True(sub.Hp < sub.MaxHp, "its charges hit it");

        Run(simulation, 1200, () => destroyer.SpawnsDocked == 1);
        Assert.Equal(1, destroyer.SpawnsDocked);
        Assert.False(osprey.IsAlive);
    }

    [Fact]
    public void The_numbers_of_ra2_the_hornet_s_five_bombs_and_the_cruiser_s_volley_of_three()
    {
        var rules = Match().World.Rules;
        var hornet = rules.Unit(UnitType.AllianceHornet);
        Assert.Equal((75, 5), (hornet.Hp, hornet.Ammo));
        Assert.Equal((80, 3), (rules.Database.Weapons[hornet.Primary].Damage, rules.Unit(UnitType.AllianceAegisCruiser) is var cruiser ? rules.Database.Weapons[cruiser.Primary].Burst : 0));
        var osprey = rules.Unit(UnitType.AllianceOsprey);
        Assert.Equal((135, 5, 50), (osprey.Hp, osprey.Ammo, rules.Database.Weapons[osprey.Primary].Damage));
        Assert.Equal(60, rules.Database.Weapons[rules.Unit(UnitType.AllianceDestroyer).Primary].Damage);
    }

    [Fact]
    public void Torpedoes_hit_a_ship_and_never_a_tank_on_the_shore()
    {
        var simulation = Match();
        var world = simulation.World;
        Spawn(world, Union, UnitType.TyphoonSub, 30, 30);
        var tank = Spawn(world, Allied, UnitType.AllianceLightTank, 30, 18);
        var cruiser = Spawn(world, Allied, UnitType.AllianceAegisCruiser, 33, 30);

        Assert.False(TargetingSystem.CanHurt(world.Rules, UnitType.TyphoonSub, tank));
        Run(simulation, 300, () => cruiser.Hp < cruiser.MaxHp);
        Assert.True(cruiser.Hp < cruiser.MaxHp);
        Assert.Equal(tank.MaxHp, tank.Hp);
        Assert.False(TargetingSystem.CanHurt(world.Rules, UnitType.AllianceAegisCruiser, Spawn(world, Union, UnitType.SeaScorpion, 50, 40)), "the cruiser fires at the air only");
    }

    [Fact]
    public void The_squid_grips_a_ship_holds_it_still_and_crushes_it()
    {
        var simulation = Match();
        var world = simulation.World;
        var squid = Spawn(world, Union, UnitType.GiantSquid, 30, 30);
        var ship = Spawn(world, Allied, UnitType.AllianceAegisCruiser, 31, 30);
        Run(simulation, 120, () => ship.Parasite != 0);

        Assert.Equal(UnitType.GiantSquid, ship.Parasite);
        Assert.False(squid.IsAlive);
        var (at, hp) = (ship.Position, ship.Hp);
        simulation.Enqueue(new MoveCommand(Allied, simulation.CurrentTick, 0, [ship.Id], new CellPos(50, 30).Center));
        // RA2 SquidGrab: 15 every 99 frames.
        Run(simulation, 110);

        Assert.Equal(at, ship.Position);
        Assert.True(ship.Hp < hp);
    }

    [Fact]
    public void The_dreadnought_fires_only_when_ordered_and_a_cruiser_brings_its_missiles_down()
    {
        var simulation = Match();
        var world = simulation.World;
        var dreadnought = Spawn(world, Union, UnitType.Dreadnought, 12, 30);
        var target = Spawn(world, Allied, UnitType.AllianceDestroyer, 30, 30);
        Run(simulation, 60);
        Assert.Null(dreadnought.Target);

        var cruiser = Spawn(world, Allied, UnitType.AllianceAegisCruiser, 28, 32);
        simulation.Enqueue(new AttackCommand(Union, simulation.CurrentTick, 0, [dreadnought.Id], target.Id));
        var events = Run(simulation, 400);

        Assert.Contains(events, e => e is WeaponFired fired && fired.Shooter == dreadnought.Id);
        // One cruiser brings missiles down, though not all of a dreadnought's volleys.
        Assert.Contains(events, e => e is EntityDestroyed destroyed && destroyed.Killer == cruiser.Id);
    }

    [Fact]
    public void The_carrier_sends_three_Hornets_when_ordered()
    {
        var simulation = Match();
        var world = simulation.World;
        var carrier = Spawn(world, Allied, UnitType.AllianceAircraftCarrier, 12, 30);
        var target = Spawn(world, Union, UnitType.Dreadnought, 30, 30);
        simulation.Enqueue(new AttackCommand(Allied, simulation.CurrentTick, 0, [carrier.Id], target.Id));
        var events = Run(simulation, 300, () => target.Hp < target.MaxHp - 100);

        Assert.Equal(3, events.OfType<SpawnLaunched>().Count(launched => launched.Mother == carrier.Id));
        Assert.True(target.Hp <= target.MaxHp - 100);
    }

    [Fact]
    public void The_sea_scorpion_fires_at_a_jet_and_the_dolphin_at_a_submarine()
    {
        var simulation = Match();
        var world = simulation.World;
        Spawn(world, Union, UnitType.SeaScorpion, 30, 30);
        var jet = Spawn(world, Allied, UnitType.AllianceHarrier, 33, 30);
        Run(simulation, 120, () => jet.Hp < jet.MaxHp);
        Assert.True(!jet.IsAlive || jet.Hp < jet.MaxHp);

        var sub = Spawn(world, Union, UnitType.TyphoonSub, 40, 40);
        Spawn(world, Allied, UnitType.AllianceDolphin, 43, 40);
        Run(simulation, 120, () => sub.Hp < sub.MaxHp);
        Assert.True(sub.Hp < sub.MaxHp);
    }
}
