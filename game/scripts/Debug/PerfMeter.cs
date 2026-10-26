using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Rts.Client.Debug;

/// <summary>What the client sees of the game for the line of a second of the performance log, beside the times the meter adds up.</summary>
public readonly record struct PerfSample(
    double Seconds, int Tick, double ProcessMs, int Units, int Projectiles, int Buildings, int PathQueue, double DrawnObjects, double DrawCalls,
    double Primitives, double StaticMemoryMb, double ManagedMemoryMb, int Gen0, int Gen1, int Gen2);

/// <summary>
/// The sums of the performance log (the owner, 2026-10-26: a line a second into a file, to see what grows while a match heats the
/// computer): the frames — how many, their average and longest time; the ticks of the simulation — how many, their average and longest
/// time; the time of every phase of a tick (<c>GameSimulation.Probe</c>), the turn of every bot among them (<c>bot 1</c> …); the nodes
/// the pathfinder looked at; the time of every part of the client's frames (<c>client_&lt;node&gt;.process</c>, <c>.draw</c>) in the
/// second. No Godot here: the client feeds it, a test checks the sums and the lines. The columns of the phases are
/// those seen before the first line, in the order they came.
/// </summary>
public sealed class PerfMeter
{
    private static readonly string[] Fixed =
    [
        "seconds", "tick", "fps", "frame_ms_avg", "frame_ms_max", "process_ms", "ticks", "tick_ms_avg", "tick_ms_max", "sim_ms", "bots_ms",
        "path_expanded", "path_queue", "units", "projectiles", "buildings", "drawn_objects", "draw_calls", "primitives",
        "static_mb", "managed_mb", "gc0", "gc1", "gc2",
    ];

    private readonly List<string> _parts = new();
    private readonly Dictionary<string, int> _partIndex = new(StringComparer.Ordinal);
    private double[] _partMs = new double[32];
    private readonly List<string> _phases = new();
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);
    private double[] _phaseMs = new double[16];
    private int _frames;
    private double _frameMs;
    private double _frameMaxMs;
    private int _ticks;
    private double _tickMs;
    private double _tickMaxMs;
    private double _thisTickMs;
    private long _expanded;
    private bool _headed;

    /// <summary>A frame of <paramref name="seconds"/>.</summary>
    public void Frame(double seconds)
    {
        var ms = seconds * 1000;
        _frames++;
        _frameMs += ms;
        _frameMaxMs = Math.Max(_frameMaxMs, ms);
    }

    /// <summary>A phase of the tick that took <paramref name="ms"/>.</summary>
    public void Phase(string name, double ms)
    {
        if (!_index.TryGetValue(name, out var index))
        {
            if (_headed)
            {
                // A phase new after the header (a bot that came later): counted in the tick, not given a column of its own.
                _thisTickMs += ms;
                return;
            }

            index = _phases.Count;
            _index[name] = index;
            _phases.Add(name);
            if (index >= _phaseMs.Length)
            {
                Array.Resize(ref _phaseMs, _phaseMs.Length * 2);
            }
        }

        _phaseMs[index] += ms;
        _thisTickMs += ms;
    }

    /// <summary>
    /// A part of the client's frame that took <paramref name="ms"/>: the work of a node in its <c>_Process</c> or <c>_Draw</c>. A part
    /// first seen after the header has no column.
    /// </summary>
    public void Part(string name, double ms)
    {
        if (!_partIndex.TryGetValue(name, out var index))
        {
            if (_headed)
            {
                return;
            }

            index = _parts.Count;
            _partIndex[name] = index;
            _parts.Add(name);
            if (index >= _partMs.Length)
            {
                Array.Resize(ref _partMs, _partMs.Length * 2);
            }
        }

        _partMs[index] += ms;
    }

    /// <summary>The end of a tick: its phases add up to its time; the pathfinder looked at <paramref name="expanded"/> nodes in it.</summary>
    public void EndTick(int expanded)
    {
        _ticks++;
        _tickMs += _thisTickMs;
        _tickMaxMs = Math.Max(_tickMaxMs, _thisTickMs);
        _thisTickMs = 0;
        _expanded += expanded;
    }

    /// <summary>The header of the file: the fixed columns, then one a phase seen so far, <c>ms_</c> and its name.</summary>
    public string Header()
    {
        _headed = true;
        var header = new StringBuilder(string.Join(',', Fixed));
        foreach (var phase in _phases)
        {
            header.Append(",ms_").Append(phase.Replace(' ', '_'));
        }

        foreach (var part in _parts)
        {
            header.Append(",client_").Append(part);
        }

        return header.ToString();
    }

    /// <summary>The line of the second that ends now, with what the client saw then; the sums start again.</summary>
    public string Line(PerfSample sample)
    {
        var bots = 0.0;
        for (var i = 0; i < _phases.Count; i++)
        {
            bots += _phases[i].StartsWith("bot ", StringComparison.Ordinal) ? _phaseMs[i] : 0;
        }

        var c = CultureInfo.InvariantCulture;
        var line = new StringBuilder();
        line.Append(c, $"{sample.Seconds:0.0},{sample.Tick},{_frames},{Average(_frameMs, _frames):0.00},{_frameMaxMs:0.00},{sample.ProcessMs:0.00},");
        line.Append(c, $"{_ticks},{Average(_tickMs, _ticks):0.00},{_tickMaxMs:0.00},{_tickMs:0.00},{bots:0.00},{_expanded},{sample.PathQueue},");
        line.Append(c, $"{sample.Units},{sample.Projectiles},{sample.Buildings},{sample.DrawnObjects:0},{sample.DrawCalls:0},{sample.Primitives:0},");
        line.Append(c, $"{sample.StaticMemoryMb:0.0},{sample.ManagedMemoryMb:0.0},{sample.Gen0},{sample.Gen1},{sample.Gen2}");
        for (var i = 0; i < _phases.Count; i++)
        {
            line.Append(c, $",{_phaseMs[i]:0.00}");
        }

        for (var i = 0; i < _parts.Count; i++)
        {
            line.Append(c, $",{_partMs[i]:0.00}");
        }

        Array.Clear(_phaseMs);
        Array.Clear(_partMs);
        (_frames, _frameMs, _frameMaxMs, _ticks, _tickMs, _tickMaxMs, _expanded) = (0, 0, 0, 0, 0, 0, 0);
        return line.ToString();
    }

    private static double Average(double sum, int count) => count == 0 ? 0 : sum / count;
}
