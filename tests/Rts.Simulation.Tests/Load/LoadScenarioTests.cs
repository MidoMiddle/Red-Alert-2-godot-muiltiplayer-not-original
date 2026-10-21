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
    private const int MaxTicks = 3000;

    /// <summary>Measured at step 15: 285 of 300 on their own cell, the rest pushed aside by the crowd at the goal, at most 4 cells.</summary>
    private const int MinExact = 280;
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
        var map = LoadScenario.LoadMap(ClientFiles.RepoRoot);
        var simulation = LoadScenario.Start(map);
        Assert.Equal(300, simulation.World.Entities.OfType<Unit>().Count());
        LoadScenario.OrderAcrossTheMap(simulation);
        simulation.Step();
        var places = simulation.World.Entities.OfType<Unit>().ToDictionary(u => u.Id, u => u.Orders[^1].Point.ToCell());

        var tick = 1;
        for (; tick < MaxTicks && !AllStand(simulation.World); tick++)
        {
            simulation.Step();
            if (tick % 50 == 0)
            {
                ReservationTests.AssertOccupancy(simulation.World);
            }
        }

        ReservationTests.AssertOccupancy(simulation.World);
        var distances = simulation.World.Entities.OfType<Unit>().Select(u => Chebyshev(u.Position.ToCell(), places[u.Id])).ToList();
        output.WriteLine($"all standing after {tick} ticks; on their own cell {distances.Count(d => d == 0)}, farthest {distances.Max()}");

        Assert.True(AllStand(simulation.World), $"Units still moving after {MaxTicks} ticks.");
        Assert.True(distances.Count(d => d == 0) >= MinExact, $"Only {distances.Count(d => d == 0)} units on their own cell.");
        Assert.True(distances.Max() <= MaxOff, $"A unit stands {distances.Max()} cells from its place.");
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

    private static bool AllStand(GameWorld world) =>
        world.Entities.OfType<Unit>().All(u => u.MoveTarget is null && !u.PathPending && u.Path is null);

    private static int Chebyshev(CellPos a, CellPos b) => System.Math.Max(System.Math.Abs(a.X - b.X), System.Math.Abs(a.Y - b.Y));
}
