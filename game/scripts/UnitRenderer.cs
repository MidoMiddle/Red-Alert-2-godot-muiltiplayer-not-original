using System.Collections.Generic;
using Godot;
using Rts.Client.Units;
using Rts.Simulation.World;

namespace Rts.Client;

/// <summary>
/// Draws every unit with its symbolic <see cref="UnitView"/>, interpolated between the previous and the current tick so that
/// 15 ticks per second still look smooth at 60+ frames per second: position along a line, hull and turret along the
/// shorter arc. Views are pooled per unit type. Selected units glow.
/// </summary>
public partial class UnitRenderer : Node3D
{
    private readonly Dictionary<EntityId, UnitView> _views = new();
    private readonly Dictionary<UnitType, Stack<UnitView>> _pool = new();
    private readonly HashSet<EntityId> _alive = new();
    private readonly List<EntityId> _gone = new();
    private readonly Dictionary<(int Owner, bool Selected), StandardMaterial3D> _materials = new();

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
            if (entity is not Unit { IsAlive: true } unit)
            {
                continue;
            }

            if (!_views.TryGetValue(unit.Id, out var view))
            {
                view = TakeView(unit.Archetype);
                _views.Add(unit.Id, view);
            }

            view.SetMaterial(MaterialFor(unit.Owner.Value, _input.Selection.Contains(unit.Id)));
            var current = new UnitPose(unit.Position, unit.Facing, unit.TurretFacing);
            var previous = _host.TryGetPreviousPose(unit.Id, out var before) ? before : current;
            var alpha = _host.Alpha;
            view.SetPose(
                WorldCoords.ToGodotOnGround(previous.Position).Lerp(WorldCoords.ToGodotOnGround(current.Position), alpha),
                UnitFacing.InterpolatedYaw(previous.Facing, current.Facing, alpha),
                UnitFacing.InterpolatedYaw(previous.TurretFacing, current.TurretFacing, alpha));
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

    private UnitView TakeView(UnitArchetype archetype)
    {
        if (_pool.TryGetValue(archetype.Type, out var pool) && pool.TryPop(out var view))
        {
            view.Visible = true;
            return view;
        }

        view = new UnitView();
        view.Build(archetype);
        AddChild(view);
        return view;
    }

    private void ReturnView(UnitView view)
    {
        view.Visible = false;
        if (!_pool.TryGetValue(view.Type, out var pool))
        {
            pool = new Stack<UnitView>();
            _pool.Add(view.Type, pool);
        }

        pool.Push(view);
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
