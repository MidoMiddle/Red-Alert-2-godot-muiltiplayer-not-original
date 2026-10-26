using System.Linq;
using Rts.AI.Knowledge;
using Rts.AI.Modules;
using Rts.Data.Tests;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Rules;
using Rts.Simulation.World;
using Xunit;

namespace Rts.AI.Tests;

/// <summary>
/// The water in the plans of a bot (M11, the bot that knows every weapon), on scenes of the game's rules: what it reads from the map
/// (<see cref="MapReading"/>), the share of the fleet in its army, the shipyard it builds and the fleet that sails (<see cref="FleetManager"/>).
/// The bot is of the Union, its start the north-west corner (8, 8); the enemies may be at the three other corners.
/// </summary>
public sealed class NavalScenarioTests
{
    private static GameRules Game => GameDataFiles.Rules;

    /// <summary>A sea along the north of the map, south of the bot's start: y 14 to 21, from edge to edge.</summary>
    private static readonly (int X, int Y, int Width, int Height) Band = (0, 14, 64, 8);

    [Fact]
    public void A_map_of_land_has_a_way_by_land_and_none_by_water()
    {
        var reading = Read(Water());

        Assert.True(reading.LandRoute);
        Assert.False(reading.NavalRoute);
        Assert.Null(reading.Harbour);
    }

    [Fact]
    public void A_lake_that_reaches_no_enemy_is_no_way_to_it()
    {
        var reading = Read(Water((14, 2, 8, 8)));

        Assert.True(reading.LandRoute);
        Assert.False(reading.NavalRoute);
    }

    [Fact]
    public void A_sea_along_the_bases_leads_to_the_enemy_beside_by_water()
    {
        var scene = Water(Band);
        var reading = Read(scene);

        Assert.True(reading.LandRoute);
        Assert.True(reading.NavalRoute);
        var harbour = Assert.NotNull(reading.Harbour);
        for (var y = harbour.Y; y < harbour.Y + 4; y++)
        {
            for (var x = harbour.X; x < harbour.X + 4; x++)
            {
                Assert.Equal(LandType.Deep, scene.World.Map.LandAt(new CellPos(x, y)));
            }
        }

        // Only the enemy of the north-east corner (56, 8) is within reach of the sea: its shore is the sea 6 cells south of it.
        var shore = Assert.Single(reading.EnemyShores);
        Assert.Equal(14, shore.Y);
        Assert.InRange(shore.X, 50, 62);
    }

    [Fact]
    public void On_islands_the_way_is_by_water_only()
    {
        var reading = Read(Islands());

        Assert.False(reading.LandRoute);
        Assert.True(reading.NavalRoute);
        Assert.Equal(3, reading.EnemyShores.Length);
    }

    [Fact]
    public void With_a_way_by_land_too_the_fleet_gets_its_share_of_the_army()
    {
        var scene = Water(Band);
        Base(scene, harbour: new CellPos(4, 15));
        var shares = Shares(scene);

        // The standard army of the Union: 70 % of it on the ground, 30 % ships by the same roles. Of the ships only the submarine is open
        // without a radar: it takes the shares of its roles — against ships 45 %, against buildings 15 % — and the fleet's share
        // against soldiers and the air stays empty.
        var ships = shares.Where(share => Game.Database.Units[share.Unit].Category == UnitCategory.Naval).Sum(share => share.Share);
        var ground = shares.Where(share => Game.Database.Units[share.Unit].Category != UnitCategory.Naval).Sum(share => share.Share);
        Assert.Equal(3_000 * 60 / 100, ships);
        Assert.InRange(ground, 6_990, 7_000);
        Assert.Contains(shares, share => Game.Database.Units[share.Unit].Id == "typhoon_sub");
        Assert.Contains(shares, share => Game.Database.Units[share.Unit].Id == "heavy_tank");
    }

    [Fact]
    public void On_islands_with_a_ferry_the_army_keeps_its_land_units_and_its_fleet()
    {
        // The Union has the hovercraft (M11 step 3): the ferry takes the land army over, so the army is as on any map with water.
        var scene = Islands();
        Base(scene, harbour: new CellPos(16, 2));
        var shares = Shares(scene);

        Assert.Contains(shares, share => Game.Database.Units[share.Unit].Category == UnitCategory.Naval);
        Assert.Contains(shares, share => Game.Database.Units[share.Unit].Category != UnitCategory.Naval);
    }

    [Fact]
    public void Ships_are_not_built_on_a_lake_that_leads_nowhere()
    {
        var scene = Water((14, 2, 8, 8));
        Base(scene, harbour: new CellPos(15, 3));
        var shares = Shares(scene);

        Assert.DoesNotContain(shares, share => Game.Database.Units[share.Unit].Category == UnitCategory.Naval);
    }

    [Fact]
    public void On_a_map_whose_water_leads_to_the_enemy_the_bot_builds_a_shipyard()
    {
        var scene = Water(Band);
        Base(scene, harbour: null);
        scene.World.VisionOf(Scene.Me).Reveal(new CellPos(10, 16), 12);
        scene.World.Player(Scene.Me).Credits = 3000;
        scene.Look();
        var planner = new BuildPlanner(Scene.Settings with { BuildOrder = "balanced_simple", MaxRefineries = 1 });

        planner.Update(scene.Context());

        var produce = Assert.Single(scene.RunCommands().OfType<ProduceCommand>());
        Assert.Equal("naval_shipyard", Game.Database.Buildings[produce.DefIndex].Id);
    }

    [Fact]
    public void A_harbour_not_seen_yet_is_looked_at_before_the_shipyard_is_ordered()
    {
        var scene = Water((0, 20, 64, 8));
        Base(scene, harbour: null);
        scene.World.Player(Scene.Me).Credits = 3000;
        var conscript = scene.Spawn(Scene.Me, Type("conscript"), 8, 12);
        scene.Look();
        var context = scene.Context();
        var harbour = Assert.NotNull(context.Map.Harbour);
        Assert.False(scene.View.IsExplored(new CellPos(harbour.X + 3, harbour.Y + 3)));
        var planner = new BuildPlanner(Scene.Settings with { BuildOrder = "balanced_simple", MaxRefineries = 1 });

        planner.Update(context);

        var commands = scene.RunCommands();
        Assert.DoesNotContain(commands, command => command is ProduceCommand);
        var move = Assert.Single(commands.OfType<MoveCommand>());
        Assert.Equal(conscript.Id, Assert.Single(move.Units));
        Assert.Equal(19, move.Target.ToCell().Y);
    }

    [Fact]
    public void The_fleet_waits_for_enough_ships_then_sails_for_the_enemy_shore()
    {
        var scene = Water(Band);
        var yard = Base(scene, harbour: new CellPos(4, 15))!;
        var exit = new CellPos(yard.Cell.X + 1, yard.Cell.Y + 4);
        scene.Spawn(Scene.Me, Type("sea_scorpion"), exit.X, exit.Y);
        scene.Spawn(Scene.Me, Type("sea_scorpion"), exit.X + 1, exit.Y);
        var fleet = new FleetManager(Scene.Settings with { FirstAttackFrom = 0 });
        scene.Look();

        fleet.Update(scene.Context());

        Assert.False(fleet.Out);
        Assert.DoesNotContain(scene.RunCommands(), command => command is AttackMoveCommand);

        scene.Spawn(Scene.Me, Type("typhoon_sub"), exit.X + 2, exit.Y);
        scene.Look(scene.World.CurrentTick);
        var context = scene.Context();
        fleet.Update(context);

        Assert.True(fleet.Out);
        var attack = Assert.Single(scene.RunCommands().OfType<AttackMoveCommand>());
        Assert.Equal(3, attack.Units.Length);
        Assert.Equal(context.Map.EnemyShores[0], attack.Target.ToCell());
    }

    [Fact]
    public void A_beaten_fleet_sails_home()
    {
        var scene = Water(Band);
        var yard = Base(scene, harbour: new CellPos(4, 15))!;
        var ships = Enumerable.Range(0, 3).Select(i => scene.Spawn(Scene.Me, Type("sea_scorpion"), yard.Cell.X + 1 + i, yard.Cell.Y + 4)).ToList();
        var fleet = new FleetManager(Scene.Settings with { FirstAttackFrom = 0 });
        scene.Look();
        fleet.Update(scene.Context());
        scene.RunCommands();
        Assert.True(fleet.Out);

        foreach (var ship in ships)
        {
            ship.Hp = ship.MaxHp / 5;
        }

        scene.Look(scene.World.CurrentTick);
        fleet.Update(scene.Context());

        Assert.False(fleet.Out);
        var home = Assert.Single(scene.RunCommands().OfType<MoveCommand>());
        Assert.Equal(new CellPos(yard.Cell.X + 1, yard.Cell.Y + 4), home.Target.ToCell());
    }

    [Fact]
    public void Ships_do_not_join_the_squads_nor_scout()
    {
        var scene = Water(Band);
        Base(scene, harbour: new CellPos(4, 15));
        var ship = scene.Spawn(Scene.Me, Type("sea_scorpion"), 6, 19);
        var tank = scene.Spawn(Scene.Me, Type("heavy_tank"), 8, 10);
        scene.Look();
        var context = scene.Context();

        Assert.True(context.Units.InFleet(ship));
        Assert.False(context.Units.InArmy(ship));
        Assert.True(context.Units.InArmy(tank));
    }

    /// <summary>A scene of the game's rules: clear land, with deep water in the rectangles.</summary>
    private static Scene Water(params (int X, int Y, int Width, int Height)[] water)
    {
        var map = new MapGrid(64, 64, Game.Land);
        foreach (var (left, top, width, height) in water)
        {
            for (var y = top; y < top + height; y++)
            {
                for (var x = left; x < left + width; x++)
                {
                    map.SetLand(new CellPos(x, y), LandType.Deep);
                }
            }
        }

        return new Scene(rules: Game, map: map);
    }

    /// <summary>Deep water but for an island of 15 × 15 cells round each start.</summary>
    private static Scene Islands()
    {
        var map = new MapGrid(64, 64, Game.Land);
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                var island = Scene.Corners.Any(start => System.Math.Max(System.Math.Abs(x - start.X), System.Math.Abs(y - start.Y)) <= 7);
                if (!island)
                {
                    map.SetLand(new CellPos(x, y), LandType.Deep);
                }
            }
        }

        return new Scene(rules: Game, map: map);
    }

    /// <summary>
    /// The base of the bot by its start: the yard, a reactor, a refinery, barracks and a war factory — and the shipyard at
    /// <paramref name="harbour"/> unless it is null; returns the shipyard.
    /// </summary>
    private static Building? Base(Scene scene, CellPos? harbour)
    {
        Build(scene, "construction_yard", 6, 6);
        Build(scene, "reactor", 2, 2);
        Build(scene, "refinery", 10, 2);
        Build(scene, "barracks", 2, 9);
        Build(scene, "war_factory", 2, 11);
        return harbour is { } at ? Build(scene, "naval_shipyard", at.X, at.Y) : null;
    }

    private static Building Build(Scene scene, string id, int x, int y) =>
        scene.World.CreateBuilding(Scene.Me, Game.Database.Buildings[Game.Database.IndexOfBuilding(id)], new CellPos(x, y), ready: true, null);

    private static MapReading Read(Scene scene)
    {
        scene.Look();
        return scene.Context().Map;
    }

    private static System.Collections.Generic.IReadOnlyList<(int Unit, long Share)> Shares(Scene scene)
    {
        scene.Look();
        var production = new ProductionManager(Scene.Settings with { Army = "standard_army" });
        production.Target(scene.Context());
        return production.Shares;
    }

    private static UnitType Type(string id) => UnitTypes.TryGetById(id, out var type) ? type : throw new System.ArgumentException(id);
}
