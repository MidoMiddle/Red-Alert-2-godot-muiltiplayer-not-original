using BenchmarkDotNet.Running;
using Rts.Simulation.Benchmarks;

// dotnet run -c Release --project src/Rts.Simulation.Benchmarks -- --load        stopwatch run of the load scenario
// dotnet run -c Release --project src/Rts.Simulation.Benchmarks -- --phases      the same, timed phase by phase
// dotnet run -c Release --project src/Rts.Simulation.Benchmarks -- --write-map   regenerate game/maps/test/load128.txt and squad.txt
// dotnet run -c Release --project src/Rts.Simulation.Benchmarks -- --filter '*'  BenchmarkDotNet
var root = LoadScenario.RepoRoot(Directory.GetCurrentDirectory());
if (args.Contains("--write-map"))
{
    var path = Path.Combine(root, LoadScenario.MapPath);
    File.WriteAllText(path, LoadMapGenerator.Generate());
    Console.WriteLine($"wrote {path}");
    var squad = Path.Combine(root, SquadMap.MapPath);
    File.WriteAllText(squad, SquadMap.Generate(File.ReadAllText(Path.Combine(root, SquadMap.SandboxPath))));
    Console.WriteLine($"wrote {squad}");
}
else if (args.Contains("--phases"))
{
    PhaseRun.Run(root);
}
else if (args.Contains("--load"))
{
    LoadRun.Run(root);
}
else
{
    BenchmarkSwitcher.FromAssembly(typeof(SimulationBenchmarks).Assembly).Run(args);
}
