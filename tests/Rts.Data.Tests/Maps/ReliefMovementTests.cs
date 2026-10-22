using System.Collections.Immutable;
using Rts.Data.Maps;
using Rts.MapTool;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Maps;

/// <summary>Paths and movement on the relief test map (docs/implementation/M7_maps.md, 4.3–4.4, step 3): ramps, cliffs, slopes.</summary>
public sealed class ReliefMovementTests
{
    private static readonly PlayerId Blue = new(1);

    private static MapGrid Relief() => RtsMapReader.Read(File.ReadAllText(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps", "relief_test.rtsmap"))).ToMapGrid();

    [Theory]
    [InlineData(Locomotor.Foot)]
    [InlineData(Locomotor.Track)]
    [InlineData(Locomotor.Wheel)]
    public void The_way_up_the_plateau_takes_a_ramp_and_never_a_cliff(Locomotor locomotor)
    {
        var map = Relief();

        var path = AStar.FindPath(map, new CellPos(12, 10), ReliefTestMap.PlateauCell, locomotor);

        Assert.Equal(PathStatus.Found, path.Status);
        Assert.DoesNotContain(path.Cells, cell => map.Heights.ShapeAt(cell) == CellShape.Cliff);
        Assert.Contains(path.Cells, cell => map.Heights.ShapeAt(cell) == CellShape.Slope);
    }

    [Fact]
    public void The_shelf_behind_cliffs_cannot_be_reached_and_the_path_ends_at_its_foot()
    {
        var map = Relief();

        var path = AStar.FindPath(map, new CellPos(12, 10), ReliefTestMap.ShelfCell, Locomotor.Track);

        Assert.Equal(PathStatus.Unreachable, path.Status);
        Assert.NotEqual(CellShape.Cliff, map.Heights.ShapeAt(path.End));
        Assert.InRange(path.End.X, 12, 23);
        Assert.InRange(path.End.Y, 0, 8);
    }

    [Fact]
    public void A_tank_is_slower_up_a_ramp_than_on_flat_ground()
    {
        // East ramp: four slope cells from x 8 to 11 on row 4, the plateau beyond; on row 0 the same distance is flat.
        var up = TicksToDrive(new CellPos(12, 4), new CellPos(7, 4));
        var flat = TicksToDrive(new CellPos(17, 0), new CellPos(12, 0));

        Assert.True(up > flat + 4, $"Up the ramp {up} ticks, on flat ground {flat}.");
    }

    [Fact]
    public void Nobody_spawns_on_a_cliff()
    {
        var simulation = new GameSimulation(new SimulationSettings(1, Relief()), GameDataFiles.Rules);
        simulation.Enqueue(new SpawnTestUnitCommand(Blue, 0, 0, UnitType.Conscript, ReliefTestMap.PlateauWallCell.Center));
        simulation.Enqueue(new SpawnTestUnitCommand(Blue, 0, 1, UnitType.Conscript, ReliefTestMap.PlateauCell.Center));

        simulation.Step();

        var unit = Assert.Single(simulation.World.Entities.OfType<Unit>());
        Assert.Equal(ReliefTestMap.PlateauCell, unit.Position.ToCell());
    }

    [Fact]
    public void Paths_on_the_relief_map_are_the_same_on_every_system()
    {
        // Golden value: FNV-1a over the cells of paths for foot, track and wheel between far corners of the map.
        var map = Relief();
        var hash = new Fnv1a64();
        foreach (var locomotor in new[] { Locomotor.Foot, Locomotor.Track, Locomotor.Wheel })
        {
            foreach (var (from, to) in new[] { (new CellPos(0, 0), new CellPos(23, 23)), (new CellPos(23, 0), ReliefTestMap.PitCell), (new CellPos(0, 23), ReliefTestMap.PlateauCell) })
            {
                var path = PathSmoother.Smooth(map, AStar.FindPath(map, from, to, locomotor), locomotor);
                hash.Add((byte)path.Status);
                hash.Add(path.Cost);
                foreach (var cell in path.Waypoints)
                {
                    hash.Add(cell.X);
                    hash.Add(cell.Y);
                }
            }
        }

        Assert.Equal(15580552373648312171UL, hash.Value);
    }

    private static int TicksToDrive(CellPos from, CellPos to)
    {
        var simulation = new GameSimulation(new SimulationSettings(5, Relief()), GameDataFiles.Rules);
        simulation.Enqueue(new SpawnTestUnitCommand(Blue, 0, 0, UnitType.HeavyTank, from.Center));
        simulation.Step();
        var tank = simulation.World.Entities.OfType<Unit>().Single();
        simulation.Enqueue(new MoveCommand(Blue, simulation.CurrentTick, 1, [tank.Id], to.Center));
        var ticks = 0;
        do
        {
            simulation.Step();
            ticks++;
            Assert.True(ticks < 2000, "The tank never arrived.");
        }
        while (tank.MoveTarget is not null || tank.PathPending);

        Assert.Equal(to, tank.Position.ToCell());
        return ticks;
    }
}
