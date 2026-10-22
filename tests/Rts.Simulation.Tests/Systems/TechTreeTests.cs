using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Systems;

/// <summary>
/// What a player may build (docs/implementation/M3_base_economy.md, 4.7, 6): the definitions of its faction whose prerequisites are
/// all ready buildings of the player, worked out when the buildings change.
/// </summary>
public sealed class TechTreeTests
{
    private static readonly PlayerId Player1 = new(1);

    private static GameWorld NewWorld() => new(seed: 1, widthCells: 40, heightCells: 40, TestRules.Standard);

    private static Building Build(GameWorld world, string id, int x, int y, bool ready = true) =>
        world.CreateBuilding(Player1, TestRules.BuildingDef(id), new CellPos(x, y), ready, null);

    private static bool CanBuild(GameWorld world, string id)
    {
        var rules = world.Rules.Database;
        var player = world.Player(Player1);
        var building = rules.IndexOfBuilding(id);
        return building >= 0 ? player.CanBuildBuilding(building) : player.CanBuildUnit(rules.IndexOfUnit(id));
    }

    private static string[] Available(GameWorld world)
    {
        TechTree.Update(world);
        var rules = world.Rules.Database;
        return rules.Buildings.Select(b => b.Id).Concat(rules.Units.Select(u => u.Id)).Where(id => CanBuild(world, id)).Order().ToArray();
    }

    [Fact]
    public void With_a_construction_yard_the_reactor_and_the_wall_open()
    {
        var world = NewWorld();
        Build(world, "construction_yard", 0, 0);

        Assert.Equal(["construction_yard", "reactor", "wall"], Available(world));
        Assert.False(CanBuild(world, "barracks"));
    }

    [Fact]
    public void A_ready_reactor_opens_the_barracks_and_the_refinery()
    {
        var world = NewWorld();
        Build(world, "construction_yard", 0, 0);
        Build(world, "reactor", 4, 0);

        Assert.Equal(["barracks", "construction_yard", "reactor", "refinery", "wall"], Available(world));
    }

    [Fact]
    public void A_growing_reactor_opens_nothing_until_it_is_ready()
    {
        var world = NewWorld();
        Build(world, "construction_yard", 0, 0);
        var reactor = Build(world, "reactor", 4, 0, ready: false);

        Assert.DoesNotContain("barracks", Available(world));

        world.CompleteBuildUp(reactor);

        Assert.Contains("barracks", Available(world));
    }

    [Fact]
    public void A_destroyed_reactor_closes_the_barracks_again()
    {
        var simulation = new GameSimulation(new SimulationSettings(1) { MapWidthCells = 40, MapHeightCells = 40 }, TestRules.Standard);
        Build(simulation.World, "construction_yard", 0, 0);
        Build(simulation.World, "reactor", 4, 0);
        simulation.Step();
        Assert.True(CanBuild(simulation.World, "barracks"));

        simulation.Enqueue(new DamageMapObjectCommand(Player1, 1, 0, new CellPos(4, 0), DamageMapObjectCommand.MaxAmount));
        simulation.Step();

        Assert.False(CanBuild(simulation.World, "barracks"));
        Assert.True(CanBuild(simulation.World, "reactor"));
    }

    [Fact]
    public void Units_need_their_factories()
    {
        var world = NewWorld();
        Build(world, "construction_yard", 0, 0);
        Build(world, "reactor", 4, 0);
        Build(world, "refinery", 7, 0);
        Assert.False(CanBuild(world, "heavy_tank"));

        Build(world, "war_factory", 11, 0);
        TechTree.Update(world);
        Assert.True(CanBuild(world, "heavy_tank"));
        Assert.True(CanBuild(world, "harvester"));
        Assert.False(CanBuild(world, "tesla_tank"));
        Assert.False(CanBuild(world, "conscript"));

        Build(world, "radar", 15, 0);
        Build(world, "tech_center", 18, 0);
        Build(world, "barracks", 22, 0);
        TechTree.Update(world);
        Assert.True(CanBuild(world, "tesla_tank"));
        Assert.True(CanBuild(world, "conscript"));
    }

    [Fact]
    public void Another_players_buildings_open_nothing()
    {
        var world = NewWorld();
        world.CreateBuilding(new PlayerId(2), TestRules.BuildingDef("construction_yard"), new CellPos(0, 0), ready: true, null);

        Assert.Equal(["construction_yard"], Available(world));
    }

    [Fact]
    public void Technologies_are_worked_out_only_when_the_buildings_change()
    {
        var world = NewWorld();
        Build(world, "construction_yard", 0, 0);
        TechTree.Update(world);
        var reactor = world.Rules.Database.IndexOfBuilding("reactor");
        Assert.True(world.Player(Player1).CanBuildBuilding(reactor));

        // Without a change the sets stay as they are, even if they no longer matched the buildings.
        world.Player(Player1).SetAvailable(new bool[world.Rules.Database.Units.Length], new bool[world.Rules.Database.Buildings.Length]);
        TechTree.Update(world);

        Assert.False(world.Player(Player1).CanBuildBuilding(reactor));
    }

    [Fact]
    public void Building_and_destroying_give_the_same_states_every_time()
    {
        static ulong Play()
        {
            var simulation = new GameSimulation(new SimulationSettings(5) { MapWidthCells = 40, MapHeightCells = 40 }, TestRules.Standard);
            simulation.World.CreateBuilding(Player1, TestRules.BuildingDef("construction_yard"), new CellPos(0, 0), true, null);
            simulation.World.CreateBuilding(Player1, TestRules.BuildingDef("reactor"), new CellPos(4, 0), false, null);
            simulation.Enqueue(new DamageMapObjectCommand(Player1, 3, 0, new CellPos(0, 0), DamageMapObjectCommand.MaxAmount));
            for (var i = 0; i < 6; i++)
            {
                simulation.Step();
            }

            return StateHasher.Compute(simulation.World);
        }

        Assert.Equal(Play(), Play());
    }
}
