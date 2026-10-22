using Rts.Data.Maps;
using Rts.MapTool;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Maps;

/// <summary>Debug squads at the starts of a <c>.rtsmap</c> map (docs/implementation/M7_maps.md, step 4).</summary>
public sealed class StartSquadsTests
{
    private static MapDefinition Relief() => RtsMapReader.Read(File.ReadAllText(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps", "relief_test.rtsmap")));

    [Fact]
    public void Slot_one_gets_every_type_and_the_others_four_units()
    {
        var map = Relief();
        var grid = map.ToMapGrid();

        var units = StartSquads.Place(map, grid, GameDataFiles.Rules);

        var blue = units.Where(u => u.Owner == new PlayerId(1)).ToList();
        var red = units.Where(u => u.Owner == new PlayerId(2)).ToList();
        Assert.Equal(GameDataFiles.Rules.Units.Select(a => a.Type), blue.Select(u => u.Type));
        Assert.Equal([UnitType.HeavyTank, UnitType.SupportVehicle, UnitType.Conscript, UnitType.Rocketeer], red.Select(u => u.Type));
        Assert.Equal(ReliefTestMap.BlueStart, blue[0].Cell);
        Assert.Equal(new CellPos(ReliefTestMap.BlueStart.X + 3, ReliefTestMap.BlueStart.Y + 1), blue[7].Cell);
        Assert.Equal(new CellPos(ReliefTestMap.RedStart.X + 1, ReliefTestMap.RedStart.Y + 1), red[3].Cell);
        Assert.All(units, u => Assert.Equal(CellShape.Flat, grid.Heights.ShapeAt(u.Cell)));
    }

    [Fact]
    public void Cells_off_the_map_or_closed_to_a_unit_are_skipped()
    {
        var size = 8;
        var land = Enumerable.Repeat(LandType.Clear, size * size).ToArray();
        land[7 * size + 7] = LandType.Deep;
        var map = new MapDefinition("edge", "Edge", "", "", size, size, "winter", 2, [new MapStart(1, new CellPos(5, 6)), new MapStart(2, new CellPos(6, 6))],
            [new('s', "snow")], [.. new byte[(size + 1) * (size + 1)]], [.. land], [.. Enumerable.Repeat('s', size * size)], [.. new byte[size * size]], [], [], []);
        var grid = map.ToMapGrid();

        var units = StartSquads.Place(map, grid, GameDataFiles.Rules);

        Assert.All(units, u => Assert.True(grid.CanEnter(u.Cell, GameDataFiles.Rules.Unit(u.Type).Locomotor)));
        Assert.DoesNotContain(units, u => u.Cell == new CellPos(7, 7));
        Assert.Equal(3, units.Count(u => u.Owner == new PlayerId(2)));
    }

    [Fact]
    public void The_blue_squad_drives_up_the_plateau_by_a_ramp()
    {
        var definition = Relief();
        var grid = definition.ToMapGrid();
        var simulation = new GameSimulation(new SimulationSettings(3, grid), GameDataFiles.Rules);
        uint sequence = 0;
        foreach (var unit in StartSquads.Place(definition, grid, GameDataFiles.Rules))
        {
            simulation.Enqueue(new SpawnTestUnitCommand(unit.Owner, 0, sequence++, unit.Type, unit.Cell.Center));
        }

        simulation.Step();
        var blue = simulation.World.Entities.OfType<Unit>().Where(u => u.Owner == new PlayerId(1)).ToList();
        Assert.Equal(8, blue.Count);
        simulation.Enqueue(new MoveCommand(new PlayerId(1), simulation.CurrentTick, sequence++, [.. blue.Select(u => u.Id)], ReliefTestMap.PlateauCell.Center));
        simulation.Step();
        var climbed = false;
        for (var tick = 0; tick < 3000 && blue.Any(u => u.MoveTarget is not null || u.PathPending || u.HoldTicks > 0); tick++)
        {
            simulation.Step();
            Assert.All(blue, u => Assert.NotEqual(CellShape.Cliff, grid.Heights.ShapeAt(u.Position.ToCell())));
            climbed |= blue.Any(u => grid.Heights.ShapeAt(u.Position.ToCell()) == CellShape.Slope);
        }

        Assert.True(climbed, "The squad went up a ramp.");
        Assert.All(blue, u => Assert.True(grid.Heights.CellLevel(u.Position.ToCell()) == 4, $"{u.Archetype.Id} at {u.Position.ToCell()}, level {grid.Heights.CellLevel(u.Position.ToCell())}, shape {grid.Heights.ShapeAt(u.Position.ToCell())}"));
    }
}
