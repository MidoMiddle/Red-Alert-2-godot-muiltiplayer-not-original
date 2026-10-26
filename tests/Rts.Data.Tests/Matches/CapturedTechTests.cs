using Rts.Client.Hud;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The technology of a building taken from the other faction, as in RA2 (the owner, 2026-10-26; <see cref="TechTree"/>): its barracks
/// train that faction's soldiers, its war factory builds its vehicles, its construction yard its buildings — not its specials, of a
/// country or of stolen technology. By the game data; two people play, no bot.
/// </summary>
public sealed class CapturedTechTests
{
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    private static GameSimulation Match() =>
        new(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "captured tech",
                    108,
                    new MatchOptions(StartingCredits: 10_000, StartingUnits: 0, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                new MapGrid(64, 64)),
            GameDataFiles.Rules);

    private static Building Build(GameWorld world, PlayerId player, string id, int x, int y)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(player, def, new CellPos(x, y), true, null);
    }

    private static Building Taken(GameWorld world, string id, int x, int y)
    {
        var building = Build(world, Allied, id, x, y);
        EntrySystem.Capture(world, building, Union, null);
        return building;
    }

    private static bool Unit(GameWorld world, string id)
    {
        TechTree.Update(world);
        return world.Player(Union).CanBuildUnit(world.Rules.Database.IndexOfUnit(id));
    }

    private static bool Building(GameWorld world, string id)
    {
        TechTree.Update(world);
        return world.Player(Union).CanBuildBuilding(world.Rules.Database.IndexOfBuilding(id));
    }

    [Fact]
    public void Allied_barracks_taken_train_the_allied_soldiers_but_not_the_specials()
    {
        var world = Match().World;
        Build(world, Union, "construction_yard", 4, 4);
        Build(world, Union, "reactor", 10, 4);
        Assert.False(Unit(world, "alliance_rifleman"));

        Taken(world, "alliance_barracks", 20, 20);

        Assert.True(Unit(world, "alliance_rifleman"));
        Assert.True(Unit(world, "alliance_engineer"));
        Assert.True(Unit(world, "alliance_attack_dog"));
        // Its own prerequisites still hold: the spy needs the Allies' battle lab.
        Assert.False(Unit(world, "alliance_spy"));
        // The specials never: the sniper of Britain, the Chrono Commando of stolen technology.
        Taken(world, "alliance_air_force_command", 30, 30);
        Assert.True(Unit(world, "alliance_rocketeer"));
        Assert.False(Unit(world, "alliance_sniper"));
        Assert.False(Unit(world, "alliance_chrono_commando"));
        // The Soviets' own soldiers need their own barracks: an allied one is in the group, and opens them too, as in RA2.
        Assert.True(Unit(world, "tesla_trooper"));
    }

    [Fact]
    public void An_allied_war_factory_taken_builds_the_allied_vehicles_but_not_those_of_a_country()
    {
        var world = Match().World;
        Build(world, Union, "construction_yard", 4, 4);
        Taken(world, "alliance_war_factory", 20, 20);
        Taken(world, "alliance_air_force_command", 30, 30);

        Assert.True(Unit(world, "alliance_light_tank"));
        Assert.True(Unit(world, "alliance_ifv"));
        Assert.False(Unit(world, "alliance_tank_destroyer"));
        // A vehicle needs a vehicle factory of its own faction: the Soviets' heavy tank does not come from an allied one.
        Assert.False(Unit(world, "heavy_tank"));
    }

    [Fact]
    public void An_allied_construction_yard_taken_builds_the_allied_base_but_not_a_country_s_building_nor_a_yard()
    {
        var world = Match().World;
        Build(world, Union, "construction_yard", 4, 4);
        Assert.False(Building(world, "alliance_power_plant"));

        Taken(world, "alliance_construction_yard", 20, 20);
        Assert.True(Building(world, "alliance_power_plant"));
        Build(world, Union, "reactor", 10, 4);
        // A Soviet reactor is a power plant of RA2's POWER group: the allied barracks need no more.
        Assert.True(Building(world, "alliance_barracks"));
        // The pillbox needs barracks, of either faction.
        Assert.False(Building(world, "alliance_pillbox"));
        Build(world, Union, "barracks", 14, 4);
        Assert.True(Building(world, "alliance_pillbox"));

        Taken(world, "alliance_air_force_command", 30, 30);
        Assert.True(Building(world, "alliance_prism_tower"));
        Assert.False(Building(world, "alliance_grand_cannon"));
        Assert.False(Building(world, "alliance_construction_yard"));
        Assert.False(Building(world, "construction_yard"));
    }

    [Fact]
    public void A_soldier_of_the_taken_barracks_is_trained_and_comes_out_and_with_the_barracks_lost_no_more_are_ordered()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Union, "construction_yard", 4, 4);
        Build(world, Union, "reactor", 10, 4);
        var barracks = Taken(world, "alliance_barracks", 20, 20);
        simulation.Step();

        var rifleman = world.Rules.Database.IndexOfUnit("alliance_rifleman");
        simulation.Enqueue(new ProduceCommand(Union, simulation.CurrentTick, 0, ProductionKind.Unit, rifleman, 1));
        UnitProduced? produced = null;
        for (var tick = 0; tick < 1500 && produced is null; tick++)
        {
            simulation.Step();
            produced = simulation.LastTickEvents.OfType<UnitProduced>().FirstOrDefault();
        }

        Assert.NotNull(produced);
        Assert.True(world.TryGet(produced!.Unit, out var entity));
        Assert.Equal((UnitType.AllianceRifleman, Union), (((Unit)entity!).Archetype.Type, entity!.Owner));

        world.DestroyBuilding(barracks, null);
        simulation.Step();
        simulation.Enqueue(new ProduceCommand(Union, simulation.CurrentTick, 1, ProductionKind.Unit, rifleman, 1));
        simulation.Step();
        Assert.Empty(world.Player(Union).Queue(ProductionCategory.Infantry));
    }

    [Fact]
    public void The_sidebar_shows_the_allied_soldiers_of_the_taken_barracks_among_the_soviet_ones()
    {
        var world = Match().World;
        Build(world, Union, "construction_yard", 4, 4);
        Build(world, Union, "reactor", 10, 4);
        Build(world, Union, "barracks", 14, 4);
        Taken(world, "alliance_barracks", 20, 20);
        TechTree.Update(world);

        var rules = world.Rules.Database;
        string[] tab = [.. SidebarModel.Cameos(world, Union, BuildGroup.Infantry).Select(cameo => rules.Units[cameo.DefIndex].Id)];

        Assert.Equal(["alliance_attack_dog", "alliance_rifleman", "alliance_engineer", "attack_dog", "conscript", "engineer", "rocketeer", "tesla_trooper"], tab);
    }
}
