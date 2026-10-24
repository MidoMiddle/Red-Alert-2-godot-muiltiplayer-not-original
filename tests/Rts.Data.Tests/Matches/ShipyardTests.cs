using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The shipyard on the water and its ships (stage 2a of filling the game): it stands on deep water only, it alone builds the ships,
/// and a unit whose factory's exit it cannot enter comes out beside the factory. By the game data; two people play, no bot.
/// </summary>
public sealed class ShipyardTests
{
    private static readonly PlayerId Union = new(1);

    // A lake of deep water east of the base, a strip of shallow water south of it.
    private static readonly (int X0, int Y0, int X1, int Y1) Lake = (20, 6, 31, 17);

    private static GameSimulation Match()
    {
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "lake",
                    5,
                    new MatchOptions(StartingCredits: 50_000),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(10, 10)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(55, 55)),
                    ],
                    [new CellPos(10, 10), new CellPos(55, 55)]),
                new MapGrid(64, 64)),
            GameDataFiles.Rules);
        var map = simulation.World.Map;
        for (var y = Lake.Y0; y <= Lake.Y1; y++)
        {
            for (var x = Lake.X0; x <= Lake.X1; x++)
            {
                map.SetLand(new CellPos(x, y), LandType.Deep);
            }
        }

        for (var x = 6; x <= 14; x++)
        {
            map.SetLand(new CellPos(x, 22), LandType.Shallow);
        }

        return simulation;
    }

    private static BuildingDef Def(GameWorld world, string id)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return def;
    }

    private static Building Build(GameWorld world, string id, int x, int y) =>
        world.CreateBuilding(Union, Def(world, id), new CellPos(x, y), true, null);

    /// <summary>The base of the Union up to the shipyard's prerequisite, the refinery, west of the lake.</summary>
    private static void Base(GameWorld world)
    {
        Build(world, "construction_yard", 8, 8);
        Build(world, "reactor", 12, 8);
        Build(world, "refinery", 14, 11);
        TechTree.Update(world);
    }

    [Fact]
    public void The_shipyard_stands_on_deep_water_only_and_a_land_building_never_does()
    {
        var world = Match().World;
        Base(world);
        var shipyard = Def(world, "naval_shipyard");

        Assert.True(shipyard.Water);
        Assert.Equal(PlacementProblem.None, PlacementRules.Check(world, Union, shipyard, new CellPos(21, 8)));
        // Half ashore; on shallow water and land.
        Assert.Equal(PlacementProblem.NotBuildable, PlacementRules.Check(world, Union, shipyard, new CellPos(17, 8)));
        Assert.Equal(PlacementProblem.NotBuildable, PlacementRules.Check(world, Union, shipyard, new CellPos(8, 20)));
        Assert.Equal(PlacementProblem.NotBuildable, PlacementRules.Check(world, Union, Def(world, "reactor"), new CellPos(22, 8)));
    }

    [Fact]
    public void Only_the_shipyard_builds_ships_and_they_come_out_on_the_water()
    {
        var simulation = Match();
        var world = simulation.World;
        Base(world);
        Build(world, "barracks", 8, 13);
        Build(world, "war_factory", 8, 16);
        TechTree.Update(world);
        var rules = world.Rules.Database;
        var sub = rules.IndexOfUnit("typhoon_sub");

        Assert.Equal(ProductionCategory.Naval, ProductionRules.CategoryOf(rules, ProductionKind.Unit, sub));
        Assert.False(world.Player(Union).CanBuildUnit(sub), "no shipyard yet");

        Build(world, "naval_shipyard", 22, 8);
        TechTree.Update(world);
        Assert.True(world.Player(Union).CanBuildUnit(sub));
        Assert.False(world.Player(Union).CanBuildUnit(rules.IndexOfUnit("kirov_airship")), "aircraft wait for flight");

        simulation.Enqueue(new ProduceCommand(Union, simulation.CurrentTick, 0, ProductionKind.Unit, sub, 1));
        Unit? ship = null;
        for (var tick = 0; tick < 1500 && ship is null; tick++)
        {
            simulation.Step();
            ship = world.Entities.OfType<Unit>().FirstOrDefault(unit => unit.Owner == Union && unit.Type == UnitType.TyphoonSub);
        }

        Assert.NotNull(ship);
        Assert.Equal(LandType.Deep, world.Map.LandAt(ship.Position.ToCell()));
        Assert.Empty(world.Player(Union).Queue(ProductionCategory.Vehicle));
    }

    [Fact]
    public void A_unit_whose_exit_it_cannot_enter_comes_out_beside_its_factory()
    {
        // The war factory's exit is a cell of deep water: the tank comes out on land beside the factory, the queue goes on.
        var simulation = Match();
        var world = simulation.World;
        Base(world);
        Build(world, "war_factory", 24, 18);
        TechTree.Update(world);
        var exit = new CellPos(25, 22);
        world.Map.SetLand(exit, LandType.Deep);
        var tank = world.Rules.Database.IndexOfUnit("heavy_tank");

        simulation.Enqueue(new ProduceCommand(Union, simulation.CurrentTick, 0, ProductionKind.Unit, tank, 2));
        var tanks = 0;
        for (var tick = 0; tick < 2000 && tanks < 2; tick++)
        {
            simulation.Step();
            tanks = world.Entities.OfType<Unit>().Count(unit => unit.Owner == Union && unit.Type == UnitType.HeavyTank);
        }

        Assert.Equal(2, tanks);
        Assert.All(
            world.Entities.OfType<Unit>().Where(unit => unit.Owner == Union && unit.Type == UnitType.HeavyTank),
            unit => Assert.NotEqual(LandType.Deep, world.Map.LandAt(unit.Position.ToCell())));
    }
}
