using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// What D does as in RA2 besides the MCV: the Rifleman digs in behind sandbags and fires its machine gun (RA2 Para), without armor
/// from them; the construction yard packs up into its MCV. By the game data; two people play, no bot.
/// </summary>
public sealed class DeployKeyTests
{
    private static readonly PlayerId Allied = new(1);
    private static readonly PlayerId Union = new(2);

    private static GameSimulation Match() =>
        new(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "deploy",
                    67,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                new MapGrid(64, 64)),
            GameDataFiles.Rules);

    private static List<SimEvent> Run(GameSimulation simulation, int ticks)
    {
        var events = new List<SimEvent>();
        for (var tick = 0; tick < ticks; tick++)
        {
            simulation.Step();
            events.AddRange(simulation.LastTickEvents);
        }

        return events;
    }

    [Fact]
    public void The_rifleman_digs_in_fires_its_machine_gun_and_gets_up_again()
    {
        var simulation = Match();
        var world = simulation.World;
        var rifleman = world.SpawnUnit(Allied, UnitType.AllianceRifleman, new CellPos(30, 30).Center, snapToSubcell: true);
        var rules = world.Rules;
        Assert.Equal("alliance_rifle", rules.Database.Weapons[rules.WeaponsOf(rifleman)[0]].Id);

        simulation.Enqueue(new DeployCommand(Allied, simulation.CurrentTick, 0, rifleman.Id));
        Run(simulation, 1);
        Assert.True(rifleman.Deployed);
        var gun = rules.Database.Weapons[rules.WeaponsOf(rifleman)[0]];
        Assert.Equal(("alliance_rifle_deployed", 15), (gun.Id, gun.Damage));
        Assert.True(gun.Range > rules.Database.Weapons[rules.Database.IndexOfWeapon("alliance_rifle")].Range);

        // Dug in, it stays; the sandbags give no armor (RA2).
        var at = rifleman.Position;
        simulation.Enqueue(new DeployCommand(Allied, simulation.CurrentTick, 1, rifleman.Id));
        Run(simulation, 1);
        Assert.False(rifleman.Deployed);
        Assert.Equal(at, rifleman.Position);
        Assert.Equal(rules.Unit(UnitType.AllianceRifleman).Armor, rifleman.Archetype.Armor);
    }

    [Fact]
    public void The_construction_yard_packs_up_into_its_mcv_with_its_share_of_hit_points()
    {
        var simulation = Match();
        var world = simulation.World;
        var mcv = world.Entities.OfType<Unit>().Single(unit => unit.Owner == Allied && unit.Type == UnitType.AllianceMcv);
        simulation.Enqueue(new DeployCommand(Allied, simulation.CurrentTick, 0, mcv.Id));
        Run(simulation, 200);
        var yard = world.Entities.OfType<Building>().Single(building => building.IsAlive && building.Owner == Allied);
        Assert.True(yard.IsReady);
        world.SetHp(yard, yard.MaxHp / 2, null);

        simulation.Enqueue(new DeployCommand(Allied, simulation.CurrentTick, 1, yard.Id));
        var events = Run(simulation, 2);

        Assert.False(yard.IsAlive);
        var packed = world.Entities.OfType<Unit>().Single(unit => unit.IsAlive && unit.Owner == Allied && unit.Type == UnitType.AllianceMcv);
        Assert.Contains(events, e => e is Undeployed undeployed && undeployed.Building == yard.Id && undeployed.Unit == packed.Id);
        Assert.DoesNotContain(events, e => e is BuildingDestroyed);
        Assert.InRange(packed.Hp, packed.MaxHp / 2 - 1, packed.MaxHp / 2 + 1);

        // And it deploys again where it stands.
        simulation.Enqueue(new DeployCommand(Allied, simulation.CurrentTick, 2, packed.Id));
        Run(simulation, 2);
        Assert.Contains(world.Entities.OfType<Building>(), building => building.IsAlive && building.Owner == Allied && building.Cell == yard.Cell);
    }

    [Fact]
    public void Packed_up_the_yard_builds_nothing_its_building_in_work_is_paid_back_and_deployed_again_it_builds()
    {
        // The owner, 2026-10-27: while the yard is packed into its MCV, no new building.
        var simulation = Match();
        var world = simulation.World;
        var mcv = world.Entities.OfType<Unit>().Single(unit => unit.Owner == Allied && unit.Type == UnitType.AllianceMcv);
        simulation.Enqueue(new DeployCommand(Allied, simulation.CurrentTick, 0, mcv.Id));
        Run(simulation, 200);
        var yard = world.Entities.OfType<Building>().Single(building => building.IsAlive && building.Owner == Allied);
        var plant = world.Rules.Database.IndexOfBuilding("alliance_power_plant");
        var player = world.Player(Allied);
        simulation.Enqueue(new ProduceCommand(Allied, simulation.CurrentTick, 1, ProductionKind.Building, plant, 1));
        Run(simulation, 30);
        Assert.Single(player.Queue(ProductionCategory.Building));
        var credits = player.Credits;

        simulation.Enqueue(new DeployCommand(Allied, simulation.CurrentTick, 2, yard.Id));
        Run(simulation, 2);
        // The building in work is cancelled with what was paid for it, and no new one is taken.
        Assert.Empty(player.Queue(ProductionCategory.Building));
        Assert.True(player.Credits > credits);
        simulation.Enqueue(new ProduceCommand(Allied, simulation.CurrentTick, 3, ProductionKind.Building, plant, 1));
        Run(simulation, 2);
        Assert.Empty(player.Queue(ProductionCategory.Building));

        // Driven off and deployed again elsewhere, it builds again.
        var packed = world.Entities.OfType<Unit>().Single(unit => unit.IsAlive && unit.Owner == Allied && unit.Type == UnitType.AllianceMcv);
        simulation.Enqueue(new MoveCommand(Allied, simulation.CurrentTick, 4, [packed.Id], new CellPos(20, 12).Center));
        Run(simulation, 300);
        simulation.Enqueue(new DeployCommand(Allied, simulation.CurrentTick, 5, packed.Id));
        Run(simulation, 200);
        Assert.Contains(world.Entities.OfType<Building>(), building => building.IsAlive && building.Owner == Allied && building.IsReady && building.Cell != yard.Cell);
        simulation.Enqueue(new ProduceCommand(Allied, simulation.CurrentTick, 6, ProductionKind.Building, plant, 1));
        Run(simulation, 2);
        Assert.Single(player.Queue(ProductionCategory.Building));
    }

    [Fact]
    public void A_yard_taken_from_the_other_faction_packs_up_into_its_mcv_and_deploys_again_into_its_yard()
    {
        var simulation = Match();
        var world = simulation.World;
        world.Rules.Database.TryGetBuilding("construction_yard", out var def);
        var yard = world.CreateBuilding(Union, def, new CellPos(30, 30), true, null);
        Rts.Simulation.Systems.EntrySystem.Capture(world, yard, Allied, null);

        simulation.Enqueue(new DeployCommand(Allied, simulation.CurrentTick, 0, yard.Id));
        Run(simulation, 2);
        Assert.False(yard.IsAlive);
        var mcv = world.Entities.OfType<Unit>().Single(unit => unit.IsAlive && unit.Owner == Allied && unit.Type == UnitType.Mcv);

        simulation.Enqueue(new DeployCommand(Allied, simulation.CurrentTick, 1, mcv.Id));
        Run(simulation, 2);
        Assert.Contains(world.Entities.OfType<Building>(), building => building.IsAlive && building.Owner == Allied && building.Def!.Id == "construction_yard");
    }

    [Fact]
    public void An_enemy_construction_yard_does_not_pack_up_for_another_player()
    {
        var simulation = Match();
        var world = simulation.World;
        world.Rules.Database.TryGetBuilding("construction_yard", out var def);
        var yard = world.CreateBuilding(Union, def, new CellPos(40, 40), true, null);
        simulation.Enqueue(new DeployCommand(Allied, simulation.CurrentTick, 0, yard.Id));
        Run(simulation, 2);
        Assert.True(yard.IsAlive);
    }
}
