using Rts.AI.Modules;
using Rts.AI.Profiles;
using Rts.AI.Squads;
using Rts.Data.Tests;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// The bot and the buildings of the factions a map places for nobody (2026-10-27, the maps of RA2): it knows them from the map, keeps its
/// harvesters, its base and its MCV out of the reach of the defenses — which fire at every player — fights them with its squads, and sends
/// its engineers into what the data lets them take. On scenes of the game's rules; the bot is of the Union, its base in the north-west.
/// </summary>
public sealed class NobodysDefenseScenarioTests
{
    private static GameRules Game => GameDataFiles.Rules;

    private static Scene Clear() => new(rules: Game);

    private static Building Own(Scene scene, PlayerId owner, string id, int x, int y) =>
        scene.World.CreateBuilding(owner, Game.Database.Buildings[Game.Database.IndexOfBuilding(id)], new CellPos(x, y), ready: true, null);

    private static Building Nobodys(Scene scene, string id, int x, int y) => Own(scene, PlayerId.Neutral, id, x, y);

    private static List<Command> Turn(Scene scene, IAiModule module, int tick = 0)
    {
        scene.Look(tick);
        module.Update(scene.Context());
        return scene.RunCommands();
    }

    [Fact]
    public void The_bot_knows_the_defenses_of_nobody_from_the_map_and_forgets_one_seen_destroyed()
    {
        var scene = Clear();
        var tower = Nobodys(scene, "alliance_prism_tower", 40, 40);
        var reactor = Nobodys(scene, "nuclear_reactor", 50, 10);

        scene.Look();

        var known = scene.Memory.NobodysBuildings.ToDictionary(building => building.Id);
        Assert.Equal((8, true), (known[tower.Id].Reach, known[tower.Id].HitsGround));
        Assert.False(known[reactor.Id].Fires);
        Assert.Equal(tower.Id, scene.Memory.NobodysDefenseReaching(new CellPos(46, 44))!.Id);
        Assert.Null(scene.Memory.NobodysDefenseReaching(new CellPos(46, 47)));
        // Not an enemy: no squad is sent at it, no base thought to be there; but a foe of the squads in sight.
        Assert.Empty(scene.View.VisibleEnemies);

        scene.World.VisionOf(Scene.Me).Reveal(new CellPos(40, 40), 6);
        scene.Look(1);
        Assert.Equal([tower.Id], scene.View.Foes.Select(foe => foe.Id));

        scene.World.DestroyBuilding(tower, null);
        scene.Look(2);
        Assert.DoesNotContain(scene.Memory.NobodysBuildings, building => building.Id == tower.Id);
        Assert.Contains(scene.Memory.NobodysBuildings, building => building.Id == reactor.Id);
    }

    [Fact]
    public void A_harvester_working_under_a_lens_tower_of_nobody_is_sent_to_a_safe_field()
    {
        var scene = Clear();
        Own(scene, Scene.Me, "construction_yard", 6, 6);
        Own(scene, Scene.Me, "reactor", 12, 2);
        Own(scene, Scene.Me, "refinery", 6, 12);
        scene.World.Map.SetOre(new CellPos(30, 12), new OreCell(OreKind.Ore, 5));
        scene.World.Map.SetOre(new CellPos(8, 24), new OreCell(OreKind.Ore, 5));
        // Out of the harvester's sight: the map shows it.
        Nobodys(scene, "alliance_prism_tower", 37, 12);
        var harvester = scene.Spawn(Scene.Me, UnitType.Harvester, 28, 12);
        harvester.Harvester!.Field = new CellPos(30, 12);
        scene.World.VisionOf(Scene.Me).Reveal(new CellPos(8, 24), 3);

        var order = Assert.Single(Turn(scene, new EconomyManager(AiSettings.Medium)).OfType<HarvestCommand>());

        Assert.Equal((new CellPos(8, 24), harvester.Id), (order.Cell, order.Units.Single()));
    }

    [Fact]
    public void A_harvester_far_from_the_tower_works_on()
    {
        var scene = Clear();
        Own(scene, Scene.Me, "construction_yard", 6, 6);
        Own(scene, Scene.Me, "reactor", 12, 2);
        Own(scene, Scene.Me, "refinery", 6, 12);
        scene.World.Map.SetOre(new CellPos(30, 12), new OreCell(OreKind.Ore, 5));
        scene.World.Map.SetOre(new CellPos(8, 24), new OreCell(OreKind.Ore, 5));
        Nobodys(scene, "alliance_prism_tower", 41, 12);
        var harvester = scene.Spawn(Scene.Me, UnitType.Harvester, 28, 12);
        harvester.Harvester!.Field = new CellPos(30, 12);
        scene.World.VisionOf(Scene.Me).Reveal(new CellPos(8, 24), 3);

        Assert.Empty(Turn(scene, new EconomyManager(AiSettings.Medium)).OfType<HarvestCommand>());
    }

    [Fact]
    public void The_mcv_drives_out_of_the_reach_of_a_grand_cannon_of_nobody_before_it_deploys()
    {
        // As at the inner starts of WAR Pro: the cannon of the middle reaches the start.
        var scene = Clear();
        var faction = scene.World.Player(Scene.Me).Faction;
        var mcv = scene.Spawn(Scene.Me, UnitTypes.TryGetById(faction.Mcv, out var type) ? type : UnitType.Mcv, 20, 20);
        Nobodys(scene, "alliance_grand_cannon", 32, 20);
        scene.World.VisionOf(Scene.Me).Reveal(new CellPos(20, 20), 20);
        var planner = new BuildPlanner(AiSettings.Medium);

        var commands = Turn(scene, planner);

        Assert.Empty(commands.OfType<DeployCommand>());
        var move = Assert.Single(commands.OfType<MoveCommand>());
        var yard = Game.Database.Buildings[Game.Database.IndexOfBuilding(faction.ConstructionYard)];
        var corner = new CellPos(move.Target.ToCell().X - (yard.Footprint.Width - 1) / 2, move.Target.ToCell().Y - (yard.Footprint.Height - 1) / 2);
        var far = new CellPos(corner.X + yard.Footprint.Width - 1, corner.Y + yard.Footprint.Height - 1);
        Assert.Null(scene.Memory.NobodysDefenseReaching(corner, far, 0));

        // There it deploys.
        Scene.Move(mcv, move.Target.ToCell().X, move.Target.ToCell().Y);
        mcv.MoveTarget = null;
        Assert.Single(Turn(scene, planner, scene.Simulation.CurrentTick).OfType<DeployCommand>());
    }

    [Fact]
    public void A_squad_that_comes_upon_a_tower_of_nobody_fires_at_it()
    {
        var scene = Clear();
        var a = scene.Spawn(Scene.Me, UnitType.HeavyTank, 14, 14);
        var b = scene.Spawn(Scene.Me, UnitType.HeavyTank, 15, 14);
        var squads = new SquadManager(AiSettings.Medium with
        {
            MinAttackArmy = 2, Micro = MicroLevel.Focus, RetreatThresholdPercent = 0, FirstAttackFrom = 0, WaveInterval = 0, AssumedEnemyPercent = 0,
        });
        var context = scene.Context();
        scene.Look(0);
        context.Begin(new Rts.Simulation.Ai.AiTurn(scene.Simulation, Scene.Me) { Tick = 0 });
        squads.Update(context);
        context.End();
        Assert.Equal(SquadState.Moving, context.Plan.Squads.Single().State);

        while (scene.Simulation.CurrentTick < 15)
        {
            scene.Simulation.Step();
        }

        // Before the tanks pick it themselves.
        var tower = Nobodys(scene, "alliance_prism_tower", 28, 24);
        Scene.Move(a, 24, 24);
        Scene.Move(b, 24, 25);
        scene.World.VisionOf(Scene.Me).Reveal(new CellPos(24, 24), 8);

        scene.Look(15);
        var before = scene.Simulation.ExecutedCommands.Count;
        context.Begin(new Rts.Simulation.Ai.AiTurn(scene.Simulation, Scene.Me) { Tick = 15 });
        squads.Update(context);
        context.End();
        scene.Simulation.Step();
        scene.Simulation.Step();

        Assert.Equal(SquadState.Fighting, context.Plan.Squads.Single().State);
        var attack = Assert.Single(scene.Simulation.ExecutedCommands.Skip(before).OfType<AttackCommand>());
        Assert.Equal(tower.Id, attack.Target);
    }

    [Fact]
    public void An_engineer_is_sent_into_a_reactor_of_nobody_by_the_base_but_not_one_a_tower_of_nobody_covers()
    {
        var scene = Clear();
        Own(scene, Scene.Me, "construction_yard", 6, 6);
        Own(scene, Scene.Me, "reactor", 2, 2);
        Own(scene, Scene.Me, "refinery", 10, 2);
        Own(scene, Scene.Me, "barracks", 2, 9);
        var reactor = Nobodys(scene, "nuclear_reactor", 20, 8);
        var engineer = scene.Spawn(Scene.Me, UnitType.Engineer, 10, 12);
        scene.World.VisionOf(Scene.Me).Reveal(new CellPos(12, 10), 20);

        var enter = Assert.Single(Turn(scene, new EngineerManager(Scene.Settings)).OfType<EnterCommand>());
        Assert.Equal((engineer.Id, reactor.Id), (Assert.Single(enter.Units), enter.Building));

        var guarded = Clear();
        Own(guarded, Scene.Me, "construction_yard", 6, 6);
        Own(guarded, Scene.Me, "reactor", 2, 2);
        Own(guarded, Scene.Me, "refinery", 10, 2);
        Own(guarded, Scene.Me, "barracks", 2, 9);
        Nobodys(guarded, "nuclear_reactor", 20, 8);
        Nobodys(guarded, "alliance_prism_tower", 30, 10);
        guarded.Spawn(Scene.Me, UnitType.Engineer, 10, 12);
        guarded.World.VisionOf(Scene.Me).Reveal(new CellPos(12, 10), 20);

        Assert.Empty(Turn(guarded, new EngineerManager(Scene.Settings)).OfType<EnterCommand>());
    }

    [Fact]
    public void No_engineer_goes_for_a_defense_or_a_superweapon_of_nobody()
    {
        var scene = Clear();
        Own(scene, Scene.Me, "construction_yard", 6, 6);
        Own(scene, Scene.Me, "reactor", 2, 2);
        Own(scene, Scene.Me, "barracks", 2, 9);
        Nobodys(scene, "iron_curtain", 20, 8);
        Nobodys(scene, "alliance_patriot_missile_system", 20, 16);
        scene.Spawn(Scene.Me, UnitType.Engineer, 10, 12);
        scene.World.VisionOf(Scene.Me).Reveal(new CellPos(12, 10), 20);

        Assert.Empty(Turn(scene, new EngineerManager(Scene.Settings)).OfType<EnterCommand>());
    }
}
