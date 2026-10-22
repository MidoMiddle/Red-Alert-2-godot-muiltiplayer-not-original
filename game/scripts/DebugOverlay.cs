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
        "Drag LMB box select · Shift+click add/remove · double click same type · Ctrl+1…9 group · 1…9 select (twice: camera) · Shift+1…9 add · " +
        "Shift+order queue · Ctrl+Shift+order attack-move · Z+orders waypoints, then order without Z · S stop · X scatter\n" +
        "K damage map object, bridge or building (debug) · Alt+LMB spawn · B spawn (again: type) · T same type (twice: map) · E select all · Esc cancel or menu · P pause · N step · +/- speed · F5 save replay · F9 play replay · R new session · " +
        "arrows/edge/wheel camera · F1…F4 bookmark (Ctrl: save)\n" +
        "Debug: F6 paths of the selected · F7 cells (passability / reservations / buckets) · F8 path queue graph";

    private SimulationHost _host = null!;
    private PlayerInput _input = null!;
    private PathPreview? _pathPreview;
    private CellOverlay? _cellOverlay;
    private readonly QueueGraph _queueGraph = new() { Name = "QueueGraph" };
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
        _queueGraph.Bind(_host);
        AddChild(_queueGraph);
    }

    public override void _Process(double delta)
    {
        // Main adds the path preview in its own _Ready, which runs after this one.
        _pathPreview ??= GetNodeOrNull<PathPreview>("../PathPreview");
        _cellOverlay ??= GetNodeOrNull<CellOverlay>("../CellOverlay");
        // The graph sits under the text, which grows and shrinks with the lines.
        _queueGraph.Position = new Vector2(_label.Position.X, _label.Position.Y + _label.Size.Y + 6f);
        var simulation = _host.Simulation;
        var units = simulation.World.Entities.Count(e => e is Unit);
        var state = _host.Paused ? "PAUSED" : $"x{_host.SpeedMultiplier:0.##}";
        var mode = _host.IsPlayback ? "REPLAY" : "LIVE";

        _label.Text =
            $"{mode}  {state}\n" +
            $"tick {simulation.CurrentTick}   ticks/s {_host.MeasuredTicksPerSecond}   FPS {Engine.GetFramesPerSecond():0}\n" +
            $"units {units}   spawn {UnitTypes.IdOf(_host.SpawnType)}   checksum {simulation.LastChecksum?.ToString() ?? "-"}\n" +
            DescribePlayer(simulation.World.Player(SimulationHost.LocalPlayer)) + "\n" +
            (_input.BuildArmed ? $"build {_host.BuildId}: click to place, V next type, Esc stop\n" : "") +
            (_host.Notice.Length > 0 ? $"base: {_host.Notice}\n" : "") +
            DescribeCell(simulation.World, _input.CellUnderPointer) + "\n" +
            PathText.DescribeQueue(simulation.World.PathRequests.Requests.Count, simulation.World.PathRequests.ExpandedLastTick, simulation.World.PathRequests.Budget) + "\n" +
            (_pathPreview is null ? "" : _pathPreview.Description + "\n") +
            (_cellOverlay is null ? "" : _cellOverlay.Description + "\n") +
            $"selected {_input.Selection.Units.Count}   " + UnitStateText.Describe(SelectedUnit(simulation.World)) +
            (SelectedUnit(simulation.World) is { } selected ? " · " + UnitStateText.DescribeSight(selected, simulation.World.Map) : "") + "\n" +
            (_host.ReplayStatus.Length > 0 ? $"replay: {_host.ReplayStatus}\n" : "") +
            _controls;
    }

    /// <summary>The selected unit when exactly one is selected and alive.</summary>
    private Unit? SelectedUnit(GameWorld world) =>
        _input.Selection.Units.Count == 1 && world.TryGet(_input.Selection.Units[0], out var entity) && entity is Unit { IsAlive: true } unit
            ? unit
            : null;

    /// <summary><c>cell 12,30 Rough · foot 90 track 90 wheel 75</c>; a tree or impassable land shows as such.</summary>
    /// <summary>The economy of a player (M3 step 5): "P1 credits 10000 · power 150/70 · tech 3 buildings, 0 units", "LOW POWER" in a deficit.</summary>
    private static string DescribePlayer(PlayerState player)
    {
        var (units, buildings) = player.AvailableCount;
        return $"{player.Id} credits {player.Credits} · power {player.PowerOutput}/{player.PowerDrain}" +
               (player.LowPower ? " LOW POWER" : "") + $" · tech {buildings} buildings, {units} units";
    }

    private static string DescribeCell(GameWorld world, CellPos? pointer)
    {
        var map = world.Map;
        if (pointer is not { } cell)
        {
            return "cell -";
        }

        var land = map.LandAt(cell).ToString();
        foreach (var mapObject in map.ObjectsAt(cell))
        {
            // The map object and its hit points (M7 step 5): "pine_tree 200/400", "stump", "boulder".
            land += mapObject switch
            {
                { Destroyed: true, Kind: MapObjectKind.PineTree } => " + stump",
                { Destroyed: true } => "",
                { Type.Destructible: false } => $" + {mapObject.Type.Id}",
                _ => $" + {mapObject.Type.Id} {mapObject.Hp}/{mapObject.Type.MaxHp}",
            };
        }

        // A bridge segment over the water (M7 step 6): "Deep + bridge_wood_low 400/600", "Deep + broken bridge".
        land += map.BridgeAt(cell) switch
        {
            { IsIntact: true } segment => $" + {segment.Bridge.Type.Id} {segment.Hp}/{segment.Bridge.Type.SegmentHp}",
            { } => " + broken bridge",
            null => "",
        };

        // A building on the cell (M7 step 7): "neutral_house_a 600/800 (Neutral)".
        if (world.BuildingAt(cell) is { } building)
        {
            var (id, maxHp) = building.Def is { } def ? (def.Id, def.Hp) : (building.Type.Id, building.Type.MaxHp);
            land += $" + {id} {building.Hp}/{maxHp} ({building.Owner})" + (building.IsReady ? "" : $" growing {building.BuildUpPermille / 10}%");
        }
        var foot = map.SpeedPercent(cell, Locomotor.Foot);
        var track = map.SpeedPercent(cell, Locomotor.Track);
        var wheel = map.SpeedPercent(cell, Locomotor.Wheel);
        return foot == 0 && track == 0 && wheel == 0
            ? $"cell {cell.X},{cell.Y} {land} · impassable"
            : $"cell {cell.X},{cell.Y} {land} · foot {foot} track {track} wheel {wheel}";
    }
}
