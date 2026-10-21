using System.Linq;
using Godot;
using Rts.Simulation.World;

namespace Rts.Client;

/// <summary>Debug panel: mode, tick, rates, entity count, last checksum, replay check and controls.</summary>
public partial class DebugOverlay : CanvasLayer
{
    private const string Controls =
        "LMB move all blue · Shift+LMB spawn · P pause · N step · +/- speed · F5 save replay · F9 play replay · R new session\n" +
        "Camera: screen edges · WASD/arrows · hold RMB and move the mouse · wheel zoom";

    private SimulationHost _host = null!;
    private Label _label = null!;

    public override void _Ready()
    {
        _host = GetNode<SimulationHost>("../SimulationHost");
        _label = GetNode<Label>("Label");
    }

    public override void _Process(double delta)
    {
        var simulation = _host.Simulation;
        var units = simulation.World.Entities.Count(e => e is TestUnit);
        var state = _host.Paused ? "PAUSED" : $"x{_host.SpeedMultiplier:0.##}";
        var mode = _host.IsPlayback ? "REPLAY" : "LIVE";

        _label.Text =
            $"{mode}  {state}\n" +
            $"tick {simulation.CurrentTick}   ticks/s {_host.MeasuredTicksPerSecond}   FPS {Engine.GetFramesPerSecond():0}\n" +
            $"units {units}   checksum {simulation.LastChecksum?.ToString() ?? "-"}\n" +
            (_host.ReplayStatus.Length > 0 ? $"replay: {_host.ReplayStatus}\n" : "") +
            Controls;
    }
}
