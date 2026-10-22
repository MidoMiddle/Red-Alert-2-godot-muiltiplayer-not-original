using Rts.AI.Modules;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Tests;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>What the build planner of a bot orders (docs/implementation/M5_first_bot.md, 4.6–4.7, step 5), on scenes.</summary>
public sealed class BuildPlannerTests
{
    private static int Index(string id) => TestRules.Standard.Database.IndexOfBuilding(id);

    /// <summary>The buildings the bot orders on one turn of its economy and its planner.</summary>
    private static List<int> Ordered(Scene scene)
    {
        scene.Look();
        var context = scene.Context();
        new EconomyManager(AiSettings.Medium).Update(context);
        new BuildPlanner(AiSettings.Medium).Update(context);
        return scene.RunCommands().OfType<ProduceCommand>().Select(command => command.DefIndex).ToList();
    }

    private static Scene Base(params string[] buildings)
    {
        var scene = new Scene();
        scene.Build(Scene.Me, "construction_yard", 30, 30);
        var x = 6;
        foreach (var id in buildings)
        {
            scene.Build(Scene.Me, id, x, 6);
            x += 6;
        }

        return scene;
    }

    /// <summary>
    /// The base of the whole build order — its tesla coil on the north side, far from the east — with reactors enough and four
    /// harvesters: after the order only the defense toward an attack may come.
    /// </summary>
    private static Scene Done()
    {
        var scene = Base("reactor", "refinery", "barracks", "refinery", "war_factory", "reactor", "radar", "tesla_coil");
        foreach (var (id, x) in new[] { ("tech_center", 6), ("reactor", 12), ("reactor", 18) })
        {
            scene.Build(Scene.Me, id, x, 14);
        }

        for (var i = 0; i < 4; i++)
        {
            scene.Spawn(Scene.Me, UnitType.Harvester, 40 + 2 * i, 50);
        }

        scene.World.Player(Scene.Me).Credits = 3000;
        return scene;
    }

    private static List<int> OrderedAfterAttack(Scene scene, CellPos from)
    {
        scene.Look();
        var context = scene.Context();
        context.Plan.AttackedFrom = from;
        new EconomyManager(AiSettings.Medium).Update(context);
        new BuildPlanner(AiSettings.Medium).Update(context);
        return scene.RunCommands().OfType<ProduceCommand>().Select(command => command.DefIndex).ToList();
    }

    [Fact]
    public void After_an_attack_from_a_side_no_defense_covers_another_defense_is_ordered_facing_it()
    {
        // Without an attack the order is done and nothing comes.
        Assert.Empty(Ordered(Done()));
        Assert.Equal([Index("tesla_coil")], OrderedAfterAttack(Done(), new CellPos(50, 31)));

        // It goes to the east of the yard (30, 30), toward the attack.
        var scene = Done();
        scene.Look();
        var yard = scene.World.Entities.OfType<Building>().First(building => building.Def?.Id == "construction_yard");
        var placement = new BasePlacement { Toward = new CellPos(50, 31) };
        CellPos? corner;
        while (!placement.TryFind(scene.View, scene.Memory, yard, Index("tesla_coil"), out corner))
        {
        }

        Assert.NotNull(corner);
        Assert.True(corner.Value.X > 32, $"{corner}");
    }

    [Fact]
    public void A_side_a_defense_covers_already_gets_none()
    {
        var scene = Done();
        scene.Build(Scene.Me, "tesla_coil", 37, 30);

        Assert.Empty(OrderedAfterAttack(scene, new CellPos(50, 31)));
    }

    [Fact]
    public void The_next_step_of_the_order_is_ordered()
    {
        // Reactor, refinery done: the barracks come next. The refinery's harvester is there.
        var scene = Base("reactor", "refinery");
        scene.Spawn(Scene.Me, UnitType.Harvester, 40, 40);
        scene.Spawn(Scene.Me, UnitType.Harvester, 42, 40);

        Assert.Equal([Index("barracks")], Ordered(scene));
    }

    [Fact]
    public void A_step_whose_prerequisite_still_grows_is_waited_for()
    {
        // The reactor is growing: the refinery cannot be built yet, and nothing after it goes first.
        var scene = new Scene();
        scene.Build(Scene.Me, "construction_yard", 30, 30);
        scene.World.CreateBuilding(Scene.Me, TestRules.BuildingDef("reactor"), new Rts.Simulation.Geometry.CellPos(6, 6), ready: false, null);

        Assert.Empty(Ordered(scene));
    }

    [Fact]
    public void A_reactor_needed_at_once_comes_before_the_step_of_the_order()
    {
        // Two radars drain more than the reactor gives: a reactor first, then the barracks of the order.
        var scene = Base("reactor", "refinery", "radar", "radar");
        scene.Spawn(Scene.Me, UnitType.Harvester, 40, 40);
        scene.Spawn(Scene.Me, UnitType.Harvester, 42, 40);

        Assert.Equal([Index("reactor")], Ordered(scene));
    }

    [Fact]
    public void Without_the_money_left_after_what_is_promised_nothing_is_ordered()
    {
        var scene = Base("reactor", "refinery");
        scene.Spawn(Scene.Me, UnitType.Harvester, 40, 40);
        scene.Spawn(Scene.Me, UnitType.Harvester, 42, 40);
        scene.World.Player(Scene.Me).Credits = TestRules.BuildingDef("barracks").Cost - 1;

        Assert.Empty(Ordered(scene));
    }

    [Fact]
    public void A_lost_building_of_the_order_is_built_again()
    {
        var scene = Base("reactor", "refinery", "barracks", "refinery");
        scene.Spawn(Scene.Me, UnitType.Harvester, 40, 40);
        scene.Spawn(Scene.Me, UnitType.Harvester, 42, 40);
        var barracks = scene.World.Entities.OfType<Building>().Single(b => b.Def?.Id == "barracks");
        scene.World.DestroyBuilding(barracks, null);
        Rts.Simulation.Systems.DeathSystem.Update(scene.World, null);
        scene.World.RemoveDead();

        Assert.Equal([Index("barracks")], Ordered(scene));
    }
}
