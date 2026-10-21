using Rts.Simulation.Benchmarks;
using Rts.Simulation.Tests.Client;

namespace Rts.Simulation.Tests.Load;

/// <summary>
/// The load scenario of docs/implementation/M2_unit_control.md, 4.15, without timing: CI checks that it does not break
/// (occupancy, arrival, replay), the benchmarks measure it by hand.
/// </summary>
public sealed class LoadScenarioTests
{
    [Fact]
    public void TheCommittedLoadMapIsWhatTheGeneratorWrites()
    {
        var committed = File.ReadAllText(Path.Combine(ClientFiles.RepoRoot, LoadScenario.MapPath)).Replace("\r\n", "\n");

        Assert.Equal(LoadMapGenerator.Generate(), committed);
    }

    [Fact]
    public void TheLoadScenarioReplaysToTheSameChecksums()
    {
        var map = LoadScenario.LoadMap(ClientFiles.RepoRoot);

        Assert.Equal(Run(map), Run(map));
    }

    private static List<ulong> Run(Rts.Simulation.Map.TestMap map)
    {
        var simulation = LoadScenario.Start(map);
        LoadScenario.OrderAcrossTheMap(simulation);
        var checksums = new List<ulong>();
        for (var i = 0; i < 300; i++)
        {
            simulation.Step();
            checksums.Add(simulation.LastChecksum!.Value.Value);
        }

        return checksums;
    }
}
