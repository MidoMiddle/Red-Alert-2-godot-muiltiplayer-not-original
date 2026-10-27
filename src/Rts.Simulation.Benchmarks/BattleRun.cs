using System.Diagnostics;
using Rts.Data;
using Rts.Simulation.World;

namespace Rts.Simulation.Benchmarks;

/// <summary>
/// <c>--battle</c>: the general battle of <see cref="BattleScenario"/> with a stopwatch around every tick (M4 step 15): the mean, the
/// 99th percentile and the peak of the whole fight, the same while both armies still have more than half their units, and the
/// memory allocated per tick. Warmed up first, as <see cref="LoadRun"/>.
/// </summary>
public static class BattleRun
{
    public const int WarmUpRounds = 3;

    public static void Run(string repoRoot)
    {
        var rules = GameData.LoadDirectory(Path.Combine(repoRoot, "game", GameData.Folder)).Rules;
        var map = LoadScenario.LoadMap(repoRoot, rules).Grid;
        for (var round = 0; round < WarmUpRounds; round++)
        {
            Measure(map, rules, 600, print: false);
        }

        Measure(map, rules, BattleScenario.Ticks, print: true);
    }

    private static void Measure(Rts.Simulation.Map.MapGrid map, Rts.Simulation.Rules.GameRules rules, int ticks, bool print)
    {
        var simulation = BattleScenario.Start(map.Clone(), rules, hashInterval: 15, BattleBases.Build);
        BattleScenario.Charge(simulation);
        var times = new double[ticks];
        var heavy = new List<double>();
        var bytes = new long[ticks];
        for (var i = 0; i < ticks; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            simulation.Step();
            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            bytes[i] = GC.GetAllocatedBytesForCurrentThread() - before;
            if (Army(simulation, BattleScenario.Blue) > BattleScenario.ArmySize / 2 && Army(simulation, BattleScenario.Red) > BattleScenario.ArmySize / 2)
            {
                heavy.Add(times[i]);
            }
        }

        if (!print)
        {
            return;
        }

        Console.WriteLine($"battle: {BattleScenario.ArmySize} against {BattleScenario.ArmySize} on {map.Width}x{map.Height}, {ticks} ticks");
        Print("whole fight", times);
        Print("both armies above half", heavy.ToArray());
        Console.WriteLine($"  left: blue {Army(simulation, BattleScenario.Blue)}, red {Army(simulation, BattleScenario.Red)} units");
        var steady = bytes.Skip(100).ToArray();
        Console.WriteLine($"  allocated per tick after tick 100: mean {steady.Average():0} B, peak {steady.Max()} B, ticks with any {steady.Count(b => b > 0)} of {steady.Length}");
    }

    private static void Print(string name, double[] times)
    {
        if (times.Length == 0)
        {
            Console.WriteLine($"  {name}: no ticks");
            return;
        }

        var sorted = times.OrderBy(t => t).ToArray();
        Console.WriteLine($"  {name} ({times.Length} ticks): mean {times.Average():0.00} ms, p99 {sorted[(int)(sorted.Length * 0.99)]:0.00} ms, peak {sorted[^1]:0.00} ms");
    }

    private static int Army(GameSimulation simulation, PlayerId player) =>
        simulation.World.Entities.Count(e => e is Unit { IsAlive: true } && e.Owner == player);
}
