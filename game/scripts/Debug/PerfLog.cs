using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Godot;
using Rts.Simulation;
using Rts.Simulation.World;

namespace Rts.Client.Debug;

/// <summary>
/// The performance log (the owner, 2026-10-26): while a simulation runs, a line a second goes into
/// <c>user://logs/perf/&lt;time&gt;-&lt;what&gt;.csv</c> — the frames and the time the scripts took, the ticks of the simulation and the
/// time of each of its phases and of each bot (<see cref="GameSimulation.Probe"/>), the nodes the pathfinder looked at and the paths
/// waiting, the units, shots and buildings, what the renderer drew, the memory. The sums are <see cref="PerfMeter"/>'s. On by default;
/// <c>--no-perf-log</c> turns it off. The newest <see cref="KeptFiles"/> files are kept. Measuring only: nothing in the match changes.
/// </summary>
public sealed class PerfLog : IDisposable
{
    /// <summary>How many logs are kept; the older go when a new one starts.</summary>
    public const int KeptFiles = 10;

    private readonly PerfMeter _meter = new();
    private readonly StreamWriter _file;
    private long _lap;
    private double _secondTimer;
    private double _seconds;
    private bool _headed;

    private PerfLog(GameSimulation simulation, string path)
    {
        Simulation = simulation;
        Path = path;
        _file = new StreamWriter(path) { AutoFlush = true };
        simulation.Probe = Phase;
    }

    /// <summary>The simulation measured.</summary>
    public GameSimulation Simulation { get; }

    /// <summary>The file of the log.</summary>
    public string Path { get; }

    /// <summary>Whether the log is on: not with <c>--no-perf-log</c>.</summary>
    public static bool Enabled => Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-perf-log") < 0;

    /// <summary>A log of <paramref name="simulation"/>, named by <paramref name="what"/> (a map, «sandbox», «replay»); null when off.</summary>
    public static PerfLog? Start(GameSimulation simulation, string what)
    {
        if (!Enabled)
        {
            return null;
        }

        var folder = ProjectSettings.GlobalizePath("user://logs/perf");
        Directory.CreateDirectory(folder);
        foreach (var old in Directory.GetFiles(folder, "*.csv").OrderByDescending(file => file, StringComparer.Ordinal).Skip(KeptFiles - 1))
        {
            File.Delete(old);
        }

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var log = new PerfLog(simulation, System.IO.Path.Combine(folder, $"{stamp}-{what}.csv"));
        GD.Print($"[perf] the performance log in {log.Path}; frames a second at most {(Engine.MaxFps == 0 ? "unlimited" : Engine.MaxFps)}, vsync {DisplayServer.WindowGetVsyncMode()}");
        return log;
    }

    /// <summary>A tick starts: its first phase is timed from now.</summary>
    public void BeginTick() => _lap = Stopwatch.GetTimestamp();

    /// <summary>A tick is over.</summary>
    public void EndTick() => _meter.EndTick(Simulation.World.PathRequests.ExpandedLastTick);

    /// <summary>A frame of <paramref name="delta"/> seconds; once a second the line goes into the file.</summary>
    public void Frame(double delta)
    {
        _meter.Frame(delta);
        _seconds += delta;
        _secondTimer += delta;
        if (_secondTimer < 1.0)
        {
            return;
        }

        _secondTimer -= 1.0;
        if (!_headed)
        {
            _file.WriteLine(_meter.Header());
            _headed = true;
        }

        _file.WriteLine(_meter.Line(Sample()));
    }

    public void Dispose()
    {
        if (Simulation.Probe == Phase)
        {
            Simulation.Probe = null;
        }

        _file.Dispose();
    }

    private void Phase(string name)
    {
        var now = Stopwatch.GetTimestamp();
        _meter.Phase(name, Stopwatch.GetElapsedTime(_lap, now).TotalMilliseconds);
        _lap = now;
    }

    private PerfSample Sample()
    {
        var world = Simulation.World;
        var (units, projectiles, buildings) = (0, 0, 0);
        foreach (var entity in world.Entities)
        {
            switch (entity)
            {
                case Unit { IsAlive: true }:
                    units++;
                    break;
                case Projectile:
                    projectiles++;
                    break;
                case Building { IsAlive: true }:
                    buildings++;
                    break;
            }
        }

        const double mb = 1024 * 1024;
        return new PerfSample(
            _seconds, world.CurrentTick, Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000, units, projectiles, buildings,
            world.PathRequests.Requests.Count,
            Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame),
            Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
            Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
            Performance.GetMonitor(Performance.Monitor.MemoryStatic) / mb,
            GC.GetTotalMemory(false) / mb,
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
    }
}
