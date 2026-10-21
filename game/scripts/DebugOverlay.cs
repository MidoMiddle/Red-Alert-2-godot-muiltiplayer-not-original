using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Controls;
using Rts.Client.Debug;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Client;

/// <summary>
/// Debug panel: mode, tick, rates, entity count, last checksum, replay check, the map cell under the pointer with its
/// speed for each locomotor, and the controls of the current scheme.
/// </summary>
public partial class DebugOverlay : CanvasLayer
{
    private const string ClassicControls = "Classic: LMB select / order · RMB click cancel · RMB drag camera";
    private const string ModernControls = "Modern: LMB select · RMB order · LMB empty ground cancel · MMB drag camera";
    private const string SharedControls =
        "Shift+LMB spawn · T spawn type · F3 path · Esc cancel or menu · P pause · N step · +/- speed · F5 save replay · F9 play replay · R new session · " +
        "WASD/arrows/edge/wheel camera";

    private SimulationHost _host = null!;
    private PlayerInput _input = null!;
    private PathPreview? _pathPreview;
    private Label _label = null!;
    private string _controls = string.Empty;

    public override void _Ready()
    {
        // Keeps updating while the pause menu stops the game, so the tick on screen stops with it.
        ProcessMode = ProcessModeEnum.Always;
        _host = GetNode<SimulationHost>("../SimulationHost");
        _input = GetNode<PlayerInput>("../PlayerInput");
        _label = GetNode<Label>("Label");
        var scheme = AppSettings.Instance?.Values.Scheme ?? ControlScheme.Classic;
        _controls = (scheme == ControlScheme.Classic ? ClassicControls : ModernControls) + "\n" + SharedControls;
    }

    public override void _Process(double delta)
    {
        // Main adds the path preview in its own _Ready, which runs after this one.
        _pathPreview ??= GetNodeOrNull<PathPreview>("../PathPreview");
        var simulation = _host.Simulation;
        var units = simulation.World.Entities.Count(e => e is Unit);
        var state = _host.Paused ? "PAUSED" : $"x{_host.SpeedMultiplier:0.##}";
        var mode = _host.IsPlayback ? "REPLAY" : "LIVE";

        _label.Text =
            $"{mode}  {state}\n" +
            $"tick {simulation.CurrentTick}   ticks/s {_host.MeasuredTicksPerSecond}   FPS {Engine.GetFramesPerSecond():0}\n" +
            $"units {units}   spawn {UnitArchetypes.Get(_host.SpawnType).Id}   checksum {simulation.LastChecksum?.ToString() ?? "-"}\n" +
            DescribeCell(simulation.World.Map, _input.CellUnderPointer) + "\n" +
            PathText.DescribeQueue(simulation.World.PathRequests.Requests.Count, simulation.World.PathRequests.ExpandedLastTick, simulation.World.PathRequests.Budget) + "\n" +
            (_pathPreview is null ? "" : _pathPreview.Description + "\n") +
            UnitStateText.Describe(SelectedUnit(simulation.World)) + "\n" +
            (_host.ReplayStatus.Length > 0 ? $"replay: {_host.ReplayStatus}\n" : "") +
            _controls;
    }

    /// <summary>The selected unit when exactly one is selected and alive.</summary>
    private Unit? SelectedUnit(GameWorld world) =>
        _input.Selection.Units.Count == 1 && world.TryGet(_input.Selection.Units[0], out var entity) && entity is Unit { IsAlive: true } unit
            ? unit
            : null;

    /// <summary><c>cell 12,30 Rough · foot 90 track 90 wheel 75</c>; a tree or impassable land shows as such.</summary>
    private static string DescribeCell(MapGrid map, CellPos? pointer)
    {
        if (pointer is not { } cell)
        {
            return "cell -";
        }

        var land = map.IsBlocked(cell) ? $"{map.LandAt(cell)} + tree" : map.LandAt(cell).ToString();
        var foot = map.SpeedPercent(cell, Locomotor.Foot);
        var track = map.SpeedPercent(cell, Locomotor.Track);
        var wheel = map.SpeedPercent(cell, Locomotor.Wheel);
        return foot == 0 && track == 0 && wheel == 0
            ? $"cell {cell.X},{cell.Y} {land} · impassable"
            : $"cell {cell.X},{cell.Y} {land} · foot {foot} track {track} wheel {wheel}";
    }
}
