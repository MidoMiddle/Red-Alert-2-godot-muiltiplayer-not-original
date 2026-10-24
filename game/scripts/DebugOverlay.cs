using System.Collections.Generic;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Controls;
using Rts.Client.Debug;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Rules;
using Rts.Simulation.Visibility;
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
        "Debug: F6 paths of the selected · F7 cells (passability / reservations / buckets) · F8 path queue graph · F10 fog revealed · F12 AI";

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
            DescribeQueues(simulation.World, simulation.World.Player(SimulationHost.LocalPlayer)) +
            DescribeTool() +
            (_host.Notice.Length > 0 ? $"base: {_host.Notice}\n" : "") +
            DescribeCell(simulation.World, _input.CellUnderPointer) + DescribeVision(simulation.World, _input.CellUnderPointer)
            + $" · cursor {CursorSet.Shown?.ToString() ?? "-"}\n" +
            PathText.DescribeQueue(simulation.World.PathRequests.Requests.Count, simulation.World.PathRequests.ExpandedLastTick, simulation.World.PathRequests.Budget) + "\n" +
            (_pathPreview is null ? "" : _pathPreview.Description + "\n") +
            (_cellOverlay is null ? "" : _cellOverlay.Description + "\n") +
            $"selected {_input.Selection.Units.Count}   " + UnitStateText.Describe(SelectedUnit(simulation.World)) +
            (SelectedUnit(simulation.World) is { Harvester: { } harvester } carrier
                ? $" · harvester {harvester.Mode} {harvester.Load}/{carrier.Archetype.HarvestCapacity}" : "") +
            (SelectedUnit(simulation.World) is { } selected ? " · " + UnitStateText.DescribeSight(selected, simulation.World.Map) : "") +
            (SelectedUnit(simulation.World) is { Weapons.Length: > 0 } armed ? " · " + UnitStateText.DescribeFight(armed) : "") + "\n" +
            (_host.ReplayStatus.Length > 0 ? $"replay: {_host.ReplayStatus}\n" : "") +
            _controls;
    }

    /// <summary>
    /// <c> · visible</c>, <c> · fog</c> or <c> · shroud</c>: what the local player knows of the cell (M4 step 11); <c> · fog revealed</c>
    /// while F10 shows the whole map (step 12).
    /// </summary>
    private static string DescribeVision(GameWorld world, CellPos? pointer) =>
        (pointer is { } cell ? " · " + Vision.StateAt(world, SimulationHost.LocalPlayer, cell).ToString().ToLowerInvariant() : "")
        + (Terrain.FogOfWar.Current is { Revealed: true } ? " · fog revealed" : "");

    /// <summary>The selected unit when exactly one is selected and alive.</summary>
    private Unit? SelectedUnit(GameWorld world) =>
        _input.Selection.Units.Count == 1 && world.TryGet(_input.Selection.Units[0], out var entity) && entity is Unit { IsAlive: true } unit
            ? unit
            : null;

    /// <summary><c>cell 12,30 Rough · foot 90 track 90 wheel 75</c>; a tree or impassable land shows as such.</summary>
    /// <summary>The economy of a player (M3 step 5): "P1 credits 10000 · power 150/70 · tech 3 buildings, 0 units", "LOW POWER" in a deficit.</summary>
    /// <summary>The armed tool of the pointer and how to leave it, or nothing.</summary>
    private string DescribeTool() => _input.Tool switch
    {
        PointerTool.Build when _host.DebugBuild => $"build {_host.BuildId}: click to place, V next type, Esc stop\n",
        PointerTool.Build => $"place {_host.BuildId}: click to place, right click or Esc stop\n",
        PointerTool.Sell => "sell mode: click a building, right click or Esc stop\n",
        PointerTool.Repair => "repair mode: click a building, right click or Esc stop\n",
        _ => "",
    };

    /// <summary>
    /// The production queues of a player (M3 step 8), the busy ones only: "queues: building reactor 45% · infantry conscript 20% +2",
    /// with "paused", "waits for money" or "ready"; empty when nothing is produced.
    /// </summary>
    private static string DescribeQueues(GameWorld world, PlayerState player)
    {
        var rules = world.Rules.Database;
        var parts = new List<string>();
        for (var category = ProductionCategory.Infantry; category <= ProductionCategories.Last; category++)
        {
            var queue = player.Queue(category);
            if (queue.Count == 0)
            {
                continue;
            }

            var item = queue[0];
            var state = item.Ready ? "ready"
                : item.Paused ? "paused"
                : item.WaitingForFunds ? "waits for money"
                : $"{item.Progress / (ProductionRules.TotalProgress(rules, item.Kind, item.DefIndex) / 100)}%";
            parts.Add($"{category.ToString().ToLowerInvariant()} {ProductionRules.IdOf(rules, item.Kind, item.DefIndex)} {state}" +
                      (queue.Count > 1 ? $" +{queue.Count - 1}" : ""));
        }

        return parts.Count == 0 ? "" : $"queues: {string.Join(" · ", parts)}\n";
    }

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
            if (building.Repairing)
            {
                land += " · repairing";
            }

            if (building.Def?.DockCell is not null)
            {
                land += Rts.Simulation.Systems.HarvesterSystem.DockFree(world, building) ? " · dock free" : " · dock busy";
            }
        }

        // Ore (M3 step 9): "ore 7 · 200 cr", "gems 3 · 200 cr".
        if (map.OreAt(cell) is { IsEmpty: false } ore)
        {
            land += $" + {(ore.Kind == OreKind.Gems ? "gems" : "ore")} {ore.Stage} · {world.Rules.CreditsOf(ore)} cr";
        }

        var foot = map.SpeedPercent(cell, Locomotor.Foot);
        var track = map.SpeedPercent(cell, Locomotor.Track);
        var wheel = map.SpeedPercent(cell, Locomotor.Wheel);
        return foot == 0 && track == 0 && wheel == 0
            ? $"cell {cell.X},{cell.Y} {land} · impassable"
            : $"cell {cell.X},{cell.Y} {land} · foot {foot} track {track} wheel {wheel}";
    }
}
