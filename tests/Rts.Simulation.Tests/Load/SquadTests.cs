using Rts.Simulation.Benchmarks;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Tests.Client;
using Rts.Simulation.World;
using Xunit.Abstractions;

namespace Rts.Simulation.Tests.Load;

/// <summary>
/// The squad of the M2 definition of done (docs/implementation/M2_unit_control.md, section 7): 40 tanks and 20 infantry go
/// around the forest and stand as a blob at the target, and all of them pass a gap of one cell. Thresholds as measured
/// (see the journal): around the forest 90 % stand at tick 860 and the last at 947, through the gap 607 and 720; at least 56
/// of 60 places taken, nobody farther than a cell from one.
/// </summary>
public sealed class SquadTests(ITestOutputHelper output)
{
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

        var run = GroupRun.Order(simulation, squad, new CellPos(24, 4).Center);

        output.WriteLine($"around the forest: {run}");
        Assert.All(squad, unit => Assert.True(unit.Position.ToCell().Y < 9, $"{unit.Id} stayed south of the forest at {unit.Position.ToCell()}."));
        AssertSettled(run);
    }

    [Fact]
    public void AllSixtyPassTheOneCellGapInTheWall()
    {
        var (simulation, squad) = Start();

        var run = GroupRun.Order(simulation, squad, new CellPos(SquadMap.GapColumn, 46).Center);

        output.WriteLine($"through the gap: {run}");
        Assert.All(squad, unit => Assert.True(unit.Position.ToCell().Y > SquadMap.WallRow, $"{unit.Id} stayed before the wall at {unit.Position.ToCell()}."));
        AssertSettled(run);
    }

    private static void AssertSettled(GroupRun run)
    {
        Assert.True(run.PlacesTaken >= 56, $"Only {run.PlacesTaken} of 60 places taken.");
        Assert.True(run.FarthestOff <= 2, $"A unit stands {run.FarthestOff} cells from any place.");
        // No long shuffle at the target: the last unit stands soon after most of the group (before the place swaps: 1.55x).
        Assert.True(run.TickAll * 4 <= run.Tick90 * 5, $"The last unit stood at tick {run.TickAll}, 90 % at {run.Tick90}.");
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
}
