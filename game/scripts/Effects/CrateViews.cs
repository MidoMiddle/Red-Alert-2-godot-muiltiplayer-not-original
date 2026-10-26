using System.Collections.Generic;
using Godot;
using Rts.Simulation.Visibility;

namespace Rts.Client.Effects;

/// <summary>
/// The crates on the field (M11 a6): the supply crate model of the table of models (<c>crate/supply</c>) — a wooden box without it —
/// on the cell of every crate of the simulation the player has explored; gone once it is taken. Read from the world every frame.
/// </summary>
public partial class CrateViews : Node3D
{
    private const string ModelKey = "crate/supply";

    private static readonly Color Wood = new("#8A6A3E");

    private readonly Dictionary<int, Node3D> _views = new();
    private readonly HashSet<int> _seen = new();
    private SimulationHost? _host;

    public override void _Ready() => _host = GetNodeOrNull<SimulationHost>("../SimulationHost");

    public override void _Process(double delta)
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("CrateViews.process");
        if (_host is null)
        {
            return;
        }

        var world = _host.Simulation.World;
        _seen.Clear();
        foreach (var crate in world.CratesOnMap)
        {
            _seen.Add(crate.Id);
            if (!_views.TryGetValue(crate.Id, out var view))
            {
                view = Models.ModelLibrary.Instantiate(ModelKey, cell: new Vector2I(crate.Cell.X, crate.Cell.Y)) ?? Placeholder();
                view.Position = WorldCoords.ToGodotOnGround(crate.Cell.Center);
                AddChild(view);
                _views.Add(crate.Id, view);
            }

            view.Visible = Terrain.FogOfWar.Current is { Revealed: true }
                           || Vision.StateAt(world, SimulationHost.LocalPlayer, crate.Cell) != CellVisibility.Shroud;
        }

        foreach (var id in new List<int>(_views.Keys))
        {
            if (!_seen.Contains(id))
            {
                _views[id].QueueFree();
                _views.Remove(id);
            }
        }
    }

    // Without the model: a wooden box of half a cell.
    private static Node3D Placeholder()
    {
        var box = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.45f, 0.35f, 0.45f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = Wood },
            Position = new Vector3(0, 0.175f, 0),
        };
        var root = new Node3D();
        root.AddChild(box);
        return root;
    }
}
