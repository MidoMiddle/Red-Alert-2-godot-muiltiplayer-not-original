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

    /// <summary>A base and, in sight of a tank of the bot at (50, 40), the enemy units of <paramref name="enemies"/> in a row.</summary>
    private static Scene Facing(params UnitType[] enemies)
    {
        var scene = Base();
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 50, 40);
        for (var i = 0; i < enemies.Length; i++)
        {
            scene.Spawn(Scene.Enemy, enemies[i], 52 + i, 42);
        }

        return scene;
    }

    private static readonly UnitType[] FourConscripts = [UnitType.Conscript, UnitType.Conscript, UnitType.Conscript, UnitType.Conscript];

    [Theory]
    // The heavy tank and the flak track answer infantry (4000 and 1000 of the army). Against all infantry the army picked against it is
    // theirs alone, 7600 and 1900 of the whole 9500; the level picks that share of the army: none, 30 %, 70 %.
    [InlineData(0, 4000, 1000, 2500, 2000)]
    [InlineData(30, 5080, 1270, 1750, 1400)]
    [InlineData(70, 6520, 1630, 750, 600)]
    public void Against_masses_of_infantry_the_higher_level_picks_more_of_what_beats_it(int pick, int tank, int flak, int conscript, int rocketeer)
    {
        var target = Target(Facing(FourConscripts), new ProductionManager(AiSettings.Medium with { CounterPickPercent = pick }));

        Assert.Equal((tank, flak, conscript, rocketeer), (target["heavy_tank"], target["support_vehicle"], target["conscript"], target["rocketeer"]));
        Assert.Equal(9500, target.Values.Sum());
    }

    [Fact]
    public void Against_vehicles_the_rocketeers_gain_and_what_cannot_be_built_leaves_its_part_to_them()
    {
        // The tesla tank answers vehicles too, but there is no tech centre: the rocketeer takes the whole part.
        var target = Target(Facing(UnitType.HeavyTank, UnitType.HeavyTank), new ProductionManager(AiSettings.Medium with { CounterPickPercent = 70 }));

        Assert.Equal(2000 * 30 / 100 + 9500 * 70 / 100, target["rocketeer"]);
        Assert.Equal(4000 * 30 / 100, target["heavy_tank"]);
    }

    [Fact]
    public void Against_a_mixed_enemy_both_counters_gain_by_its_mix()
    {
        // Half infantry, half vehicles: half the whole for the heavy tank and the flak track, half for the rocketeer.
        var target = Target(
            Facing(UnitType.Conscript, UnitType.Conscript, UnitType.HeavyTank, UnitType.HeavyTank),
            new ProductionManager(AiSettings.Medium with { CounterPickPercent = 100 }));

        Assert.Equal((3800, 950, 4750, 0), (target["heavy_tank"], target["support_vehicle"], target["rocketeer"], target["conscript"]));
    }

    [Fact]
    public void An_enemy_forgotten_leaves_the_army_as_it_is()
    {
        var scene = Facing(FourConscripts);
        var production = new ProductionManager(AiSettings.Medium with { CounterPickPercent = 70 });
        Target(scene, production);
        foreach (var unit in scene.World.Entities.OfType<Unit>().Where(unit => unit.Owner == Scene.Enemy))
        {
            Scene.Move(unit, 60, 60);
        }

        scene.Look(Rts.AI.View.EnemyEstimate.ForgetTicks);
        production.Target(scene.Context());

        Assert.Equal(4000, production.Shares.Single(share => share.Unit == Unit("heavy_tank")).Share);
    }

    [Theory]
    // The army is the tank of the bot that sees the conscripts. «Easy» picks nothing against them: the conscript falls shortest of its
    // 25 %. «Hard» picks 70 %: the flak track and the heavy tank grow, the conscript shrinks, and the flak track is ordered first.
    [InlineData(0, "conscript")]
    [InlineData(70, "support_vehicle")]
    public void Against_infantry_the_first_order_follows_the_pick_of_the_level(int pick, string first)
    {
        var scene = Facing(FourConscripts);
        scene.Look();
        var context = scene.Context();
        new ProductionManager(AiSettings.Medium with { CounterPickPercent = pick }).Update(context);
        context.End();

        Assert.Equal(Unit(first), scene.RunCommands().OfType<ProduceCommand>().First().DefIndex);
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
