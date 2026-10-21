using System.Collections.Generic;
using Godot;
using Rts.Simulation.World;

namespace Rts.Client;

/// <summary>
/// Draws every test unit as a cube, interpolated between the previous and the current tick so that
/// 15 ticks per second still look smooth at 60+ frames per second. Views are pooled. Selected units glow.
/// </summary>
public partial class UnitRenderer : Node3D
{
    private const float CubeSize = 0.5f;

    private readonly Dictionary<EntityId, MeshInstance3D> _views = new();
    private readonly Stack<MeshInstance3D> _pool = new();
    private readonly HashSet<EntityId> _alive = new();
    private readonly List<EntityId> _gone = new();
    private readonly Dictionary<(int Owner, bool Selected), StandardMaterial3D> _materials = new();
    private readonly BoxMesh _mesh = new() { Size = new Vector3(CubeSize, CubeSize, CubeSize) };

    private SimulationHost _host = null!;
    private PlayerInput _input = null!;

    public override void _Ready()
    {
        _host = GetNode<SimulationHost>("../SimulationHost");
        _input = GetNode<PlayerInput>("../PlayerInput");
    }

    public override void _Process(double delta)
    {
        _alive.Clear();
        foreach (var entity in _host.Simulation.World.Entities)
        {
            if (entity is not TestUnit { IsAlive: true } unit)
            {
                continue;
            }

            if (!_views.TryGetValue(unit.Id, out var view))
            {
                view = TakeView();
                _views.Add(unit.Id, view);
            }

            view.MaterialOverride = MaterialFor(unit.Owner.Value, _input.Selection.Contains(unit.Id));
            var current = WorldCoords.ToGodotOnGround(unit.Position, CubeSize / 2);
            var previous = _host.TryGetPreviousPosition(unit.Id, out var before)
                ? WorldCoords.ToGodotOnGround(before, CubeSize / 2)
                : current;
            view.Position = previous.Lerp(current, _host.Alpha);
            _alive.Add(unit.Id);
        }

        _gone.Clear();
        foreach (var id in _views.Keys)
        {
            if (!_alive.Contains(id))
            {
                _gone.Add(id);
            }
        }

        foreach (var id in _gone)
        {
            ReturnView(_views[id]);
            _views.Remove(id);
        }
    }

    private MeshInstance3D TakeView()
    {
        if (_pool.TryPop(out var view))
        {
            view.Visible = true;
            return view;
        }

        view = new MeshInstance3D { Mesh = _mesh };
        AddChild(view);
        return view;
    }

    private void ReturnView(MeshInstance3D view)
    {
        view.Visible = false;
        _pool.Push(view);
    }

    private StandardMaterial3D MaterialFor(int owner, bool selected)
    {
        if (!_materials.TryGetValue((owner, selected), out var material))
        {
            var color = owner switch
            {
                0 => new Color(0.6f, 0.6f, 0.6f),
                1 => new Color(0.2f, 0.45f, 0.95f),
                2 => new Color(0.9f, 0.25f, 0.2f),
                _ => Color.FromHsv(owner * 0.13f % 1f, 0.7f, 0.9f),
            };
            material = new StandardMaterial3D { AlbedoColor = color };
            if (selected)
            {
                material.EmissionEnabled = true;
                material.Emission = color;
                material.EmissionEnergyMultiplier = 0.8f;
            }

            _materials.Add((owner, selected), material);
        }

        return material;
    }
}
