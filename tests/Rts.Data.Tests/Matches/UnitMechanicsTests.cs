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
/// The mechanics of RA2 units checked against docs/ra2_units (2026-10-26): who a tank does not run over, the transports that do not crush
/// nor train, the units that heal themselves, the squid's sensor, Ivan going off when killed; a bomb carried into a transport waits there,
/// an engineer takes a bomb off, a drone inside keeps the MCV from deploying, an erased demolition truck does not blow up. By the game
/// data, without the fog of war; two people play, no bot.
/// </summary>
public sealed class UnitMechanicsTests
{
    private const int Size = 64;
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    private static GameSimulation Match()
    {
        var land = Enumerable.Repeat((byte)LandType.Clear, Size * Size).ToArray();
        var map = MapGrid.FromLayers(Size, Size, land, new byte[Size * Size], default, null, null, []);
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "mechanics",
                    83,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false, StartingUnits: 0),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                map),
            GameDataFiles.Rules);
        simulation.World.VisionOf(Union).ExploreAll();
        simulation.World.VisionOf(Allied).ExploreAll();
        return simulation;
    }

    private static Unit Spawn(GameWorld world, PlayerId player, UnitType type, int x, int y) =>
        world.SpawnUnit(player, type, new CellPos(x, y).Center, snapToSubcell: true);

    private static void Run(GameSimulation simulation, int ticks, Func<bool>? until = null)
    {
        for (var tick = 0; tick < ticks && until?.Invoke() != true; tick++)
        {
            simulation.Step();
        }
    }

    private static void Bomb(GameWorld world, Entity target)
    {
        target.BombTicks = 400;
        target.BombWeapon = world.Rules.Database.IndexOfWeapon("ivan_bomb_blast");
        target.BombOwner = Union;
    }

    private static Rts.Simulation.Rules.UnitDef Def(string id) =>
        GameDataFiles.Rules.Database.Units[GameDataFiles.Rules.Database.IndexOfUnit(id)];

    [Fact]
    public void The_data_says_who_is_not_crushed_who_does_not_crush_nor_train_who_heals_and_the_squid_is_a_sensor()
    {
        Assert.All(new[] { "tesla_trooper", "desolator", "alliance_chrono_legionnaire" }, id => Assert.False(Def(id).Crushable, id));
        Assert.False(Def("alliance_amphibious_transport").Crusher);
        Assert.All(new[] { "alliance_amphibious_transport", "hover_transport", "alliance_harvester" }, id => Assert.False(Def(id).Trainable, id));
        Assert.All(new[] { "harvester", "alliance_harvester", "kirov_airship", "giant_squid", "desolator", "apocalypse_tank" }, id => Assert.True(Def(id).SelfHeal > 0, id));
        Assert.All(new[] { "harvester", "alliance_harvester" }, id => Assert.True(Def(id).RadiationImmune, id));
        Assert.Equal(8, Def("giant_squid").Sensors);
    }

    [Fact]
    public void A_harvester_heals_itself()
    {
        var simulation = Match();
        var harvester = Spawn(simulation.World, Union, UnitType.Harvester, 20, 20);
        simulation.World.SetHp(harvester, harvester.MaxHp / 2, null);

        Run(simulation, SimulationSettings.DefaultTicksPerSecond * 5);

        Assert.True(harvester.Hp > harvester.MaxHp / 2);
    }

    [Fact]
    public void Ivan_goes_off_when_he_is_killed()
    {
        var simulation = Match();
        var world = simulation.World;
        var ivan = Spawn(world, Union, UnitType.CrazyIvan, 20, 20);
        var rifleman = Spawn(world, Allied, UnitType.AllianceRifleman, 21, 20);

        DeathSystem.Kill(world, ivan, EntityId.None, PlayerId.Neutral, DeathCause.Weapon, -1, null);
        Run(simulation, 3);

        Assert.False(rifleman.IsAlive);
    }

    [Fact]
    public void A_bomb_carried_into_a_transport_waits_and_goes_off_when_the_soldier_comes_out()
    {
        var simulation = Match();
        var world = simulation.World;
        var conscript = Spawn(world, Union, UnitType.Conscript, 20, 20);
        var transport = Spawn(world, Union, UnitType.HoverTransport, 22, 20);
        Bomb(world, conscript);

        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, [conscript.Id], transport.Id));
        Run(simulation, 200, () => transport.Passengers.Count == 1);
        Run(simulation, 30);

        Assert.Single(transport.Passengers);
        Assert.Equal(transport.MaxHp, transport.Hp);

        var bystander = Spawn(world, Allied, UnitType.AllianceRifleman, 22, 23);
        simulation.Enqueue(new DeployCommand(Union, simulation.CurrentTick, 1, transport.Id));
        Run(simulation, 4);

        Assert.Empty(transport.Passengers);
        Assert.DoesNotContain(world.Entities, entity => entity is Unit { IsAlive: true, Type: UnitType.Conscript });
        Assert.True(transport.Hp < transport.MaxHp);
        Assert.True(bystander.IsAlive);
    }

    [Fact]
    public void A_bomb_inside_goes_off_where_the_transport_falls()
    {
        var simulation = Match();
        var world = simulation.World;
        var conscript = Spawn(world, Union, UnitType.Conscript, 20, 20);
        var transport = Spawn(world, Union, UnitType.HoverTransport, 22, 20);
        Bomb(world, conscript);
        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, [conscript.Id], transport.Id));
        Run(simulation, 200, () => transport.Passengers.Count == 1);
        var rifleman = Spawn(world, Allied, UnitType.AllianceRifleman, 23, 20);

        DeathSystem.Kill(world, transport, EntityId.None, PlayerId.Neutral, DeathCause.Weapon, -1, null);
        Run(simulation, 3);

        Assert.False(rifleman.IsAlive);
    }

    [Fact]
    public void An_engineer_takes_a_bomb_off_his_building_and_stays()
    {
        var simulation = Match();
        var world = simulation.World;
        world.Rules.Database.TryGetBuilding("reactor", out var def);
        var reactor = world.CreateBuilding(Union, def, new CellPos(20, 20), true, null);
        var engineer = Spawn(world, Union, UnitType.Engineer, 20, 25);
        Bomb(world, reactor);
        Assert.Equal(EntryAction.Defuse, EntrySystem.ActionOf(world, engineer, reactor));

        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, [engineer.Id], reactor.Id));
        Run(simulation, 200, () => reactor.BombTicks == 0);
        Run(simulation, 450);

        Assert.True(engineer.IsAlive);
        Assert.Equal(0, reactor.BombTicks);
        Assert.Equal(reactor.MaxHp, reactor.Hp);
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, engineer, reactor));
    }

    [Fact]
    public void A_drone_inside_keeps_the_mcv_from_deploying()
    {
        var simulation = Match();
        var world = simulation.World;
        var mcv = Spawn(world, Allied, UnitType.AllianceMcv, 30, 30);
        var drone = Spawn(world, Union, UnitType.TerrorDrone, 34, 30);
        simulation.Enqueue(new AttackCommand(Union, simulation.CurrentTick, 0, [drone.Id], mcv.Id));
        Run(simulation, 300, () => mcv.Parasite != 0);
        Assert.Equal(UnitType.TerrorDrone, mcv.Parasite);

        var refused = new DeployCommand(Allied, simulation.CurrentTick, 0, mcv.Id);
        simulation.Enqueue(refused);
        Run(simulation, 2);

        Assert.True(mcv.IsAlive);
        Assert.Contains(refused, simulation.RefusedCommands);
    }

    [Fact]
    public void An_erased_demolition_truck_does_not_blow_up()
    {
        var simulation = Match();
        var world = simulation.World;
        var legionnaire = Spawn(world, Allied, UnitType.AllianceChronoLegionnaire, 20, 20);
        var truck = Spawn(world, Union, UnitType.DemolitionTruck, 24, 20);
        var bystander = Spawn(world, Allied, UnitType.AllianceMcv, 25, 22);

        simulation.Enqueue(new AttackCommand(Allied, simulation.CurrentTick, 0, [legionnaire.Id], truck.Id));
        Run(simulation, 3000, () => !truck.IsAlive);
        Run(simulation, 5);

        Assert.False(truck.IsAlive);
        Assert.Equal(bystander.MaxHp, bystander.Hp);
    }
}
