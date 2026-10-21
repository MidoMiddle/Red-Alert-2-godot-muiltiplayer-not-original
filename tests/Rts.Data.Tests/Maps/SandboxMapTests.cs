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
    public void Land_matches_cell_by_cell_and_trees_become_objects()
    {
        var old = OldSandbox().Grid;
        var map = RtsMapReader.Read(File.ReadAllText(SampleMaps.SandboxPath));

        for (var y = 0; y < old.Height; y++)
        {
            for (var x = 0; x < old.Width; x++)
            {
                var cell = new CellPos(x, y);
                Assert.Equal(old.LandAt(cell), map.LandAt(cell));
                Assert.Equal(old.IsBlocked(cell), map.Objects.Any(o => o.Cell == cell && o.Type == TestMapConverter.PineTree));
            }
        }

        Assert.All(map.Heights, h => Assert.Equal(0, h));
    }

    [Fact]
    public void The_sandbox_hash_is_the_same_on_every_system()
    {
        // Golden value: the canonical text and SHA-256 must not depend on the OS or the runtime.
        var map = RtsMapReader.Read(File.ReadAllText(SampleMaps.SandboxPath));

        Assert.Equal("6b62f7cf4c4a9a50fa01505531a7303ef3ccb4f97896e45d83c7958feae267ac", MapHasher.Hash(map));
    }
}
