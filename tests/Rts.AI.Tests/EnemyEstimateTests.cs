using Rts.AI.Modules;
using Rts.AI.Profiles;
using Rts.AI.View;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// What a bot knows of its enemies (docs/implementation/M8_ai_difficulty.md, 4.6, step 6): only what it has seen — late by its perception,
/// fading as it grows old — and everything for «Brutal», whose player sees the whole map; and how each level scouts.
/// </summary>
public sealed class EnemyEstimateTests
{
    private static readonly PlayerId Third = new(3);

    private static EnemyPicture Enemy(Scene scene) => scene.Memory.Estimate.Of(Scene.Enemy);

    [Fact]
    public void The_bot_knows_only_the_enemies_it_has_seen_by_their_kind()
    {
        var scene = new Scene();
        scene.Spawn(Scene.Me, UnitType.Conscript, 10, 10);
        scene.Spawn(Scene.Enemy, UnitType.Conscript, 12, 10);
        scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 10, 12);
        scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 50, 50);

        scene.Look();

        // The tank far away in the fog is not known.
        var enemy = Enemy(scene);
        Assert.Equal((200, 100, 100), (enemy.Units, enemy.Infantry, enemy.Vehicles));
        Assert.Equal((100, 100), (enemy.Of(UnitType.Conscript), enemy.Of(UnitType.HeavyTank)));
        Assert.True(enemy.ArmyStrength > 0);
        Assert.Equal(0, enemy.LastSeenTick);
    }

    [Fact]
    public void What_was_seen_fades_after_a_minute_is_forgotten_after_three_and_is_fresh_when_seen_again()
    {
        var scene = new Scene();
        scene.Spawn(Scene.Me, UnitType.Conscript, 10, 10);
        var tank = scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 12, 10);
        scene.Look(0);
        Scene.Move(tank, 50, 50);

        scene.Look(EnemyEstimate.FreshTicks);
        Assert.Equal(100, Enemy(scene).Units);
        scene.Look(1800);
        Assert.Equal(50, Enemy(scene).Units);
        scene.Look(EnemyEstimate.ForgetTicks);
        Assert.Equal(0, Enemy(scene).Units);
        Assert.Empty(scene.Memory.Estimate.Players);

        Scene.Move(tank, 12, 10);
        scene.Look(3000);
        Assert.Equal((100, 3000), (Enemy(scene).Units, Enemy(scene).LastSeenTick));
    }

    [Fact]
    public void An_enemy_is_known_no_earlier_than_the_perception_lets_it()
    {
        var scene = new Scene(perceptionDelay: 15);
        scene.Spawn(Scene.Me, UnitType.Conscript, 10, 10);
        scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 12, 10);

        for (var tick = 0; tick < 15; tick++)
        {
            scene.Look(tick);
            Assert.Equal(0, Enemy(scene).Units);
        }

        scene.Look(15);
        Assert.Equal(100, Enemy(scene).Units);
    }

    [Fact]
    public void The_buildings_seen_give_the_technology_the_refineries_and_the_harvesters_count_apart()
    {
        var scene = new Scene();
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 30, 30);
        scene.Build(Scene.Enemy, "construction_yard", 32, 30);
        scene.Look(0);
        var yard = Enemy(scene).TechLevel;

        scene.Build(Scene.Enemy, "barracks", 30, 33);
        scene.Build(Scene.Enemy, "refinery", 26, 30);
        scene.Spawn(Scene.Enemy, UnitType.Harvester, 28, 34);
        scene.Look(1);
        var barracks = Enemy(scene);

        scene.Build(Scene.Enemy, "tech_center", 26, 26);
        scene.Look(2);

        // The yard needs nothing; the barracks stand behind the reactor, the tech centre behind the factory and the radar.
        Assert.Equal(0, yard);
        Assert.Equal((2, 1, 3, 100, 0), (barracks.TechLevel, barracks.Refineries, barracks.Buildings, barracks.Harvesters, barracks.Units));
        Assert.Equal(4, Enemy(scene).TechLevel);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Brutal_knows_every_enemy_on_the_map_and_medium_only_those_it_sees(bool reveal)
    {
        var scene = new Scene();
        scene.World.Player(Scene.Me).Modifiers = new PlayerModifiers(0, 0, reveal);
        scene.Spawn(Scene.Me, UnitType.Conscript, 10, 10);
        scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 50, 50);
        scene.Spawn(Scene.Enemy, UnitType.Conscript, 56, 8);
        scene.Build(Scene.Enemy, "construction_yard", 40, 52);

        scene.Look();

        var enemy = Enemy(scene);
        Assert.Equal(reveal ? (200, 1) : (0, 0), (enemy.Units, enemy.Buildings));
    }

    [Fact]
    public void Two_enemies_are_known_apart_and_together()
    {
        var scene = new Scene();
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 30, 30);
        scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 32, 30);
        scene.Spawn(Third, UnitType.Conscript, 30, 32);
        scene.Spawn(Third, UnitType.Conscript, 28, 30);

        scene.Look();

        var estimate = scene.Memory.Estimate;
        Assert.Equal(new[] { Scene.Enemy, Third }, estimate.Players.Select(picture => picture.Player));
        Assert.Equal((100, 200), (estimate.Of(Scene.Enemy).Units, estimate.Of(Third).Infantry));
        Assert.Equal((300, 200, 100), (estimate.Total.Units, estimate.Total.Infantry, estimate.Total.Vehicles));
    }

    // --- Scouting by level ----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The scene of the scouting of a level: a conscript of the bot at its start (8, 8) and its yard there, the whole map explored but out
    /// of sight now, ore at (24, 24) — near the base — and at (56, 8) — far from it; the enemy unseen, so likely at (56, 56). The errand of
    /// the scout once the module has thought at tick 100.
    /// </summary>
    private static (CellPos? Errand, List<Command> Commands, AiContext Context) Scouting(ScoutingLevel level)
    {
        var (scene, scouting, context, _) = Explored(level);
        return (scouting.Errand, scene.RunCommands(), context);
    }

    /// <summary>The scene of <see cref="Scouting"/> after the turn at tick 100, and the scout.</summary>
    private static (Scene Scene, ScoutManager Scouting, AiContext Context, Unit Scout) Explored(ScoutingLevel level)
    {
        var scene = new Scene();
        var scout = scene.Spawn(Scene.Me, UnitType.Conscript, 8, 8);
        scene.Build(Scene.Me, "construction_yard", 6, 6);
        foreach (var cell in new[] { new CellPos(24, 24), new CellPos(56, 8) })
        {
            scene.World.Map.SetOre(cell, new OreCell(OreKind.Ore, OreCell.MaxStage));
        }

        // Seen once and no longer: explored, in the fog.
        var vision = scene.World.VisionOf(Scene.Me);
        for (var y = 4; y < 64; y += 8)
        {
            for (var x = 4; x < 64; x += 8)
            {
                vision.Reveal(new CellPos(x, y), 6);
                vision.Hide(new CellPos(x, y), 6);
            }
        }

        var scouting = new ScoutManager(AiSettings.Medium with { Scouting = level });
        var context = scene.Context();
        while (scene.Simulation.CurrentTick < 100)
        {
            scene.Simulation.Step();
        }

        scene.Look(100);
        context.Begin(new Rts.Simulation.Ai.AiTurn(scene.Simulation, Scene.Me) { Tick = 100 });
        scouting.Update(context);
        context.End();
        return (scene, scouting, context, scout);
    }

    [Fact]
    public void Easy_has_no_scout()
    {
        var (errand, commands, context) = Scouting(ScoutingLevel.None);

        Assert.Null(errand);
        Assert.Empty(commands);
        Assert.Null(context.Plan.Scout);
    }

    [Fact]
    public void Asked_for_a_look_medium_sends_its_scout_to_the_core_of_the_enemy_before_its_next_outing()
    {
        // M8 step 10: a squad waits to know the enemy; the scout resting at home goes where it saw the enemy's construction vehicle.
        var (scene, scouting, context, _) = Explored(ScoutingLevel.Periodic);
        scene.Spawn(Scene.Enemy, UnitType.Mcv, 40, 40);
        var vision = scene.World.VisionOf(Scene.Me);
        vision.Reveal(new CellPos(40, 40), 3);
        scene.Look(110);
        vision.Hide(new CellPos(40, 40), 3);

        scene.Look(120);
        context.Begin(new Rts.Simulation.Ai.AiTurn(scene.Simulation, Scene.Me) { Tick = 120 });
        context.Plan.LookAtTheEnemy = true;
        scouting.Update(context);
        context.End();

        Assert.Equal(new CellPos(40, 40), scouting.Errand);
    }

    [Fact]
    public void Medium_rests_at_home_once_everything_is_explored()
    {
        var (errand, _, context) = Scouting(ScoutingLevel.Periodic);

        Assert.Null(errand);
        Assert.NotNull(context.Plan.Scout);
    }

    [Fact]
    public void Hard_keeps_its_scout_out_on_the_field_seen_longest_ago_nearest_the_enemy()
    {
        var (errand, commands, _) = Scouting(ScoutingLevel.Constant);

        Assert.Equal(new CellPos(24, 24), errand);
        Assert.Equal(new CellPos(24, 24), Assert.Single(commands.OfType<MoveCommand>()).Target.ToCell());
    }

    [Fact]
    public void The_scout_of_hard_goes_on_from_place_to_place_and_never_home()
    {
        var (scene, scouting, context, scout) = Explored(ScoutingLevel.Constant);
        var errands = new List<CellPos?>();

        // More places than an outing of «Medium» has: each time the scout gets there, the next one, the field it saw longest ago.
        for (var turn = 1; turn <= ScoutManager.PlacesAnOuting + 2; turn++)
        {
            Scene.Move(scout, scouting.Errand!.Value.X, scouting.Errand.Value.Y);
            var tick = 100 + turn * 45;
            scene.Look(tick);
            context.Begin(new Rts.Simulation.Ai.AiTurn(scene.Simulation, Scene.Me) { Tick = tick });
            scouting.Update(context);
            context.End();
            errands.Add(scouting.Errand);
        }

        Assert.All(errands, errand => Assert.NotNull(errand));
        Assert.Equal(new CellPos(56, 8), errands[0]);
        Assert.Equal(new CellPos(24, 24), errands[1]);
    }

    [Fact]
    public void Brutal_looks_at_the_fields_far_from_its_base_first()
    {
        var (errand, _, _) = Scouting(ScoutingLevel.ConstantExpansions);

        Assert.Equal(new CellPos(56, 8), errand);
    }
}
