using Rts.Simulation.Benchmarks;
using Rts.Simulation.Geometry;
using Rts.Simulation.Tests.Client;
using Rts.Simulation.Tests.Systems;
using Rts.Simulation.World;
using Xunit.Abstractions;

namespace Rts.Simulation.Tests.Load;

/// <summary>
/// The load scenario of docs/implementation/M2_unit_control.md, 4.15, without timing: CI checks that it does not break
/// (occupancy, arrival, replay), the benchmarks measure it by hand.
/// </summary>
public sealed class LoadScenarioTests(ITestOutputHelper output)
{
    /// <summary>Measured: 90 % stand at tick 1414, the last at 1639; 282 of 300 places taken, nobody farther than a cell.</summary>
    private const int MinTaken = 280;
    private const int MaxOff = 4;

    [Fact]
    public void TheCommittedLoadMapIsWhatTheGeneratorWrites()
    {
        var committed = File.ReadAllText(Path.Combine(ClientFiles.RepoRoot, LoadScenario.MapPath)).Replace("\r\n", "\n");

        Assert.Equal(LoadMapGenerator.Generate(), committed);
    }

    [Fact]
    public void ThreeHundredUnitsCrossTheMapAndStandNearTheirPlaces()
    {
        var map = LoadScenario.LoadMap(ClientFiles.RepoRoot, TestRules.Standard);
        var simulation = LoadScenario.Start(map, TestRules.Standard);
        var units = simulation.World.Entities.OfType<Unit>().ToList();
        Assert.Equal(300, units.Count);
        LoadScenario.OrderAcrossTheMap(simulation);

        var run = GroupRun.Run(simulation, units);

        output.WriteLine($"across the map: {run}");
        Assert.True(run.PlacesTaken >= MinTaken, $"Only {run.PlacesTaken} of 300 places taken.");
        Assert.True(run.FarthestOff <= MaxOff, $"A unit stands {run.FarthestOff} cells from any place.");
        Assert.True(run.TickAll * 4 <= run.Tick90 * 5, $"The last unit stood at tick {run.TickAll}, 90 % at {run.Tick90}.");
    }

    [Fact]
    public void TheLoadScenarioReplaysToTheSameChecksums()
    {
        var map = LoadScenario.LoadMap(ClientFiles.RepoRoot, TestRules.Standard);

        Assert.Equal(Run(map), Run(map));
    }

    private static List<ulong> Run(Rts.Simulation.Map.TestMap map)
    {
        var simulation = LoadScenario.Start(map, TestRules.Standard);
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
