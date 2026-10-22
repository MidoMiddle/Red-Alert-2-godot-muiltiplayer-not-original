using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Maps;

/// <summary>Buildings of a <c>.rtsmap</c> file in the simulation (docs/implementation/M7_maps.md, 4.8, step 7).</summary>
public sealed class BuildingMapTests
{
    // 8 x 8 of clear land with a pond in the south-west, a neutral house and an oil derrick of slot 2.
    private const string Hamlet = """
        . . . . . . . .
        . . . . . . . .
        . . . . . . . .
        . . . . . . . .
        . . . . . . . .
        . . . . . . . .
        ~ ~ . . . . . .
        ~ ~ . . . . . .
        building neutral_house_a 2 2 0 3
        building tech_oil_derrick 5 5 2
        """;

    private static string HamletText() => RtsMapWriter.Write(TestMapConverter.Convert(TestMapParser.Parse(Hamlet), "hamlet", "Hamlet"));

    [Fact]
    public void Structures_of_the_file_become_the_start_buildings_by_the_table()
    {
        var text = HamletText();
        Assert.Contains("  - { type: neutral_house_a, cell: [2, 2], owner: neutral, facing: 3 }", text);
        Assert.Contains("  - { type: tech_oil_derrick, cell: [5, 5], owner: 2, facing: 0 }", text);

        var grid = RtsMapReader.Read(text).ToMapGrid();

        Assert.Equal(
            new[]
            {
                new BuildingState(BuildingKind.NeutralHouseA, new CellPos(2, 2), PlayerId.Neutral, 3),
                new BuildingState(BuildingKind.TechOilDerrick, new CellPos(5, 5), new PlayerId(2), 0),
            },
            grid.StartBuildings);
        Assert.True(grid.HasBuilding(new CellPos(3, 3)));
        Assert.False(grid.CanEnter(new CellPos(6, 6), Locomotor.Track));
        Assert.True(grid.ContentEquals(TestMapParser.Parse(Hamlet).Grid));
    }

    [Fact]
    public void A_building_type_the_table_does_not_know_is_an_error()
    {
        var map = RtsMapReader.Read(HamletText().Replace("type: neutral_house_a", "type: neutral_castle"));

        var error = Assert.Throws<ArgumentException>(() => map.ToMapGrid());

        Assert.Contains("neutral_castle", error.Message);
    }

    [Fact]
    public void A_building_on_the_water_is_an_error()
    {
        var map = RtsMapReader.Read(HamletText().Replace("cell: [2, 2]", "cell: [0, 5]"));

        var error = Assert.Throws<ArgumentException>(() => map.ToMapGrid());

        Assert.Contains("cell 0,6 is Shallow", error.Message);
    }

    [Fact]
    public void The_sandbox_village_stands_in_the_way_until_a_house_falls()
    {
        var grid = RtsMapReader.Read(File.ReadAllText(SampleMaps.SandboxPath)).ToMapGrid();
        Assert.Equal(7, grid.StartBuildings.Count);
        Assert.All(grid.StartBuildings, b => Assert.True(b.Owner.IsNeutral));
        Assert.Equal(3, grid.StartBuildings.Count(b => BuildingTypes.Get(b.Kind).Capturable));

        // The house at (36, 52)…(37, 53): a jeep drives round it, and straight through once it is down.
        var simulation = new Rts.Simulation.GameSimulation(new Rts.Simulation.SimulationSettings(4, grid));
        var round = AStar.FindPath(simulation.World.Map, new CellPos(34, 52), new CellPos(39, 52), Locomotor.Wheel);
        Assert.DoesNotContain(round.Cells, simulation.World.Map.HasBuilding);
        for (var blow = 0; blow < 4; blow++)
        {
            simulation.Enqueue(new Rts.Simulation.Commands.DamageMapObjectCommand(new PlayerId(1), 0, (uint)blow, new CellPos(37, 53), 200));
        }

        simulation.Step();

        Assert.Equal(6, simulation.World.Entities.Count);
        var through = AStar.FindPath(simulation.World.Map, new CellPos(34, 52), new CellPos(39, 52), Locomotor.Wheel);
        Assert.Contains(new CellPos(36, 52), through.Cells);
    }
}
