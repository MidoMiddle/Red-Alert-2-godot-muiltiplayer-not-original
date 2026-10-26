using System.Linq;
using Rts.AI.Modules;
using Rts.Data.Tests;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Rules;
using Rts.Simulation.World;
using Xunit;

namespace Rts.AI.Tests;

/// <summary>
/// What a bot keeps up before the tuning, on short scenes of the game's rules: the repairs of its buildings and vehicles
/// (<see cref="RepairManager"/>), the crates (<see cref="CrateManager"/>), the defenses against what the enemy fields and the purifier
/// of the ore (<see cref="BuildPlanner"/>). The bot is of the Union, its base in the north-west corner.
/// </summary>
public sealed class UpkeepScenarioTests
{
    private static GameRules Game => GameDataFiles.Rules;

    [Fact]
    public void A_hurt_building_is_repaired_with_money_to_spare_and_one_near_its_end_without()
    {
        var rich = new Scene(rules: Game);
        var refinery = Own(rich, Scene.Me, "refinery", 10, 2);
        refinery.Hp = refinery.MaxHp / 2;
        rich.World.Player(Scene.Me).Credits = 2000;
        rich.Look();
        new RepairManager().Update(rich.Context());
        Assert.Equal(refinery.Id, Assert.Single(rich.RunCommands().OfType<ToggleRepairCommand>()).Building);

        var poor = new Scene(rules: Game);
        var hurt = Own(poor, Scene.Me, "refinery", 10, 2);
        hurt.Hp = hurt.MaxHp * 6 / 10;
        var dying = Own(poor, Scene.Me, "barracks", 2, 9);
        dying.Hp = dying.MaxHp / 5;
        poor.World.Player(Scene.Me).Credits = 0;
        poor.Look();
        new RepairManager().Update(poor.Context());
        Assert.Equal(dying.Id, Assert.Single(poor.RunCommands().OfType<ToggleRepairCommand>()).Building);
    }

    [Fact]
    public void A_wall_is_not_repaired()
    {
        var scene = new Scene(rules: Game);
        var wall = Own(scene, Scene.Me, "wall", 10, 10);
        wall.Hp = 1;
        scene.World.Player(Scene.Me).Credits = 2000;
        scene.Look();

        new RepairManager().Update(scene.Context());

        Assert.Empty(scene.RunCommands());
    }

    [Fact]
    public void A_hurt_tank_at_home_goes_beside_the_service_depot_one_at_a_time_and_off_its_errand_when_whole()
    {
        var scene = new Scene(rules: Game);
        var depot = Own(scene, Scene.Me, "service_depot", 20, 20);
        var first = scene.Spawn(Scene.Me, Type("heavy_tank"), 10, 10);
        var second = scene.Spawn(Scene.Me, Type("heavy_tank"), 11, 10);
        first.Hp = first.MaxHp / 3;
        second.Hp = second.MaxHp / 3;
        var repairs = new RepairManager();
        scene.Look();
        var context = scene.Context();

        repairs.Update(context);

        var move = Assert.Single(scene.RunCommands().OfType<MoveCommand>());
        var unit = Assert.Single(move.Units);
        var cell = move.Target.ToCell();
        Assert.True(cell.X >= depot.Cell.X - 1 && cell.X <= depot.FarCell.X + 1 && cell.Y >= depot.Cell.Y - 1 && cell.Y <= depot.FarCell.Y + 1);
        Assert.Contains(unit.Value, context.Plan.Errands);

        // Whole again: off its errand, back to the squads.
        var sent = unit == first.Id ? first : second;
        sent.Hp = sent.MaxHp;
        scene.Look(scene.World.CurrentTick);
        context = scene.Context();
        repairs.Update(context);
        Assert.DoesNotContain(unit.Value, context.Plan.Errands);
    }

    [Fact]
    public void A_crate_in_sight_is_taken_by_the_nearest_soldier_unless_an_enemy_guards_it()
    {
        var scene = new Scene(rules: Game);
        var conscript = scene.Spawn(Scene.Me, Type("conscript"), 10, 10);
        scene.Spawn(Scene.Me, Type("conscript"), 30, 30);
        scene.World.Crates.Add(new Crate(1, new CellPos(16, 12)));
        scene.World.VisionOf(Scene.Me).Reveal(new CellPos(14, 12), 8);
        scene.Look();
        var context = scene.Context();

        new CrateManager().Update(context);

        var move = Assert.Single(scene.RunCommands().OfType<MoveCommand>());
        Assert.Equal((conscript.Id, new CellPos(16, 12)), (Assert.Single(move.Units), move.Target.ToCell()));
        Assert.Contains(conscript.Id.Value, context.Plan.Errands);

        var guarded = new Scene(rules: Game);
        guarded.Spawn(Scene.Me, Type("conscript"), 10, 10);
        guarded.Spawn(Scene.Enemy, Type("heavy_tank"), 18, 12);
        guarded.World.Crates.Add(new Crate(1, new CellPos(16, 12)));
        guarded.World.VisionOf(Scene.Me).Reveal(new CellPos(14, 12), 8);
        guarded.Look();
        new CrateManager().Update(guarded.Context());
        Assert.Empty(guarded.RunCommands());
    }

    [Fact]
    public void Against_the_enemy_s_jets_the_bot_builds_a_defense_that_fires_at_the_air()
    {
        var scene = new Scene(rules: Game);
        Base(scene);
        scene.Spawn(Scene.Enemy, Type("alliance_harrier"), 14, 14);
        scene.Spawn(Scene.Enemy, Type("alliance_harrier"), 15, 14);
        scene.World.VisionOf(Scene.Me).Reveal(new CellPos(12, 12), 12);
        scene.World.Player(Scene.Me).Credits = 3000;
        scene.Look();

        Planner().Update(scene.Context());

        var produce = Assert.Single(scene.RunCommands().OfType<ProduceCommand>());
        Assert.Equal("flak_cannon", Game.Database.Buildings[produce.DefIndex].Id);
    }

    [Fact]
    public void Against_the_side_attacked_the_bot_builds_a_defense_that_hurts_what_attacked()
    {
        var scene = new Scene(rules: Game);
        Base(scene);
        scene.Spawn(Scene.Enemy, Type("alliance_light_tank"), 20, 20);
        scene.Spawn(Scene.Enemy, Type("alliance_light_tank"), 21, 20);
        scene.World.VisionOf(Scene.Me).Reveal(new CellPos(16, 16), 12);
        scene.World.Player(Scene.Me).Credits = 3000;
        scene.Look();
        var context = scene.Context();
        context.Plan.AttackedFrom = new CellPos(20, 20);

        Planner().Update(context);

        var produce = Assert.Single(scene.RunCommands().OfType<ProduceCommand>());
        var defense = Game.Database.Buildings[produce.DefIndex];
        Assert.Equal(ProductionCategory.Defense, defense.Category);
        Assert.True(context.Units.DefenseDamagePerMinute(produce.DefIndex, Game.Database.IndexOfUnit("alliance_light_tank")) > 0, defense.Id);
    }

    [Fact]
    public void The_alliance_builds_its_purifier_of_the_ore_with_a_refinery_and_a_battle_lab()
    {
        var scene = new Scene(rules: Game);
        scene.World.Player(Scene.Me).Faction = Game.Database.Factions.Single(faction => faction.Id == "alliance");
        Own(scene, Scene.Me, "alliance_construction_yard", 6, 6);
        Own(scene, Scene.Me, "alliance_power_plant", 2, 2);
        Own(scene, Scene.Me, "alliance_power_plant", 2, 5);
        Own(scene, Scene.Me, "alliance_ore_refinery", 11, 2);
        Own(scene, Scene.Me, "alliance_barracks", 2, 9);
        Own(scene, Scene.Me, "alliance_war_factory", 2, 13);
        // RA2: the purifier needs the battle lab.
        Own(scene, Scene.Me, "alliance_battle_lab", 18, 6);
        scene.World.Player(Scene.Me).Credits = 3000;
        scene.Look();

        new BuildPlanner(Scene.Settings with { BuildOrder = "alliance_balanced_simple", MaxRefineries = 1, SecondFactoryCredits = int.MaxValue })
            .Update(scene.Context());

        var produce = Assert.Single(scene.RunCommands().OfType<ProduceCommand>());
        Assert.Equal("alliance_ore_purifier", Game.Database.Buildings[produce.DefIndex].Id);
    }

    private static BuildPlanner Planner() =>
        new(Scene.Settings with { BuildOrder = "balanced_simple", MaxRefineries = 1, SecondFactoryCredits = int.MaxValue });

    /// <summary>The base of the bot: the yard, reactors, a refinery, barracks and a war factory — the simple order done.</summary>
    private static void Base(Scene scene)
    {
        Own(scene, Scene.Me, "construction_yard", 6, 6);
        Own(scene, Scene.Me, "reactor", 2, 2);
        Own(scene, Scene.Me, "reactor", 2, 5);
        Own(scene, Scene.Me, "refinery", 11, 2);
        Own(scene, Scene.Me, "barracks", 2, 9);
        Own(scene, Scene.Me, "war_factory", 2, 12);
    }

    private static Building Own(Scene scene, PlayerId owner, string id, int x, int y) =>
        scene.World.CreateBuilding(owner, Game.Database.Buildings[Game.Database.IndexOfBuilding(id)], new CellPos(x, y), ready: true, null);

    private static UnitType Type(string id) => UnitTypes.TryGetById(id, out var type) ? type : throw new System.ArgumentException(id);
}
