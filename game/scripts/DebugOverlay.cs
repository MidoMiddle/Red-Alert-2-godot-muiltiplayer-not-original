using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Controls;
using Rts.Simulation.World;

namespace Rts.Client;

/// <summary>Debug panel: mode, tick, rates, entity count, last checksum, replay check and the controls of the current scheme.</summary>
public partial class DebugOverlay : CanvasLayer
{
    private const string ClassicControls = "Classic: LMB select / order · RMB click cancel · RMB drag camera";
    private const string ModernControls = "Modern: LMB select · RMB order · LMB empty ground cancel · MMB drag camera";
    private const string SharedControls =
        "Shift+LMB spawn · T spawn type · Esc cancel or menu · P pause · N step · +/- speed · F5 save replay · F9 play replay · R new session · " +
        "WASD/arrows/edge/wheel camera";

    private SimulationHost _host = null!;
    private Label _label = null!;
    private string _controls = string.Empty;

    public override void _Ready()
    {
        // Keeps updating while the pause menu stops the game, so the tick on screen stops with it.
        ProcessMode = ProcessModeEnum.Always;
        _host = GetNode<SimulationHost>("../SimulationHost");
        _label = GetNode<Label>("Label");
        var scheme = AppSettings.Instance?.Values.Scheme ?? ControlScheme.Classic;
        _controls = (scheme == ControlScheme.Classic ? ClassicControls : ModernControls) + "\n" + SharedControls;
    }

    public override void _Process(double delta)
    {
        var simulation = _host.Simulation;
        var units = simulation.World.Entities.Count(e => e is Unit);
        var state = _host.Paused ? "PAUSED" : $"x{_host.SpeedMultiplier:0.##}";
        var mode = _host.IsPlayback ? "REPLAY" : "LIVE";

        _label.Text =
            $"{mode}  {state}\n" +
            $"tick {simulation.CurrentTick}   ticks/s {_host.MeasuredTicksPerSecond}   FPS {Engine.GetFramesPerSecond():0}\n" +
            $"units {units}   spawn {UnitArchetypes.Get(_host.SpawnType).Id}   checksum {simulation.LastChecksum?.ToString() ?? "-"}\n" +
            (_host.ReplayStatus.Length > 0 ? $"replay: {_host.ReplayStatus}\n" : "") +
            _controls;
    }
}
