using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The buildings of the water (docs/naval_elements, 04 and 08): the offshore platform and the ships of nobody stand only on the water,
/// stop the fleet; a ship sinks and leaves the water open, the platform's ruins stay; a transport helicopter sets an engineer down on the
/// platform to take it (the owner, 2026-11-01). By the game data; two people play, no bot.
/// </summary>
public sealed class WaterBuildingsTests
{
    private const int Size = 48;
    private static readonly PlayerId Allied = new(2);

    // A sea over the east half of the map: deep from x = 26, shallow at x = 24 and 25.
    private static readonly CellPos Platform = new(30, 10);
    private static readonly CellPos Ferry = new(30, 20);
    private static readonly CellPos Boat = new(24, 30);

    private static byte[] Land()
    {
        var land = new byte[Size * Size];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                land[y * Size + x] = (byte)(x >= 26 ? LandType.Deep : x >= 24 ? LandType.Shallow : LandType.Clear);
            }
        }

        return land;
    }

    private static GameSimulation Match()
    {
        var map = MapGrid.FromLayers(Size, Size, Land(), new byte[Size * Size], default, null, null,
        [
            new BuildingState(BuildingKind.TechOffshorePlatform, Platform, PlayerId.Neutral, 0),
            new BuildingState(BuildingKind.NeutralFerry, Ferry, PlayerId.Neutral, 0),
            new BuildingState(BuildingKind.NeutralFishingBoat, Boat, PlayerId.Neutral, 0),
        ], types: GameDataFiles.Rules.MapTypes);
        return new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "water",
                    11,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(4, 44)),
                    ],
                    [new CellPos(4, 4), new CellPos(4, 44)]),
                map),
            GameDataFiles.Rules);
    }

    private static void Destroy(GameSimulation simulation, Building building)
    {
        var world = simulation.World;
        world.SetHp(building, 1, null);
        var cannon = world.Rules.Database.IndexOfWeapon("alliance_light_cannon");
        world.Hits.Add(new Rts.Simulation.Combat.Hit(building.Id, building.Id, Allied, cannon, Rts.Simulation.Combat.Aim.CentreOf(building), building.Id));
        for (var tick = 0; tick < 3; tick++)
        {
            simulation.Step();
        }
    }

    [Fact]
    public void The_platform_and_the_ships_stand_on_the_water_and_stop_the_fleet()
    {
        var world = Match().World;
        Assert.NotNull(world.BuildingAt(Platform));
        Assert.NotNull(world.BuildingAt(new CellPos(31, 11)));
        Assert.NotNull(world.BuildingAt(new CellPos(31, 20)));
        Assert.NotNull(world.BuildingAt(Boat));
        Assert.False(world.Map.CanEnter(new CellPos(31, 20), Locomotor.Naval));
        Assert.True(world.Map.CanEnter(new CellPos(32, 20), Locomotor.Naval));
    }

    [Fact]
    public void A_building_of_the_water_is_refused_on_land_and_one_of_the_land_on_the_water()
    {
        var types = GameDataFiles.Rules.MapTypes;
        Assert.Throws<ArgumentException>(() => MapGrid.FromLayers(Size, Size, Land(), new byte[Size * Size], default, null, null,
            [new BuildingState(BuildingKind.NeutralFerry, new CellPos(10, 10), PlayerId.Neutral, 0)], types: types));

        // Half on the shallow, half on the beach's land: no.
        Assert.Throws<ArgumentException>(() => MapGrid.FromLayers(Size, Size, Land(), new byte[Size * Size], default, null, null,
            [new BuildingState(BuildingKind.NeutralFerry, new CellPos(23, 10), PlayerId.Neutral, 0)], types: types));
        Assert.Throws<ArgumentException>(() => MapGrid.FromLayers(Size, Size, Land(), new byte[Size * Size], default, null, null,
            [new BuildingState(BuildingKind.TechOilDerrick, new CellPos(30, 30), PlayerId.Neutral, 0)], types: types));
    }

    private static void Run(GameSimulation simulation, int ticks, Func<bool> until)
    {
        for (var tick = 0; tick < ticks && !until(); tick++)
        {
            simulation.Step();
        }
    }

    [Fact]
    public void A_night_hawk_sets_an_engineer_down_on_the_platform_and_it_is_taken_and_pays()
    {
        var simulation = Match();
        var world = simulation.World;
        var platform = world.BuildingAt(Platform)!;
        var hawk = world.SpawnUnit(Allied, UnitType.AllianceNighthawk, new CellPos(18, 10).Center, snapToSubcell: true);
        var engineer = world.SpawnUnit(Allied, UnitType.AllianceEngineer, new CellPos(18, 13).Center, snapToSubcell: true);

        // Nobody walks to it, and a helicopter with nobody aboard takes nothing.
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, engineer, platform));
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, hawk, platform));

        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [engineer.Id], hawk.Id));
        Run(simulation, 200, () => hawk.Passengers.Count == 1);
        Assert.Single(hawk.Passengers);
        Assert.Equal(EntryAction.DropEngineer, EntrySystem.ActionOf(world, hawk, platform));

        var before = world.Player(Allied).Credits;
        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 1, [hawk.Id], platform.Id));
        Run(simulation, 400, () => platform.Owner == Allied);
        Assert.Equal(Allied, platform.Owner);
        Assert.Empty(hawk.Passengers);
        Assert.True(hawk.IsAlive);
        Assert.Equal(before + TechBuildingSystem.DerrickStartup, world.Player(Allied).Credits);

        // It pays as the derrick does; taken, the helicopter has nothing more to do there.
        Run(simulation, TechBuildingSystem.DerrickDelay, () => false);
        Assert.Equal(before + TechBuildingSystem.DerrickStartup + TechBuildingSystem.DerrickCash, world.Player(Allied).Credits);
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, hawk, platform));
    }

    [Fact]
    public void A_ship_sinks_and_leaves_the_water_open_the_platform_leaves_its_ruins()
    {
        var simulation = Match();
        var world = simulation.World;
        Destroy(simulation, world.BuildingAt(Ferry)!);
        Assert.Null(world.BuildingAt(Ferry));
        Assert.True(world.Map.CanEnter(Ferry, Locomotor.Naval));
        Assert.True(world.Map.CanEnter(new CellPos(31, 20), Locomotor.Naval));

        Destroy(simulation, world.BuildingAt(Platform)!);
        Assert.Null(world.BuildingAt(Platform));
        Assert.True(world.Map.IsBlocked(Platform));
        Assert.False(world.Map.CanEnter(Platform, Locomotor.Naval));
    }
}
