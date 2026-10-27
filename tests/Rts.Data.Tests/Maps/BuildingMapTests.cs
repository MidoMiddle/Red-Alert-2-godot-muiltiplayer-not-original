using Rts.Simulation.Tests;
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

    private static string HamletText() => RtsMapWriter.Write(TestMapConverter.Convert(TestMapParser.Parse(Hamlet, TestRules.Standard), "hamlet", "Hamlet"));

    [Fact]
    public void Structures_of_the_file_become_the_start_buildings_by_the_table()
    {
        var text = HamletText();
        Assert.Contains("  - { type: neutral_house_a, cell: [2, 2], owner: neutral, facing: 3 }", text);
        Assert.Contains("  - { type: tech_oil_derrick, cell: [5, 5], owner: 2, facing: 0 }", text);

        var grid = RtsMapReader.Read(text).ToMapGrid(GameDataFiles.Rules.MapTypes);

        Assert.Equal(
            new[]
            {
                new BuildingState(BuildingKind.NeutralHouseA, new CellPos(2, 2), PlayerId.Neutral, 3),
                new BuildingState(BuildingKind.TechOilDerrick, new CellPos(5, 5), new PlayerId(2), 0),
            },
            grid.StartBuildings);
        Assert.True(grid.HasBuilding(new CellPos(3, 3)));
        Assert.False(grid.CanEnter(new CellPos(6, 6), Locomotor.Track));
        Assert.True(grid.ContentEquals(TestMapParser.Parse(Hamlet, TestRules.Standard).Grid));
    }

    [Fact]
    public void A_building_of_a_faction_s_data_stands_on_the_map_for_nobody_by_the_footprint_of_the_data()
    {
        // 2026-10-27: RA2's maps place the Neutral house's defenses and tech; a structure of the file may name a building of the data.
        var text = HamletText().Replace("type: neutral_house_a, cell: [2, 2]", "type: alliance_prism_tower, cell: [2, 2]")
            .Replace("type: tech_oil_derrick, cell: [5, 5], owner: 2", "type: nuclear_reactor, cell: [3, 3], owner: neutral");
        var map = RtsMapReader.Read(text);
        var grid = map.ToMapGrid(GameDataFiles.Rules.MapTypes);

        Assert.Equal(
            new[]
            {
                BuildingState.OfData("alliance_prism_tower", new(1, 1), new CellPos(2, 2), 3),
                BuildingState.OfData("nuclear_reactor", new(4, 4), new CellPos(3, 3), 0),
            },
            grid.StartBuildings);
        Assert.True(grid.HasBuilding(new CellPos(6, 6)));
        Assert.Equal(16, MapChecks.Footprint(map.Structures[1], GameDataFiles.Rules.MapTypes).Count);
        Assert.DoesNotContain(MapValidator.Check(map, GameDataFiles.Rules.MapTypes), problem => problem.Message.Contains("reactor", StringComparison.Ordinal));

        // The editor keeps them as they are.
        var session = new Rts.Data.Maps.Editing.MapEditSession(Rts.Data.Maps.Editing.EditableMap.FromDefinition(map), GameDataFiles.Rules.MapTypes);
        Assert.Equal(RtsMapWriter.Write(map), RtsMapWriter.Write(session.Map.ToDefinition()));
        Assert.Equal(grid.StartBuildings, session.ViewGrid().StartBuildings);
    }

    [Fact]
    public void A_building_of_a_faction_s_data_for_a_player_is_an_error()
    {
        var map = RtsMapReader.Read(HamletText().Replace("type: tech_oil_derrick, cell: [5, 5], owner: 2", "type: alliance_pillbox, cell: [5, 5], owner: 2"));

        Assert.Contains("for nobody (neutral) only", Assert.Throws<ArgumentException>(() => map.ToMapGrid(GameDataFiles.Rules.MapTypes)).Message);
        Assert.Contains(MapValidator.Check(map, GameDataFiles.Rules.MapTypes), problem => problem.Message.Contains("for nobody (neutral) only", StringComparison.Ordinal));
    }

    [Fact]
    public void A_building_type_the_table_does_not_know_is_an_error()
    {
        var map = RtsMapReader.Read(HamletText().Replace("type: neutral_house_a", "type: neutral_castle"));

        var error = Assert.Throws<ArgumentException>(() => map.ToMapGrid(GameDataFiles.Rules.MapTypes));

        Assert.Contains("neutral_castle", error.Message);
    }

    [Fact]
    public void A_building_on_the_water_is_an_error()
    {
        var map = RtsMapReader.Read(HamletText().Replace("cell: [2, 2]", "cell: [0, 5]"));

        var error = Assert.Throws<ArgumentException>(() => map.ToMapGrid(GameDataFiles.Rules.MapTypes));

        Assert.Contains("cell 0,6 is Shallow", error.Message);
    }

    [Fact]
    public void The_sandbox_village_stands_in_the_way_and_a_fallen_house_leaves_its_ruins_there()
    {
        var grid = RtsMapReader.Read(File.ReadAllText(SampleMaps.SandboxPath)).ToMapGrid(GameDataFiles.Rules.MapTypes);
        Assert.Equal(7, grid.StartBuildings.Count);
        Assert.All(grid.StartBuildings, b => Assert.True(b.Owner.IsNeutral));
        Assert.Equal(3, grid.StartBuildings.Count(b => GameDataFiles.Rules.MapTypes.Get(b.Kind).Capturable));

        // The house at (36, 52)…(37, 53): a jeep drives round it, and round its ruins once it is down (RA2).
        var simulation = new Rts.Simulation.GameSimulation(new Rts.Simulation.SimulationSettings(4, grid), GameDataFiles.Rules);
        var round = AStar.FindPath(simulation.World.Map, new CellPos(34, 52), new CellPos(39, 52), Locomotor.Wheel);
        Assert.DoesNotContain(round.Cells, simulation.World.Map.HasBuilding);
        for (var blow = 0; blow < 4; blow++)
        {
            simulation.Enqueue(new Rts.Simulation.Commands.DamageMapObjectCommand(new PlayerId(1), 0, (uint)blow, new CellPos(37, 53), 200));
        }

        simulation.Step();

        Assert.Equal(6, simulation.World.Entities.Count);
        var after = AStar.FindPath(simulation.World.Map, new CellPos(34, 52), new CellPos(39, 52), Locomotor.Wheel);
        Assert.DoesNotContain(new CellPos(36, 52), after.Cells);
        Assert.True(simulation.World.Map.IsBlocked(new CellPos(36, 52)));
    }
}
