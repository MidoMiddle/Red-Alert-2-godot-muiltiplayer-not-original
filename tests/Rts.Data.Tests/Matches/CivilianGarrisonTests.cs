using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// Soldiers in the civilian buildings of the map (RA2 garrisons): the first one in makes an empty house his, more of his follow while
/// there is room, an enemy's house is closed; they fire from it; they come out on an order — the house is nobody's again — and when it
/// falls. Engineers, dogs and tech buildings stay out of it. By the game data, without the fog of war; two people play, no bot.
/// </summary>
public sealed class CivilianGarrisonTests
{
    private const int Size = 64;
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);
    private static readonly CellPos House = new(30, 30);
    private static readonly CellPos Derrick = new(40, 30);

    private static GameSimulation Match()
    {
        var land = new byte[Size * Size];
        Array.Fill(land, (byte)LandType.Clear);
        var map = MapGrid.FromLayers(Size, Size, land, new byte[land.Length], default, null, [],
            [
                new BuildingState(BuildingKind.NeutralHouseA, House, PlayerId.Neutral, 0),
                new BuildingState(BuildingKind.TechOilDerrick, Derrick, PlayerId.Neutral, 0),
            ]);
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "garrison",
                    56,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                map),
            GameDataFiles.Rules);
        simulation.World.VisionOf(Union).ExploreAll();
        simulation.World.VisionOf(Allied).ExploreAll();
        return simulation;
    }

    private static void Run(GameSimulation simulation, int ticks, Func<bool>? until = null)
    {
        for (var tick = 0; tick < ticks && until?.Invoke() != true; tick++)
        {
            simulation.Step();
        }
    }

    private static Unit Spawn(GameWorld world, PlayerId owner, UnitType type, int x, int y) =>
        world.SpawnUnit(owner, type, new CellPos(x, y).Center, snapToSubcell: true);

    [Fact]
    public void The_first_soldier_in_makes_the_house_his_and_his_others_follow_while_there_is_room()
    {
        var simulation = Match();
        var world = simulation.World;
        var house = world.BuildingAt(House)!;
        Assert.Equal(3, GarrisonSystem.Capacity(house));
        var gis = Enumerable.Range(0, 4).Select(i => Spawn(world, Allied, UnitType.AllianceRifleman, 26 + i, 34)).ToList();
        var conscript = Spawn(world, Union, UnitType.Conscript, 34, 34);
        Assert.Equal(EntryAction.Garrison, EntrySystem.ActionOf(world, gis[0], house));
        Assert.Equal(EntryAction.Garrison, EntrySystem.ActionOf(world, conscript, house));

        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [gis[0].Id], house.Id));
        Run(simulation, 300, () => house.Occupants.Count == 1);
        Assert.Equal(Allied, house.Owner);
        Assert.False(gis[0].IsAlive);
        // An enemy's house is closed.
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, conscript, house));

        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 1, [gis[1].Id, gis[2].Id, gis[3].Id], house.Id));
        Run(simulation, 300);
        Assert.Equal(3, house.Occupants.Count);
        Assert.True(gis[3].IsAlive);
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, gis[3], house));
    }

    [Fact]
    public void The_soldiers_fire_from_the_house_and_come_out_on_an_order_leaving_it_neutral()
    {
        var simulation = Match();
        var world = simulation.World;
        var house = world.BuildingAt(House)!;
        var gi = Spawn(world, Allied, UnitType.AllianceRifleman, 28, 33);
        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [gi.Id], house.Id));
        Run(simulation, 300, () => house.Occupants.Count == 1);

        var conscript = Spawn(world, Union, UnitType.Conscript, 35, 31);
        Run(simulation, 300, () => !conscript.IsAlive);
        Assert.False(conscript.IsAlive);

        simulation.Enqueue(new EvacuateCommand(Allied, simulation.CurrentTick, 1, house.Id));
        Run(simulation, 2);
        Assert.Empty(house.Occupants);
        Assert.True(house.Owner.IsNeutral);
        Assert.Contains(world.Entities, entity => entity is Unit { IsAlive: true, Type: UnitType.AllianceRifleman } && entity.Owner == Allied);
    }

    [Fact]
    public void The_soldiers_come_out_when_the_house_falls()
    {
        var simulation = Match();
        var world = simulation.World;
        var house = world.BuildingAt(House)!;
        var gi = Spawn(world, Allied, UnitType.AllianceRifleman, 28, 33);
        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [gi.Id], house.Id));
        Run(simulation, 300, () => house.Occupants.Count == 1);

        var tanks = Enumerable.Range(0, 4).Select(i => Spawn(world, Union, UnitType.HeavyTank, 36, 28 + i)).ToList();
        simulation.Enqueue(new AttackCommand(Union, simulation.CurrentTick, 1, [.. tanks.Select(t => t.Id)], house.Id));
        Run(simulation, 3000, () => !house.IsAlive);

        Assert.False(house.IsAlive);
        Assert.Contains(world.Entities, entity => entity is Unit { IsAlive: true, Type: UnitType.AllianceRifleman } && entity.Owner == Allied);
    }

    [Fact]
    public void Engineers_dogs_and_tech_buildings_stay_out_of_it()
    {
        var simulation = Match();
        var world = simulation.World;
        var house = world.BuildingAt(House)!;
        var derrick = world.BuildingAt(Derrick)!;
        var gi = Spawn(world, Allied, UnitType.AllianceRifleman, 28, 33);

        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, Spawn(world, Allied, UnitType.AllianceAttackDog, 27, 33), house));
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, Spawn(world, Allied, UnitType.AllianceEngineer, 26, 33), house));
        Assert.Equal(EntryAction.None, EntrySystem.ActionOf(world, gi, derrick));
        Assert.Equal(0, GarrisonSystem.Capacity(derrick));
    }
}
