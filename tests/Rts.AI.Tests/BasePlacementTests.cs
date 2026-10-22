using Rts.AI.Modules;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Tests;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>Where a bot places a building (docs/implementation/M5_first_bot.md, 4.6, step 5), on scenes.</summary>
public sealed class BasePlacementTests
{
    private static int Index(string id) => TestRules.Standard.Database.IndexOfBuilding(id);

    private static (CellPos? Corner, int Calls) Find(Scene scene, Building yard, string id, BasePlacement? placement = null)
    {
        placement ??= new BasePlacement();
        var calls = 1;
        CellPos? corner;
        while (!placement.TryFind(scene.View, scene.Memory, yard, Index(id), out corner))
        {
            calls++;
        }

        return (corner, calls);
    }

    /// <summary>A base in the middle of the map, everything round it seen.</summary>
    private static (Scene Scene, Building Yard) Base()
    {
        var scene = new Scene();
        var yard = scene.Build(Scene.Me, "construction_yard", 30, 30);
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 20, 20);
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 44, 20);
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 20, 44);
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 44, 44);
        scene.Look();
        return (scene, yard);
    }

    [Fact]
    public void A_building_goes_near_the_yard_with_a_cell_free_round_it_off_the_ore()
    {
        var (scene, yard) = Base();
        // Ore right below the yard, where the nearest places would be.
        for (var x = 28; x <= 35; x++)
        {
            scene.World.Map.SetOre(new CellPos(x, 34), new OreCell(OreKind.Ore, 5));
        }

        scene.Look();
        var (corner, _) = Find(scene, yard, "reactor");

        var at = Assert.NotNull(corner);
        for (var y = at.Y - 1; y <= at.Y + 2; y++)
        {
            for (var x = at.X - 1; x <= at.X + 2; x++)
            {
                Assert.False(x >= 30 && x <= 32 && y >= 30 && y <= 32, $"{at} is next to the yard");
                Assert.True(y < at.Y || y > at.Y + 1 || x < at.X || x > at.X + 1 || scene.World.Map.OreAt(new CellPos(x, y)).IsEmpty);
            }
        }

        Assert.InRange(at.X, 25, 37);
        Assert.InRange(at.Y, 25, 37);
    }

    [Fact]
    public void A_refinery_goes_near_the_ore()
    {
        var (scene, yard) = Base();
        for (var x = 40; x <= 43; x++)
        {
            scene.World.Map.SetOre(new CellPos(x, 31), new OreCell(OreKind.Ore, 5));
        }

        // A unit by the ore, so that the bot knows of it.
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 42, 34);
        scene.Look();
        var (corner, _) = Find(scene, yard, "refinery");

        // East of the yard, as near the ore as the base allows (within 2 cells of it, a cell free between).
        Assert.InRange(Assert.NotNull(corner).X, 34, 35);
    }

    [Fact]
    public void A_defense_goes_on_the_side_of_the_likely_enemy()
    {
        var (scene, yard) = Base();

        // The first start where the enemy may be is the south-east corner (56, 56).
        var (corner, _) = Find(scene, yard, "tesla_coil");

        var at = Assert.NotNull(corner);
        Assert.True(at.X > 31 && at.Y > 31, $"{at}");
    }

    [Fact]
    public void A_place_that_would_shut_the_exit_of_a_war_factory_is_left_out()
    {
        var (scene, yard) = Base();
        var factory = scene.Build(Scene.Me, "war_factory", 20, 20);
        // The exit of the factory is (21, 24): rock on both sides of it, so only the cell below leads out.
        scene.World.Map.SetLand(new CellPos(20, 24), LandType.Rock);
        scene.World.Map.SetLand(new CellPos(22, 24), LandType.Rock);
        scene.Look();
        var reactor = TestRules.Standard.Database.Buildings[Index("reactor")];

        Assert.False(BasePlacement.KeepsPassages(scene.View, reactor, new CellPos(21, 25)));
        Assert.True(BasePlacement.KeepsPassages(scene.View, reactor, new CellPos(36, 36)));
        var (corner, _) = Find(scene, yard, "reactor");
        Assert.True(BasePlacement.KeepsPassages(scene.View, reactor, Assert.NotNull(corner)));
        Assert.NotEqual(new CellPos(21, 25), corner);
        Assert.Equal(factory.Owner, Scene.Me);
    }

    [Fact]
    public void A_search_spread_over_calls_ends_where_a_search_at_once_would_and_the_same_every_time()
    {
        var (scene, yard) = Base();

        var (first, calls) = Find(scene, yard, "war_factory");
        var (second, _) = Find(scene, yard, "war_factory");

        Assert.True(calls >= 3, $"{calls} calls");
        Assert.NotNull(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Nowhere_to_stand_is_an_answer_too()
    {
        var scene = new Scene();
        var yard = scene.Build(Scene.Me, "construction_yard", 30, 30);
        // Nothing round the yard has been seen: the bot knows of no place.
        var (corner, _) = Find(scene, yard, "reactor");

        Assert.Null(corner);
    }
}
