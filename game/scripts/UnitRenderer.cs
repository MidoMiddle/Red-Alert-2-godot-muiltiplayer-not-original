using System.Collections.Generic;
using Godot;
using Rts.Client.Units;
using Rts.Simulation.World;

namespace Rts.Client;

/// <summary>
/// Draws every unit with its symbolic <see cref="UnitView"/>, interpolated between the previous and the current tick so that
/// 15 ticks per second still look smooth at 60+ frames per second: position along a line, hull and turret along the
/// shorter arc. Views are pooled per unit type. Selected units glow; the unit under the pointer glows faintly (M2 step 13). A unit of
/// another player is drawn only while the local player sees it (<see cref="Terrain.FogOfWar.Shows"/>, M4 step 12).
/// </summary>
public partial class UnitRenderer : Node3D
{
    private readonly Dictionary<EntityId, UnitView> _views = new();
    private readonly Dictionary<UnitType, Stack<UnitView>> _pool = new();
    private readonly HashSet<EntityId> _alive = new();
    private readonly List<EntityId> _gone = new();
    private readonly Dictionary<(int Owner, Glow Glow), StandardMaterial3D> _materials = new();

    private SimulationHost _host = null!;
    private PlayerInput _input = null!;

    private enum Glow
    {
        None,
        Hovered,
        Selected,
    }

    public override void _Ready()
    {
        _host = GetNode<SimulationHost>("../SimulationHost");
        _input = GetNode<PlayerInput>("../PlayerInput");
    }

    public override void _Process(double delta)
    {
        _alive.Clear();
        var world = _host.Simulation.World;
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true } unit || !Terrain.FogOfWar.Shows(world, unit))
            {
                continue;
            }

            if (!_views.TryGetValue(unit.Id, out var view))
            {
                view = TakeView(unit.Archetype);
                _views.Add(unit.Id, view);
            }

            var glow = _input.Selection.Contains(unit.Id) ? Glow.Selected : unit.Id == _input.Hovered ? Glow.Hovered : Glow.None;
            view.SetMaterial(MaterialFor(unit.Owner.Value, glow));
            var current = new UnitPose(unit.Position, unit.Facing, unit.TurretFacing);
            var previous = _host.TryGetPreviousPose(unit.Id, out var before) ? before : current;
            var alpha = _host.Alpha;
            var altitude = Units.UnitAltitude.Of(unit.Archetype);
            view.SetPose(
                WorldCoords.ToGodotOnGround(previous.Position, altitude).Lerp(WorldCoords.ToGodotOnGround(current.Position, altitude), alpha),
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

    private StandardMaterial3D MaterialFor(int owner, Glow glow)
    {
        if (!_materials.TryGetValue((owner, glow), out var material))
        {
            var color = Matches.PlayerColors.Of(new PlayerId(owner));
            material = new StandardMaterial3D { AlbedoColor = color };
            if (glow != Glow.None)
            {
                material.EmissionEnabled = true;
                material.Emission = color;
                material.EmissionEnergyMultiplier = glow == Glow.Selected ? 0.8f : 0.3f;
            }

            _materials.Add((owner, glow), material);
        }

        return material;
    }
}
