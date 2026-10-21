using BenchmarkDotNet.Attributes;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.World;

namespace Rts.Simulation.Benchmarks;

/// <summary>The benchmarks of docs/implementation/M2_unit_control.md, 4.15, on the load map.</summary>
[MemoryDiagnoser]
public class SimulationBenchmarks
{
    private const int MovingTicks = 100;

    private TestMap _map = null!;
    private GameSimulation _idle = null!;
    private GameSimulation _moving = null!;
    private Pathfinder _pathfinder = null!;
    private List<EntityId> _found = null!;
    private WorldPos[] _queries = null!;

    [GlobalSetup]
    public void Setup()
    {
        _map = LoadScenario.LoadMap(LoadScenario.RepoRoot(AppContext.BaseDirectory));
        _idle = LoadScenario.Start(_map);
        _pathfinder = new Pathfinder(_map.Grid);
        _found = new List<EntityId>();
        var random = new System.Random(15);
        _queries = Enumerable.Range(0, 1000)
            .Select(_ => new WorldPos(random.Next(LoadMapGenerator.Size * 256), random.Next(LoadMapGenerator.Size * 256)))
            .ToArray();
    }

    /// <summary>A fresh order across the map before each run of <see cref="Tick_300Units_Moving"/>.</summary>
    [IterationSetup(Target = nameof(Tick_300Units_Moving))]
    public void OrderAcross()
    {
        _moving = LoadScenario.Start(_map);
        LoadScenario.OrderAcrossTheMap(_moving);
    }

    /// <summary>The first 100 ticks after the order across the map (order tick, path searches, the crowd leaving).</summary>
    [Benchmark(OperationsPerInvoke = MovingTicks)]
    public void Tick_300Units_Moving()
    {
        for (var i = 0; i < MovingTicks; i++)
        {
            _moving.Step();
        }
    }

    [Benchmark]
    public void Tick_300Units_Idle() => _idle.Step();

    [Benchmark]
    public CellPath AStar_Maze128() => _pathfinder.FindPath(LoadScenario.MazeStart, LoadScenario.MazeGoal, Locomotor.Foot);

    [Benchmark(OperationsPerInvoke = 1000)]
    public int SpatialIndex_Query()
    {
        var world = _idle.World;
        var total = 0;
        foreach (var point in _queries)
        {
            world.Spatial.QueryRadius(world, point, 6 * WorldPos.LeptonsPerCell, _found);
            total += _found.Count;
        }

        return total;
    }

    [Benchmark(OperationsPerInvoke = 1024)]
    public int Sin_Atan2()
    {
        var sum = 0;
        for (var i = 0; i < 1024; i++)
        {
            sum += (int)FixedMath.Sin(new BAngle((ushort)(i * 64))).Raw;
            sum += FixedMath.Angle(i - 512, 300).Value;
        }

        return sum;
    }

    [Benchmark]
    public ulong StateHash_128() => StateHasher.Compute(_idle.World);
}
