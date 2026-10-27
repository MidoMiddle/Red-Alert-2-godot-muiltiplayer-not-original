using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The repairs of RA2 (docs/ra2_buildings): 8 hit points every 14 ticks for 15% of the cost of a whole repair, a building by the wrench
/// and a vehicle at the service depot alike; the depot takes one vehicle at a time; the shipyards mend ships; a wall is not repaired;
/// the MCV needs the service depot. By the game data, without the fog of war; two people play, no bot.
/// </summary>
public sealed class RepairRulesTests
{
    private const int Size = 64;
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    // Deep water east of x = 44.
    private const int Shore = 44;

    private static GameSimulation Match()
    {
        var land = new byte[Size * Size];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                land[(y * Size) + x] = (byte)(x >= Shore ? LandType.Deep : LandType.Clear);
            }
        }

        var map = MapGrid.FromLayers(Size, Size, land, new byte[Size * Size], default, null, null, [], types: GameDataFiles.Rules.MapTypes);
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "repair",
                    29,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false, StartingUnits: 0),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(4, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(4, 60)]),
                map),
            GameDataFiles.Rules);
        simulation.World.VisionOf(Union).ExploreAll();
        simulation.World.VisionOf(Allied).ExploreAll();
        return simulation;
    }

    private static Building Build(GameWorld world, PlayerId player, string id, int x, int y)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(player, def, new CellPos(x, y), true, null);
    }

    private static void Run(GameSimulation simulation, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            simulation.Step();
        }
    }

    [Fact]
    public void A_building_gains_eight_every_fourteen_ticks_and_a_whole_repair_costs_fifteen_percent()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Union, "reactor", 30, 30);
        var refinery = Build(world, Union, "refinery", 10, 10);
        world.SetHp(refinery, 0 + 1, null);
        var economy = world.Rules.Economy;
        Assert.Equal((8, 14), (economy.RepairRate, economy.RepairDelay));

        var before = world.Player(Union).Credits;
        simulation.Enqueue(new ToggleRepairCommand(Union, simulation.CurrentTick, 0, refinery.Id));
        Run(simulation, 14 * 10 + 1);
        Assert.InRange(refinery.Hp, 1 + (8 * 9), 1 + (8 * 11));

        // To whole: 15% of its cost, give or take a credit of rounding.
        for (var tick = 0; tick < 20_000 && refinery.Repairing; tick++)
        {
            simulation.Step();
        }

        Assert.Equal(refinery.MaxHp, refinery.Hp);
        Assert.InRange(before - world.Player(Union).Credits, (refinery.Def!.Cost * 15 / 100) - 2, (refinery.Def.Cost * 15 / 100) + 2);
    }

    [Fact]
    public void The_service_depot_mends_one_vehicle_at_a_time()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Union, "reactor", 30, 30);
        var depot = Build(world, Union, "service_depot", 10, 10);
        var first = world.SpawnUnit(Union, UnitType.HeavyTank, new CellPos(depot.FarCell.X + 1, depot.Cell.Y).Center);
        var second = world.SpawnUnit(Union, UnitType.HeavyTank, new CellPos(depot.FarCell.X + 1, depot.Cell.Y + 2).Center);
        world.SetHp(first, 100, null);
        world.SetHp(second, 100, null);

        Run(simulation, 14 * 5);

        var mended = (first.Hp - 100) + (second.Hp - 100);
        Assert.InRange(mended, 8 * 4, 8 * 6);
        Assert.True(first.Hp == 100 || second.Hp == 100, "only one is on the pad");
    }

    [Fact]
    public void The_shipyard_mends_a_ship_beside_it()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Union, "nuclear_reactor", 30, 30);
        var yard = Build(world, Union, "naval_shipyard", 50, 20);
        var ship = world.SpawnUnit(Union, UnitType.SeaScorpion, new CellPos(yard.FarCell.X + 1, yard.Cell.Y + 1).Center);
        world.SetHp(ship, ship.MaxHp / 2, null);

        Run(simulation, 14 * 5);

        Assert.True(ship.Hp > ship.MaxHp / 2);
    }

    [Fact]
    public void A_wall_is_not_repaired()
    {
        var simulation = Match();
        var world = simulation.World;
        var wall = Build(world, Union, "wall", 20, 20);
        world.SetHp(wall, wall.MaxHp / 2, null);
        var engineer = world.SpawnUnit(Union, UnitType.Engineer, new CellPos(20, 22).Center, snapToSubcell: true);

        var toggle = new ToggleRepairCommand(Union, simulation.CurrentTick, 0, wall.Id);
        simulation.Enqueue(toggle);
        Run(simulation, 30);

        Assert.False(wall.Repairing);
        Assert.Contains(toggle, simulation.RefusedCommands);
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, engineer, wall));
        Assert.Equal(wall.MaxHp / 2, wall.Hp);
    }

    [Fact]
    public void The_mcv_needs_the_service_depot()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Union, "construction_yard", 4, 4);
        Build(world, Union, "nuclear_reactor", 30, 30);
        Build(world, Union, "barracks", 10, 4);
        Build(world, Union, "refinery", 14, 4);
        Build(world, Union, "war_factory", 20, 4);
        var mcv = world.Rules.Database.IndexOfUnit("mcv");
        TechTree.Update(world);
        Assert.False(world.Player(Union).CanBuildUnit(mcv));

        Build(world, Union, "service_depot", 26, 4);
        TechTree.Update(world);
        Assert.True(world.Player(Union).CanBuildUnit(mcv));
    }
}
