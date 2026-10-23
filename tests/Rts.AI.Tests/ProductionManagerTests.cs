using Rts.AI.Modules;
using Rts.Simulation.Commands;
using Rts.Simulation.Tests;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>What the army production of a bot aims at and orders (docs/implementation/M5_first_bot.md, 4.6, step 6), on scenes.</summary>
public sealed class ProductionManagerTests
{
    private static int Unit(string id) => TestRules.Standard.Database.IndexOfUnit(id);

    /// <summary>A base with barracks and a war factory, no tech center: everything but the tesla tank can be built.</summary>
    private static Scene Base()
    {
        var scene = new Scene();
        scene.Build(Scene.Me, "construction_yard", 30, 30);
        scene.Build(Scene.Me, "reactor", 6, 6);
        scene.Build(Scene.Me, "reactor", 12, 6);
        scene.Build(Scene.Me, "refinery", 18, 6);
        scene.Build(Scene.Me, "barracks", 24, 6);
        scene.Build(Scene.Me, "war_factory", 30, 6);
        return scene;
    }

    private static Dictionary<string, long> Target(Scene scene, ProductionManager production)
    {
        scene.Look();
        production.Target(scene.Context());
        return production.Shares.ToDictionary(share => TestRules.Standard.Database.Units[share.Unit].Id, share => share.Share);
    }

    [Fact]
    public void A_kind_that_cannot_be_built_yet_is_left_out_and_the_rest_keep_their_shares()
    {
        var target = Target(Base(), new ProductionManager(AiSettings.Medium));

        Assert.Equal(
            new Dictionary<string, long> { ["conscript"] = 2500, ["heavy_tank"] = 4000, ["rocketeer"] = 2000, ["support_vehicle"] = 1000 },
            target);
    }

    [Fact]
    public void Against_an_army_of_infantry_the_tanks_gain_and_against_vehicles_the_rocketeers()
    {
        var scene = Base();
        // A scout near enemy infantry: the memory sees four conscripts.
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 50, 40);
        for (var i = 0; i < 4; i++)
        {
            scene.Spawn(Scene.Enemy, UnitType.Conscript, 52 + i, 42);
        }

        var infantry = Target(scene, new ProductionManager(AiSettings.Medium));
        // 20 points to the heavy tank and the flak track (4000 : 1000), taken from conscript and rocketeer (2500 : 2000).
        Assert.Equal(4000 + 2000 * 4000 / 5000, infantry["heavy_tank"]);
        Assert.Equal(1000 + 2000 * 1000 / 5000, infantry["support_vehicle"]);
        Assert.Equal(2500 - 2000 * 2500 / 4500, infantry["conscript"]);

        var vehicles = Base();
        vehicles.Spawn(Scene.Me, UnitType.HeavyTank, 50, 40);
        for (var i = 0; i < 4; i++)
        {
            vehicles.Spawn(Scene.Enemy, UnitType.HeavyTank, 52 + i, 42);
        }

        // The tesla tank is not open: all 20 points go to the rocketeer.
        Assert.Equal(2000 + 2000, Target(vehicles, new ProductionManager(AiSettings.Medium))["rocketeer"]);
    }

    [Fact]
    public void The_kind_shortest_of_its_share_is_ordered_one_a_factory()
    {
        var scene = Base();
        // Two heavy tanks already: the conscript, then the other vehicles fall shorter.
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 40, 40);
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 42, 40);
        scene.Look();
        new ProductionManager(AiSettings.Medium).Update(scene.Context());

        var ordered = scene.RunCommands().OfType<ProduceCommand>().Select(command => command.DefIndex).ToList();

        // Army 2: conscript 25% × 3 = 0.75 short; rocketeer 0.6; heavy tank 1.2 − 2; flak track 0.3. One a factory: the conscript and
        // the flak track.
        Assert.Equal([Unit("conscript"), Unit("support_vehicle")], ordered.Order().ToList());
    }

    [Fact]
    public void A_full_queue_or_money_kept_for_the_next_building_orders_nothing()
    {
        var scene = Base();
        var radar = TestRules.Standard.Database.IndexOfBuilding("radar");
        // Enough for the radar and a little more, but not for the radar and the cheapest unit.
        var cheapest = TestRules.Standard.Database.Units.Min(unit => unit.Cost);
        scene.World.Player(Scene.Me).Credits = TestRules.Standard.Database.Buildings[radar].Cost + cheapest - 1;
        scene.Look();
        var context = scene.Context();
        context.Plan.NextBuilding = radar;
        new ProductionManager(AiSettings.Medium).Update(context);
        context.End();

        Assert.Empty(scene.RunCommands().OfType<ProduceCommand>());
    }

    [Fact]
    public void While_the_base_is_defended_the_army_does_not_save_for_the_next_building()
    {
        var scene = Base();
        var radar = TestRules.Standard.Database.IndexOfBuilding("radar");
        var cheapest = TestRules.Standard.Database.Units.Min(unit => unit.Cost);
        scene.World.Player(Scene.Me).Credits = TestRules.Standard.Database.Buildings[radar].Cost + cheapest - 1;
        scene.Look();
        var context = scene.Context();
        context.Plan.NextBuilding = radar;
        context.Plan.Defend();
        new ProductionManager(AiSettings.Medium).Update(context);
        context.End();

        Assert.NotEmpty(scene.RunCommands().OfType<ProduceCommand>());
    }

    [Fact]
    public void The_strategist_can_hold_the_army_back()
    {
        var scene = Base();
        scene.Look();
        var context = scene.Context();
        context.Plan.ArmyAllowed = false;
        new ProductionManager(AiSettings.Medium).Update(context);
        context.End();

        Assert.Empty(scene.RunCommands().OfType<ProduceCommand>());
    }
}
