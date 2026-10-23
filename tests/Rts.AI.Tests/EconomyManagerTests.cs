using Rts.AI.Modules;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Tests;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>The decisions of the economy of a bot (docs/implementation/M5_first_bot.md, 4.6, step 4), on scenes.</summary>
public sealed class EconomyManagerTests
{
    private static int Index(string id) => TestRules.Standard.Database.IndexOfBuilding(id);

    private static IReadOnlyList<int> Wants(Scene scene)
    {
        scene.Look();
        var context = scene.Context();
        new EconomyManager(AiSettings.Medium).Update(context);
        context.End();
        return context.Plan.Wants;
    }

    [Fact]
    public void With_nothing_but_the_yard_a_reactor_comes_before_the_refinery()
    {
        var scene = new Scene();
        scene.Build(Scene.Me, "construction_yard", 6, 6);

        Assert.Equal([Index("reactor"), Index("refinery")], Wants(scene));
    }

    [Fact]
    public void A_reserve_of_power_below_a_fifth_of_the_output_wants_a_reactor()
    {
        var scene = new Scene();
        scene.Build(Scene.Me, "construction_yard", 6, 6);
        scene.Build(Scene.Me, "reactor", 12, 6);
        scene.Build(Scene.Me, "refinery", 6, 12);
        scene.Build(Scene.Me, "war_factory", 12, 12);
        scene.Spawn(Scene.Me, UnitType.Harvester, 20, 20);
        scene.Spawn(Scene.Me, UnitType.Harvester, 22, 20);

        // 150 out, 75 in: half is left, no reactor.
        Assert.Empty(Wants(scene));

        // A radar more: 150 out, 125 in — 25 left is below 30.
        scene.Build(Scene.Me, "radar", 18, 6);
        Assert.Equal([Index("reactor")], Wants(scene));
    }

    [Fact]
    public void Harvesters_missing_and_no_war_factory_want_one()
    {
        var scene = new Scene();
        scene.Build(Scene.Me, "construction_yard", 6, 6);
        scene.Build(Scene.Me, "reactor", 12, 6);
        scene.Build(Scene.Me, "refinery", 6, 12);
        scene.Spawn(Scene.Me, UnitType.Harvester, 20, 20);

        Assert.Equal([Index("war_factory")], Wants(scene));
    }

    [Fact]
    public void A_harvester_working_near_a_known_army_is_sent_to_a_safe_field_near_the_refinery()
    {
        var scene = new Scene();
        scene.Build(Scene.Me, "construction_yard", 6, 6);
        scene.Build(Scene.Me, "reactor", 12, 6);
        scene.Build(Scene.Me, "refinery", 6, 12);
        scene.World.Map.SetOre(new CellPos(30, 12), new OreCell(OreKind.Ore, 5));
        scene.World.Map.SetOre(new CellPos(8, 24), new OreCell(OreKind.Ore, 5));
        var harvester = scene.Spawn(Scene.Me, UnitType.Harvester, 28, 12);
        harvester.Harvester!.Field = new CellPos(30, 12);
        // In the harvester's sight, so the bot knows of it.
        scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 31, 12);
        scene.Spawn(Scene.Me, UnitType.Engineer, 8, 22);

        scene.Look();
        var context = scene.Context();
        new EconomyManager(AiSettings.Medium).Update(context);
        context.End();
        scene.Simulation.Step();
        scene.Simulation.Step();

        var order = Assert.Single(scene.Simulation.ExecutedCommands.OfType<HarvestCommand>());
        Assert.Equal((new CellPos(8, 24), harvester.Id), (order.Cell, order.Units.Single()));
    }

    /// <summary>One turn of the economy with <paramref name="settings"/>: its plan and the commands it gave, once they ran.</summary>
    private static (AiPlan Plan, List<Command> Commands) Economy(Scene scene, AiSettings settings, EconomyManager? economy = null)
    {
        scene.Look(scene.Simulation.CurrentTick);
        var context = scene.Context();
        var before = scene.Simulation.ExecutedCommands.Count;
        (economy ?? new EconomyManager(settings)).Update(context);
        context.End();
        scene.Simulation.Step();
        scene.Simulation.Step();
        return (context.Plan, scene.Simulation.ExecutedCommands.Skip(before).ToList());
    }

    [Fact]
    public void Ore_on_the_side_of_the_enemy_does_not_keep_the_economy_going()
    {
        // M8 step 10: the ore by the enemy base, found by a scout, is no income the bot can count on. (38, 12) is nearer the start (56, 8)
        // where the enemy may be than the refinery at (6, 12).
        var scene = new Scene();
        scene.Build(Scene.Me, "construction_yard", 6, 6);
        scene.World.Map.SetOre(new CellPos(9, 9), new OreCell(OreKind.Ore, 5));
        var economy = new EconomyManager(AiSettings.Medium);
        Assert.False(Economy(scene, AiSettings.Medium, economy).Plan.OreGone);

        scene.Build(Scene.Me, "refinery", 6, 12);
        scene.World.Map.SetOre(new CellPos(9, 9), OreCell.Empty);
        scene.World.Map.SetOre(new CellPos(38, 12), new OreCell(OreKind.Ore, 11));
        scene.Spawn(Scene.Me, UnitType.Engineer, 36, 12);

        Assert.True(Economy(scene, AiSettings.Medium, economy).Plan.OreGone);
    }

    [Theory]
    // One full cell of ore, 300 credits — a harvester of the rules of the tests costs 100, four of them are 400: by the ore the one there is
    // is enough, by the count «Medium» wants two a refinery.
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void By_the_ore_a_bot_buys_no_harvester_the_ore_within_reach_does_not_pay_for(bool byOre, bool buys)
    {
        var scene = new Scene();
        scene.Build(Scene.Me, "construction_yard", 6, 6);
        scene.Build(Scene.Me, "reactor", 12, 6);
        scene.Build(Scene.Me, "refinery", 6, 12);
        scene.Build(Scene.Me, "war_factory", 12, 12);
        scene.Build(Scene.Me, "reactor", 18, 6);
        scene.Spawn(Scene.Me, UnitType.Harvester, 10, 18);
        scene.World.Map.SetOre(new CellPos(8, 20), new OreCell(OreKind.Ore, OreCell.MaxStage));

        var (_, commands) = Economy(scene, AiSettings.Medium with { HarvestersByOre = byOre });

        Assert.Equal(buys, commands.OfType<ProduceCommand>().Any(command => command.Kind == ProductionKind.Unit));
    }
}
