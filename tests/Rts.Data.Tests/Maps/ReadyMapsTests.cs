using System.Collections.Immutable;
using Rts.Data.Maps;
using Rts.MapTool;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Maps;

/// <summary>The four ready maps of M7 step 14 (docs/implementation/M7_maps.md, 4.15): files, checks, fairness, a match on each.</summary>
public sealed class ReadyMapsTests
{
    public static readonly TheoryData<string> Files = new(ReadyMaps.Files);

    private static MapDefinition Committed(string file) =>
        RtsMapReader.Read(File.ReadAllText(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps", file + ".rtsmap")));

    [Theory]
    [MemberData(nameof(Files))]
    public void The_committed_file_is_what_the_script_builds(string file)
    {
        var committed = File.ReadAllText(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps", file + ".rtsmap")).Replace("\r\n", "\n");

        Assert.Equal(RtsMapWriter.Write(ReadyMaps.Build(file, GameDataFiles.Rules.MapTypes)), committed);
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Every_map_passes_the_checks_and_is_fair(string file)
    {
        var map = Committed(file);

        Assert.Empty(MapValidator.Check(map, GameDataFiles.Rules.MapTypes));
        Assert.Equal(map.Players, map.Starts.Length);

        var ore = map.Starts.Select(s => OreNear(map, s.Cell)).ToList();
        Assert.All(ore, amount => Assert.Equal(ore[0], amount));
        Assert.True(ore[0] > 0);
    }

    [Fact]
    public void Each_map_has_what_its_description_promises()
    {
        var frozen = Committed("frozen-crossing");
        // A rectangle on the screen as in RA2: the area inscribed in a 90 x 90 grid, about the 4096 cells of the old 64 x 64.
        Assert.Equal((90, 90, 2, 2), (frozen.Width, frozen.Height, frozen.Players, frozen.Bridges.Length));
        Assert.Equal(MapArea.Inscribed(90), frozen.Playable);
        Assert.Contains(LandType.Ice, frozen.Land);

        var twin = Committed("twin-rivers");
        Assert.Equal((96, 96, 2, 4), (twin.Width, twin.Height, twin.Players, twin.Bridges.Length));
        Assert.Equal(4, twin.CornerHeight(48, 48));

        var taiga = Committed("taiga-front");
        Assert.Equal((128, 96, 4), (taiga.Width, taiga.Height, taiga.Players));
        Assert.True(taiga.Objects.Length > 2000, $"{taiga.Objects.Length} trees");
        Assert.Contains(taiga.Structures, s => s.Type == "neutral_apartment");
        Assert.Equal(4, taiga.Structures.Count(s => s.Type == "neutral_house_a"));

        var corners = Committed("four-corners");
        Assert.Equal((128, 128, 4), (corners.Width, corners.Height, corners.Players));
        Assert.Equal(4, corners.CornerHeight(64, 64));
        // The gem field and its drill at the centre of it (M8 step 10b).
        Assert.True(corners.Ore[58 * 128 + 59] >= 10, "gems on the plateau");
        Assert.Contains(corners.Objects, o => o is { Type: "gem_source", Cell: { X: 58, Y: 58 } });
        Assert.Contains(corners.Structures, s => s is { Type: "tech_oil_derrick", Cell: { X: 63, Y: 63 } });
    }

    [Theory]
    [InlineData("frozen-crossing", false, true)]
    [InlineData("twin-rivers", false, false)]
    [InlineData("taiga-front", false, false)]
    [InlineData("four-corners", true, false)]
    public void The_maps_are_symmetric(string file, bool turned, bool halfTurn)
    {
        var map = Committed(file);
        var (w, h) = (map.Width, map.Height);
        var trees = map.Objects.Select(o => o.Cell).ToHashSet();
        // Mirrored west to east, turned by quarters, or — a map with a playable area — turned by half (the screen's left is its right).
        CellPos Image(CellPos c) => turned ? new CellPos(w - 1 - c.Y, c.X) : halfTurn ? new CellPos(w - 1 - c.X, h - 1 - c.Y) : new CellPos(w - 1 - c.X, c.Y);
        (int, int) CornerImage(int x, int y) => turned ? (w - y, x) : halfTurn ? (w - x, h - y) : (w - x, y);

        for (var y = 0; y <= h; y++)
        {
            for (var x = 0; x <= w; x++)
            {
                var (ix, iy) = CornerImage(x, y);
                Assert.Equal(map.CornerHeight(x, y), map.CornerHeight(ix, iy));
            }
        }

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var cell = new CellPos(x, y);
                var image = Image(cell);
                Assert.Equal(map.LandAt(cell), map.LandAt(image));
                Assert.Equal(map.Ore[y * w + x], map.Ore[image.Y * w + image.X]);
                Assert.Equal(trees.Contains(cell), trees.Contains(image));
            }
        }
    }

    [Theory]
    [InlineData("frozen-crossing")]
    [InlineData("twin-rivers")]
    [InlineData("taiga-front")]
    [InlineData("four-corners")]
    public void On_every_map_the_first_squad_drives_towards_the_second_start(string file)
    {
        var map = Committed(file);
        var grid = map.ToMapGrid(GameDataFiles.Rules.MapTypes);
        var simulation = new GameSimulation(new SimulationSettings(seed: 3, grid), GameDataFiles.Rules);
        var sequences = new Dictionary<PlayerId, uint>();
        foreach (var unit in StartSquads.Place(map, grid, GameDataFiles.Rules))
        {
            sequences.TryGetValue(unit.Owner, out var sequence);
            simulation.Enqueue(new SpawnTestUnitCommand(unit.Owner, 0, sequence, unit.Type, unit.Cell.Center));
            sequences[unit.Owner] = sequence + 1;
        }

        simulation.Step();
        var (from, to) = (map.Starts[0].Cell, map.Starts[1].Cell);
        var blue = simulation.World.Entities.OfType<Unit>().Where(u => u.Owner == new PlayerId(1)).ToList();
        simulation.Enqueue(new MoveCommand(new PlayerId(1), simulation.CurrentTick, sequences[new PlayerId(1)], [.. blue.Select(u => u.Id)], to.Center));
        var crossed = new HashSet<LandType>();
        for (var tick = 0; tick < 600; tick++)
        {
            simulation.Step();
            foreach (var unit in blue.Where(u => u.IsAlive))
            {
                crossed.Add(grid.LandAt(unit.Position.ToCell()));
            }
        }

        // Halfway there, at least, by the tracks the path search finds.
        var start = Distance(from, to);
        Assert.Contains(blue, u => Distance(u.Position.ToCell(), to) * 2 <= start);
        if (map.Bridges.Length > 0)
        {
            Assert.True(crossed.Contains(LandType.Deep) || crossed.Contains(LandType.Ice), "the squad crossed the river by a bridge or the ice");
        }
    }

    private static int Distance(CellPos a, CellPos b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static int OreNear(MapDefinition map, CellPos start)
    {
        var sum = 0;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var (dx, dy) = (x - start.X, y - start.Y);
                sum += dx * dx + dy * dy <= MapValidator.OreRadius * MapValidator.OreRadius ? map.Ore[y * map.Width + x] : 0;
            }
        }

        return sum;
    }
}
