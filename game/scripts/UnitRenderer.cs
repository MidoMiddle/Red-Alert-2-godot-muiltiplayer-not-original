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

            // An enemy holo tank standing still (stage 3c) is drawn as a tree; its own view waits hidden.
            var tree = Disguises.LooksLikeTree(world, unit);
            TreeFor(unit, tree);

            // A disguised infiltrator (stage 3b) looks to another team like a soldier of its own, in its own colour.
            var (shape, colour) = Disguise(world, unit);
            if (_views.TryGetValue(unit.Id, out var view) && view.Type != shape.Type)
            {
                ReturnView(view);
                _views.Remove(unit.Id);
            }

            if (!_views.TryGetValue(unit.Id, out view))
            {
                view = TakeView(shape);
                _views.Add(unit.Id, view);
            }

            var glow = _input.Selection.Contains(unit.Id) ? Glow.Selected : unit.Id == _input.Hovered ? Glow.Hovered : Glow.None;
            // Covered by the Bastion Field Projector (stage 2d): crimson.
            view.SetMaterial(MaterialFor(unit.IsInvulnerable(world.CurrentTick) ? Curtain : colour.Value, glow));
            view.Visible = !tree;
            // A Rifleman dug in sits behind a ring of sandbags (RA2).
            view.SetSandbags(unit.Deployed && unit.Archetype.DeployedWeapon >= 0);
            var current = new UnitPose(unit.Position, unit.Facing, unit.TurretFacing);
            var previous = _host.TryGetPreviousPose(unit.Id, out var before) ? before : current;
            var alpha = _host.Alpha;
            // A helicopter idle over open ground sets down, and rises again when it has to go.
            var altitude = Units.UnitAltitude.Lands(unit.Archetype)
                ? Units.UnitAltitude.Step(unit.Id, Units.UnitAltitude.Of(unit.Archetype), Units.UnitAltitude.Target(world, unit), (float)delta)
                : Units.UnitAltitude.Of(unit.Archetype);
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
            Units.UnitAltitude.Forget(id);
            _views.Remove(id);
        }

        foreach (var (id, tree) in _trees)
        {
            if (!_alive.Contains(id))
            {
                tree.Visible = false;
            }
        }
    }

    private readonly Dictionary<EntityId, Node3D> _trees = new();

    // A pine like those of the map (MapObjects): a trunk and a cone of a crown.
    private void TreeFor(Unit unit, bool shown)
    {
        if (!shown)
        {
            if (_trees.TryGetValue(unit.Id, out var hidden))
            {
                hidden.Visible = false;
            }

            return;
        }

        if (!_trees.TryGetValue(unit.Id, out var tree))
        {
            // A tree of the map's climate, as the trees around it (TreeShapes).
            var kind = GetNodeOrNull<Terrain.TerrainBuilder>("../Terrain")?.Climate?.Trees ?? Climates.TreeKind.Pine;
            tree = Terrain.TreeShapes.Build(kind, unit.Id.Value);
            tree.Name = "Mirage" + unit.Id.Value;
            AddChild(tree);
            _trees.Add(unit.Id, tree);
        }

        tree.Visible = true;
        tree.Position = WorldCoords.ToGodotOnGround(unit.Position, 0f);
    }

    /// <summary>How the unit looks to the local player: as itself, or a disguised infiltrator of another team as the player's soldier.</summary>
    internal static (UnitArchetype Shape, PlayerId Colour) Disguise(GameWorld world, Unit unit)
    {
        var viewer = SimulationHost.LocalPlayer;
        if (!unit.Archetype.Has(UnitTalents.Disguised) || world.AreAllies(viewer, unit.Owner) || viewer.IsNeutral
            || !UnitTypes.TryGetById(world.Player(viewer).Faction.StartingInfantry, out var soldier))
        {
            return (unit.Archetype, unit.Owner);
        }

        return (world.Rules.Unit(soldier), viewer);
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

    // The colour key of what the curtain covers.
    private const int Curtain = -1;

    private StandardMaterial3D MaterialFor(int owner, Glow glow)
    {
        if (!_materials.TryGetValue((owner, glow), out var material))
        {
            var color = owner == Curtain ? new Color(0.75f, 0.08f, 0.1f) : Matches.PlayerColors.Of(new PlayerId(owner));
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
