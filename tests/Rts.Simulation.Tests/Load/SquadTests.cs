using System.Collections.Immutable;
using Rts.Simulation.Benchmarks;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Tests.Client;
using Rts.Simulation.Tests.Systems;
using Rts.Simulation.World;
using Xunit.Abstractions;

namespace Rts.Simulation.Tests.Load;

/// <summary>
/// The squad of the M2 definition of done (docs/implementation/M2_unit_control.md, section 7): 40 tanks and 20 infantry go
/// around the forest and stand as a blob at the target, and all of them pass a gap of one cell. Thresholds as measured at
/// step 16 (see the journal): around the forest 56 of 60 on their own cell, through the gap 55; the rest within 2 cells.
/// Known: most of the squad stands by tick 600, but the last tanks, whose places are inside the blob, shuffle with their
/// neighbours until about tick 1900.
/// </summary>
public sealed class SquadTests(ITestOutputHelper output)
{
    private const int MaxTicks = 4000;
    private const int MinExact = 54;
    private const int MaxOff = 2;
    private static readonly PlayerId Blue = new(1);

    [Fact]
    public void TheCommittedSquadMapIsWhatTheGeneratorWrites()
    {
        var sandbox = File.ReadAllText(Path.Combine(ClientFiles.RepoRoot, SquadMap.SandboxPath));
        var committed = File.ReadAllText(Path.Combine(ClientFiles.RepoRoot, SquadMap.MapPath)).Replace("\r\n", "\n");

        Assert.Equal(SquadMap.Generate(sandbox), committed);
    }

    [Fact]
    public void FortyTanksAndTwentyInfantryGoAroundTheForestAndStandAsABlob()
    {
        var (simulation, squad) = Start();

        var (ticks, distances) = OrderAndRun(simulation, squad, new CellPos(24, 4));

        output.WriteLine($"around the forest: all standing after {ticks} ticks, on their own cell {distances.Count(d => d == 0)}/60, farthest {distances.Max()}");
        Assert.All(squad, unit => Assert.True(unit.Position.ToCell().Y < 9, $"{unit.Id} stayed south of the forest at {unit.Position.ToCell()}."));
        Assert.True(distances.Count(d => d == 0) >= MinExact, $"Only {distances.Count(d => d == 0)} of 60 on their own cell.");
        Assert.True(distances.Max() <= MaxOff, $"A unit stands {distances.Max()} cells from its place.");
    }

    [Fact]
    public void AllSixtyPassTheOneCellGapInTheWall()
    {
        var (simulation, squad) = Start();

        var (ticks, distances) = OrderAndRun(simulation, squad, new CellPos(SquadMap.GapColumn, 46));

        output.WriteLine($"through the gap: all standing after {ticks} ticks, on their own cell {distances.Count(d => d == 0)}/60, farthest {distances.Max()}");
        Assert.All(squad, unit => Assert.True(unit.Position.ToCell().Y > SquadMap.WallRow, $"{unit.Id} stayed before the wall at {unit.Position.ToCell()}."));
        Assert.True(distances.Count(d => d == 0) >= MinExact, $"Only {distances.Count(d => d == 0)} of 60 on their own cell.");
        Assert.True(distances.Max() <= MaxOff, $"A unit stands {distances.Max()} cells from its place.");
    }

    private static (GameSimulation Simulation, List<Unit> Squad) Start()
    {
        var map = TestMapParser.Parse(File.ReadAllText(Path.Combine(ClientFiles.RepoRoot, SquadMap.MapPath)));
        var simulation = new GameSimulation(new SimulationSettings(LoadScenario.Seed, map.Grid));
        uint sequence = 0;
        foreach (var unit in map.Units)
        {
            simulation.Enqueue(new SpawnTestUnitCommand(unit.Owner, 0, sequence++, unit.Type, unit.Cell.Center));
        }

        simulation.Step();
        var squad = simulation.World.Entities.OfType<Unit>().Where(u => u.Owner == Blue).ToList();
        Assert.Equal(SquadMap.Tanks + SquadMap.Conscripts, squad.Count);
        return (simulation, squad);
    }

    /// <summary>Orders the squad to <paramref name="target"/>, runs until everyone stands; distances of each from its place.</summary>
    private static (int Ticks, List<int> Distances) OrderAndRun(GameSimulation simulation, List<Unit> squad, CellPos target)
    {
        simulation.Enqueue(new MoveCommand(Blue, simulation.CurrentTick, 100, squad.Select(u => u.Id).ToImmutableArray(), target.Center));
        simulation.Step();
        var places = squad.ToDictionary(u => u.Id, u => u.Orders[^1].Point.ToCell());
        var tick = 1;
        for (; tick < MaxTicks && squad.Any(u => u.MoveTarget is not null || u.PathPending || u.Path is not null || u.HoldTicks > 0); tick++)
        {
            simulation.Step();
            if (tick % 50 == 0)
            {
                ReservationTests.AssertOccupancy(simulation.World);
            }
        }

        Assert.True(tick < MaxTicks, $"The squad still moves after {MaxTicks} ticks.");
        ReservationTests.AssertOccupancy(simulation.World);
        var distances = squad.Select(u => System.Math.Max(
            System.Math.Abs(u.Position.ToCell().X - places[u.Id].X), System.Math.Abs(u.Position.ToCell().Y - places[u.Id].Y))).ToList();
        return (tick, distances);
    }
}
