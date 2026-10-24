using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Data.Tests.Maps;

/// <summary>The sandbox of M2 in the new format: game/data/maps/sandbox.rtsmap, made by the converter.</summary>
public sealed class SandboxMapTests
{
    private static TestMap OldSandbox() => TestMapParser.Parse(File.ReadAllText(Path.Combine(SampleMaps.RepoRoot, "game", "maps", "test", "sandbox.txt")));

    [Fact]
    public void The_committed_file_is_the_canonical_conversion_of_the_m2_sandbox()
    {
        var converted = TestMapConverter.Convert(OldSandbox(), "sandbox", "Sandbox");
        var committed = File.ReadAllText(SampleMaps.SandboxPath).Replace("\r\n", "\n");

        Assert.Equal(RtsMapWriter.Write(converted), committed);
    }

    [Fact]
    public void Land_matches_cell_by_cell_and_objects_carry_over()
    {
        var old = OldSandbox().Grid;
        var map = RtsMapReader.Read(File.ReadAllText(SampleMaps.SandboxPath));

        for (var y = 0; y < old.Height; y++)
        {
            for (var x = 0; x < old.Width; x++)
            {
                var cell = new CellPos(x, y);
                Assert.Equal(old.LandAt(cell), map.LandAt(cell));
            }
        }

        // Trees and the object lines of the ASCII map become map objects of the same types, cells and variants.
        Assert.Equal(
            old.Objects.Select(o => (o.Type.Id, o.Cell, o.Variant)).OrderBy(o => o.Id, StringComparer.Ordinal).ThenBy(o => o.Cell.Y).ThenBy(o => o.Cell.X),
            map.Objects.Select(o => (o.Type, o.Cell, o.Variant)));

        // The bridge line (M7 step 6) becomes the bridge of the map file.
        Assert.Equal(
            old.Bridges.Select(b => new BridgePlacement(b.Type.Id, b.From, b.To)),
            map.Bridges);

        // So do the building lines (M7 step 7), as structures.
        Assert.Equal(
            old.StartBuildings.Select(b => new StructurePlacement(Rts.Simulation.World.BuildingTypes.Get(b.Kind).Id, b.Cell, b.Owner.Value, b.Facing))
                .OrderBy(s => s.Type, StringComparer.Ordinal).ThenBy(s => s.Cell.Y).ThenBy(s => s.Cell.X),
            map.Structures);

        Assert.All(map.Heights, h => Assert.Equal(0, h));
    }

    [Fact]
    public void The_sandbox_hash_is_the_same_on_every_system()
    {
        // Golden value: the canonical text and SHA-256 must not depend on the OS or the runtime.
        var map = RtsMapReader.Read(File.ReadAllText(SampleMaps.SandboxPath));

        Assert.Equal("cb36f1b99e81e828af7a2ba81701acbbdcf8a80f3afacf28ba3a4d3f739d03a8", MapHasher.Hash(map));
    }

    [Fact]
    public void On_the_sandbox_a_tank_crushes_the_fence_and_a_conscript_goes_round_it()
    {
        var grid = RtsMapReader.Read(File.ReadAllText(SampleMaps.SandboxPath)).ToMapGrid();
        var simulation = new Rts.Simulation.GameSimulation(new Rts.Simulation.SimulationSettings(4, grid), GameDataFiles.Rules);
        var blue = new Rts.Simulation.World.PlayerId(1);
        simulation.Enqueue(new Rts.Simulation.Commands.SpawnTestUnitCommand(blue, 0, 0, Rts.Simulation.World.UnitType.HeavyTank, new CellPos(7, 36).Center));
        simulation.Enqueue(new Rts.Simulation.Commands.SpawnTestUnitCommand(blue, 0, 1, Rts.Simulation.World.UnitType.Conscript, new CellPos(9, 36).Center));
        simulation.Step();
        var units = simulation.World.Entities.OfType<Rts.Simulation.World.Unit>().ToList();
        var (tank, conscript) = (units[0], units[1]);
        simulation.Enqueue(new Rts.Simulation.Commands.MoveCommand(blue, simulation.CurrentTick, 2, [tank.Id], new CellPos(7, 40).Center));
        simulation.Enqueue(new Rts.Simulation.Commands.MoveCommand(blue, simulation.CurrentTick, 3, [conscript.Id], new CellPos(9, 40).Center));
        var conscriptCells = new HashSet<CellPos>();
        var crushed = false;
        for (var tick = 0; tick < 1500; tick++)
        {
            simulation.Step();
            conscriptCells.Add(conscript.Position.ToCell());
            crushed |= simulation.LastTickEvents.OfType<Rts.Simulation.Events.MapObjectDestroyed>().Any(e => e.Kind == MapObjectKind.FenceWood);
        }

        Assert.True(crushed, "The tank crushed the fence.");
        Assert.Equal(new CellPos(7, 40), tank.Position.ToCell());
        Assert.Equal(new CellPos(9, 40), conscript.Position.ToCell());
        Assert.DoesNotContain(conscriptCells, cell => cell.Y == 38 && cell.X is >= 4 and <= 11 && cell.X != 7);
    }

    [Fact]
    public void On_the_sandbox_a_jeep_crosses_the_lake_by_the_bridge_until_a_segment_falls()
    {
        var grid = RtsMapReader.Read(File.ReadAllText(SampleMaps.SandboxPath)).ToMapGrid();
        var bridge = Assert.Single(grid.Bridges);
        Assert.Equal((new CellPos(2, 57), new CellPos(10, 57), 9), (bridge.From, bridge.To, bridge.Segments.Count));
        Assert.All(bridge.Segments, s => Assert.Equal(LandType.Road, grid.SurfaceAt(s.Cell)));

        var across = Rts.Simulation.Pathfinding.AStar.FindPath(grid, new CellPos(0, 57), new CellPos(12, 57), Locomotor.Wheel);
        Assert.Equal(Rts.Simulation.Pathfinding.PathStatus.Found, across.Status);
        Assert.Contains(new CellPos(6, 57), across.Cells);

        var simulation = new Rts.Simulation.GameSimulation(new Rts.Simulation.SimulationSettings(4, grid), GameDataFiles.Rules);
        var blue = new Rts.Simulation.World.PlayerId(1);
        simulation.Enqueue(new Rts.Simulation.Commands.DamageMapObjectCommand(blue, 0, 0, new CellPos(6, 57), 600));
        simulation.Step();

        var around = Rts.Simulation.Pathfinding.AStar.FindPath(simulation.World.Map, new CellPos(0, 57), new CellPos(12, 57), Locomotor.Wheel);
        Assert.Equal(Rts.Simulation.Pathfinding.PathStatus.Found, around.Status);
        Assert.DoesNotContain(around.Cells, cell => cell.Y == 57 && cell.X is >= 2 and <= 10);
    }
}
