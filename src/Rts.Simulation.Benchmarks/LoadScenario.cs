using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Benchmarks;

/// <summary>
/// The load scenario of docs/implementation/M2_unit_control.md, 4.15, on game/maps/test/load128.txt: 300 units spawn on
/// tick 0; "idle" lets them stand, "across the map" sends four groups of 75 (five rows each) to the east third on tick 1.
/// Shared by the benchmarks and the load test of Rts.Simulation.Tests.
/// </summary>
public static class LoadScenario
{
    public const string MapPath = "game/maps/test/load128.txt";
    public const ulong Seed = 0x4C4F4144;
    public const int GroupSize = 75;

    /// <summary>The owner of every unit of the load map.</summary>
    public static readonly PlayerId Player = new(1);

    /// <summary>Where the four groups go: the east third, past the rock ridge, one north of the lake.</summary>
    public static readonly ImmutableArray<WorldPos> Targets =
    [
        new CellPos(100, 50).Center,
        new CellPos(104, 66).Center,
        new CellPos(100, 82).Center,
        new CellPos(96, 96).Center,
    ];

    /// <summary>A* across the whole map into the maze: from the west edge to the far corner of the maze.</summary>
    public static readonly CellPos MazeStart = new(1, 23);
    public static readonly CellPos MazeGoal = new(
        LoadMapGenerator.MazeLeft + LoadMapGenerator.MazeCells * 2 - 1, LoadMapGenerator.MazeTop + 1);

    /// <summary>Walks up from <paramref name="start"/> to the folder of RtsGame.sln.</summary>
    public static string RepoRoot(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RtsGame.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException($"No RtsGame.sln above {start}.");
    }

    public static TestMap LoadMap(string repoRoot) => TestMapParser.Parse(File.ReadAllText(Path.Combine(repoRoot, MapPath)));

    /// <summary>A new match on the load map with its 300 units spawned (tick 0 done).</summary>
    public static GameSimulation Start(TestMap map)
    {
        var simulation = new GameSimulation(new SimulationSettings(Seed, map.Grid));
        uint sequence = 0;
        foreach (var unit in map.Units)
        {
            simulation.Enqueue(new SpawnTestUnitCommand(unit.Owner, 0, sequence++, unit.Type, unit.Cell.Center));
        }

        simulation.Step();
        return simulation;
    }

    /// <summary>Orders the four groups across the map on the next tick. Units are taken in id order, 75 per group.</summary>
    public static void OrderAcrossTheMap(GameSimulation simulation)
    {
        foreach (var order in Orders(simulation))
        {
            simulation.Enqueue(order);
        }
    }

    /// <summary>The four group orders across the map for the next tick of <paramref name="simulation"/>.</summary>
    public static List<MoveCommand> Orders(GameSimulation simulation)
    {
        var units = simulation.World.Entities.OfType<Unit>().Where(u => u.IsAlive).Select(u => u.Id).ToList();
        var orders = new List<MoveCommand>();
        for (var group = 0; group < Targets.Length; group++)
        {
            var members = units.Skip(group * GroupSize).Take(GroupSize).ToImmutableArray();
            if (!members.IsEmpty)
            {
                orders.Add(new MoveCommand(Player, simulation.CurrentTick, (uint)(1000 + group), members, Targets[group]));
            }
        }

        return orders;
    }
}
