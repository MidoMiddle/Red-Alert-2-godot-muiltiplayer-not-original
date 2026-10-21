using System.Collections.Immutable;
using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Data.Tests.Maps;

/// <summary>Maps for the tests: a small one that uses every part of the format, and the files of the repository.</summary>
internal static class SampleMaps
{
    public static string RepoRoot { get; } = FindRoot();

    public static string SandboxPath => Path.Combine(RepoRoot, "game", "data", "maps", "sandbox.rtsmap");

    /// <summary>8 x 8, eight players, every height level, land type, ore code and section, owners neutral and a slot.</summary>
    public static MapDefinition Full()
    {
        const int size = 8;
        var heights = new byte[(size + 1) * (size + 1)];
        for (var i = 0; i < heights.Length; i++)
        {
            heights[i] = (byte)(i % (MapDefinition.MaxHeight + 1));
        }

        var land = new LandType[size * size];
        var texture = new char[size * size];
        var ore = new byte[size * size];
        for (var i = 0; i < land.Length; i++)
        {
            land[i] = (LandType)(1 + i % 8);
            texture[i] = "sdA9"[i % 4];
            ore[i] = (byte)(i % (MapDefinition.MaxOre + 1));
        }

        return new MapDefinition(
            "full-test", "Full \"test\" map: all of it", "Tests \\ M7", "Every section once.", size, size, "winter", 8,
            [.. Enumerable.Range(1, 8).Select(slot => new MapStart(slot, new CellPos(slot - 1, 7 - (slot - 1))))],
            [new('s', "snow"), new('d', "dirt"), new('A', "asphalt"), new('9', "ice")],
            [.. heights], [.. land], [.. texture], [.. ore],
            [new("pine_tree", new CellPos(2, 3), 2), new("fence_wood", new CellPos(1, 1), 0), new("pine_tree", new CellPos(1, 3), 255)],
            [new("bridge_wood_low", new CellPos(4, 1), new CellPos(4, 5)), new("bridge_wood_low", new CellPos(0, 6), new CellPos(3, 6))],
            [new("neutral_house_a", new CellPos(6, 6), 0, 3), new("tech_oil_derrick", new CellPos(5, 2), 2, 0)]);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RtsGame.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No RtsGame.sln above the test binary.");
    }
}
