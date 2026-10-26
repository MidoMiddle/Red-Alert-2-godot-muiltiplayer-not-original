using System.Linq;
using Rts.AI.Modules;
using Rts.AI.Squads;
using Rts.Data.Tests;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Rules;
using Rts.Simulation.World;
using Xunit;

namespace Rts.AI.Tests;

/// <summary>
/// The spy and the ferry of a bot (M11 step 3, the bot that knows every weapon), on short scenes of the game's rules: which enemy building
/// the spy goes into (<see cref="SpyManager"/>), and the transports that carry a squad over the water to the enemy's island and put it
/// ashore (<see cref="FerryManager"/>). The bot is of the Union, its start the north-west corner (8, 8).
/// </summary>
public sealed class SpyAndFerryScenarioTests
{
    private static GameRules Game => GameDataFiles.Rules;

    /// <summary>The settings of a squad that sets out with two units at once, as in the timing tests.</summary>
    private static readonly AiSettings Out = Scene.Settings with { MinAttackArmy = 2, FirstAttackFrom = 0, WaveInterval = 0, AssumedEnemyPercent = 0 };

    [Fact]
    public void The_spy_goes_into_the_battle_lab_first_and_then_into_the_war_factory()
    {
        var scene = new Scene(rules: Game);
        Own(scene, Scene.Enemy, "refinery", 40, 40);
        Own(scene, Scene.Enemy, "war_factory", 44, 46);
        var lab = Own(scene, Scene.Enemy, "tech_center", 50, 40);
        var spy = scene.Spawn(Scene.Me, Type("alliance_spy"), 30, 30);
        scene.World.VisionOf(Scene.Me).Reveal(new CellPos(46, 44), 14);
        scene.Look();

        new SpyManager(Scene.Settings).Update(scene.Context());

        var enter = Assert.Single(scene.RunCommands().OfType<EnterCommand>());
        Assert.Equal((spy.Id, lab.Id), (Assert.Single(enter.Units), enter.Building));

        // With the Union's technology stolen, the veterans of its war factory come next.
        var stolen = new Scene(rules: Game);
        Own(stolen, Scene.Enemy, "refinery", 40, 40);
        var factory = Own(stolen, Scene.Enemy, "war_factory", 44, 46);
        Own(stolen, Scene.Enemy, "tech_center", 50, 40);
        stolen.Spawn(Scene.Me, Type("alliance_spy"), 30, 30);
        stolen.World.Player(Scene.Me).StolenTech.Add("union");
        stolen.World.VisionOf(Scene.Me).Reveal(new CellPos(46, 44), 14);
        stolen.Look();

        new SpyManager(Scene.Settings).Update(stolen.Context());

        Assert.Equal(factory.Id, Assert.Single(stolen.RunCommands().OfType<EnterCommand>()).Building);
    }

    [Fact]
    public void Easy_sends_no_spy()
    {
        var scene = new Scene(rules: Game);
        Own(scene, Scene.Enemy, "tech_center", 50, 40);
        scene.Spawn(Scene.Me, Type("alliance_spy"), 30, 30);
        scene.World.VisionOf(Scene.Me).Reveal(new CellPos(50, 40), 8);
        scene.Look();

        new SpyManager(Scene.Settings with { Quality = Profiles.BuildOrderQuality.Simple }).Update(scene.Context());

        Assert.Empty(scene.RunCommands());
    }

    [Fact]
    public void On_islands_every_enemy_has_a_landing_on_its_own_land()
    {
        var scene = Islands();
        scene.Look();
        var reading = scene.Context().Map;

        Assert.False(reading.LandRoute);
        Assert.Equal(3, reading.Landings.Length);
        foreach (var landing in reading.Landings)
        {
            Assert.NotEqual(LandType.Deep, scene.World.Map.LandAt(landing));
            Assert.True(Scene.Corners.Skip(1).Any(start => Distance(start, landing) <= 7), $"{landing}");
        }
    }

    [Fact]
    public void On_islands_the_ferry_orders_a_transport_and_the_army_keeps_its_land_units()
    {
        var scene = Islands();
        Base(scene);
        scene.World.Player(Scene.Me).Credits = 3000;
        scene.Look();
        var context = scene.Context();

        new FerryManager().Update(context);
        var production = new ProductionManager(Scene.Settings with { Army = "standard_army" });
        production.Target(context);

        var produce = Assert.Single(scene.RunCommands().OfType<ProduceCommand>());
        Assert.Equal("hover_transport", Game.Database.Units[produce.DefIndex].Id);
        Assert.Contains(production.Shares, share => Game.Database.Units[share.Unit].Category != UnitCategory.Naval);
    }

    [Fact]
    public void On_islands_a_ready_squad_waits_for_the_ferry_instead_of_setting_out_by_land()
    {
        var scene = Islands();
        Base(scene);
        var tanks = Enumerable.Range(0, 3).Select(i => scene.Spawn(Scene.Me, Type("heavy_tank"), 11 + i, 12)).ToList();
        scene.Look();
        var context = scene.Context();

        new SquadManager(Out).Update(context);

        var squad = Assert.Single(context.Plan.Squads);
        Assert.Equal(SquadState.Ready, squad.State);
        Assert.Same(squad, context.Plan.FerryReady);
        Assert.Equal(tanks.Select(tank => tank.Id), squad.Units);
    }

    [Fact]
    public void The_ferry_boards_the_squad_carries_it_to_the_landing_and_puts_it_ashore()
    {
        var scene = Islands();
        Base(scene);
        var hover = scene.Spawn(Scene.Me, Type("hover_transport"), 12, 12);
        var tanks = Enumerable.Range(0, 2).Select(i => scene.Spawn(Scene.Me, Type("heavy_tank"), 12 + i, 13 + i)).ToList();
        var ferry = new FerryManager();
        var squads = new SquadManager(Out);

        // The squads find the squad ready; the ferry has it board.
        scene.Look(scene.World.CurrentTick);
        var context = scene.Context();
        squads.Update(context);
        Assert.NotNull(context.Plan.FerryReady);
        ferry.Update(context);
        var board = Assert.Single(scene.RunCommands().OfType<EnterCommand>());
        Assert.Equal(hover.Id, board.Building);
        Assert.Equal(tanks.Select(tank => tank.Id).Order(), board.Units.Order());

        // Once both are in, it sails for the landing.
        for (var tick = 0; tick < 150 && hover.Passengers.Count < 2; tick++)
        {
            scene.Simulation.Step();
        }

        Assert.Equal(2, hover.Passengers.Count);
        scene.Look(scene.World.CurrentTick);
        context = scene.Context();
        ferry.Update(context);
        var sail = Assert.Single(scene.RunCommands().OfType<MoveCommand>());
        var landing = sail.Target.ToCell();
        Assert.Contains(landing, context.Map.Landings);

        // At the landing it lets them out; the squads send them on as a squad that is out.
        Scene.Move(hover, landing.X, landing.Y);
        scene.Look(scene.World.CurrentTick);
        ferry.Update(scene.Context());
        Assert.Single(scene.RunCommands().OfType<DeployCommand>());
        Assert.Empty(hover.Passengers);

        scene.Look(scene.World.CurrentTick);
        context = scene.Context();
        ferry.Update(context);
        Assert.Equal(2, context.Plan.Landed.Count);
        squads.Update(context);
        Assert.Contains(context.Plan.Squads, squad => squad.State is SquadState.Moving or SquadState.Fighting && squad.Units.Count == 2);
        Assert.Empty(context.Plan.Errands);
    }

    /// <summary>Deep water but for an island of 15 × 15 cells round each start.</summary>
    private static Scene Islands()
    {
        var map = new MapGrid(64, 64, Game.Land);
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                if (!Scene.Corners.Any(start => Distance(start, new CellPos(x, y)) <= 7))
                {
                    map.SetLand(new CellPos(x, y), LandType.Deep);
                }
            }
        }

        return new Scene(rules: Game, map: map);
    }

    /// <summary>The base of the bot on its island: the yard, a reactor, a refinery, barracks, a war factory and a shipyard on the water.</summary>
    private static void Base(Scene scene)
    {
        Own(scene, Scene.Me, "construction_yard", 6, 6);
        Own(scene, Scene.Me, "reactor", 2, 2);
        Own(scene, Scene.Me, "refinery", 10, 2);
        Own(scene, Scene.Me, "barracks", 2, 9);
        Own(scene, Scene.Me, "war_factory", 2, 11);
        Own(scene, Scene.Me, "naval_shipyard", 16, 2);
    }

    private static Building Own(Scene scene, PlayerId owner, string id, int x, int y) =>
        scene.World.CreateBuilding(owner, Game.Database.Buildings[Game.Database.IndexOfBuilding(id)], new CellPos(x, y), ready: true, null);

    private static UnitType Type(string id) => UnitTypes.TryGetById(id, out var type) ? type : throw new System.ArgumentException(id);

    private static int Distance(CellPos a, CellPos b) => System.Math.Max(System.Math.Abs(a.X - b.X), System.Math.Abs(a.Y - b.Y));
}
