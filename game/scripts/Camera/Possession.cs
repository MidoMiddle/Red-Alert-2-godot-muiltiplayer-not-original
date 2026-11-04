using System.Collections.Generic;
using Godot;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Client;

/// <summary>
/// Going into a unit (the owner, 2026-11-04: "play the strategy, then all at once be the sniper, in 3D, walking our map").
/// <c>possess</c> (F) with a unit of the player selected puts a perspective camera at its eyes; the mouse turns the look, the wheel draws
/// the camera back over its shoulder. W A S D walk it along the look (by the physical keys, whatever the layout), the left button held
/// fires as in a shooter — at the enemy under the crosshair, else at the ground there — and the unit stands while it fires; a right
/// click sends it where the crosshair points. All of it is the plain orders of the game, given many times a second and without the
/// units' answers; the rules know nothing of it. While inside,
/// the people stand at their own size and the low houses of the map at their full height (<see cref="Units.UnitView.SetRealSize"/>,
/// <see cref="Models.ModelLibrary.SetFullHeight"/>): from the ground the proportions of RA2 read as a toy town. F or Esc goes back to
/// the strategy, the camera of the map over the unit; so does its death.
/// </summary>
public partial class Possession : Node
{
    private const float EyeHeight = 0.5f;
    private const float VehicleEyeHeight = 0.45f;
    private const float Sensitivity = 0.0035f;
    private const float MaxPitch = 1.25f;
    private const float MaxBack = 2.4f;
    private const float BackStep = 0.4f;
    private const float ShadowDistance = 30f;

    // How far ahead of the unit a step of W A S D sends it, in cells, and how near the end of it the next is given: a new order sends a
    // soldier back to the middle of its cell first, so they are few — at the end of a step or when the look turns.
    private const float StepAhead = 3f;
    private const float StepRenew = 1.4f;
    private const float StepTurn = 0.35f;

    // How often the aim of the held fire is looked at again: a new target is a new order.
    private const float AimSeconds = 0.2f;

    // The layer the possessed unit's own meshes go to while the camera is inside it: the camera does not draw it, the sun still does.
    private const uint OwnLayer = 1u << 19;

    private SimulationHost? _host;
    private PlayerInput? _input;
    private UnitRenderer? _units;
    private RtsCamera? _rig;
    private DirectionalLight3D? _sun;
    private CanvasLayer? _bars;
    private Camera3D? _camera;
    private CanvasLayer? _overlay;
    private EntityId _unit;
    private float _yaw;
    private float _pitch;
    private float _back;
    private float _sunShadow;
    private readonly List<(VisualInstance3D Mesh, uint Layers)> _hidden = new();
    private bool _walking;
    private Vector3 _walkDirection;
    private Vector3 _stepTo;
    private bool _firing;
    private float _aimLeft;
    private EntityId? _shotAt;
    private WorldPos? _shotPoint;

    /// <summary>Whether the camera is inside a unit now.</summary>
    public bool Active => _camera is not null;

    public override void _Ready()
    {
        _host = GetNodeOrNull<SimulationHost>("../SimulationHost");
        _input = GetNodeOrNull<PlayerInput>("../PlayerInput");
        _units = GetNodeOrNull<UnitRenderer>("../UnitsRoot");
        _rig = GetNodeOrNull<RtsCamera>("../CameraRig");
        _sun = GetNodeOrNull<DirectionalLight3D>("../Sun");
        _bars = GetNodeOrNull<CanvasLayer>("../Bars");
    }

    public override void _ExitTree()
    {
        if (Active)
        {
            Leave();
        }
    }

    // Before the field and the HUD: inside, the mouse and Esc are the possession's.
    public override void _Input(InputEvent inputEvent)
    {
        if (inputEvent is InputEventKey { Pressed: true, Echo: false } key && key.IsActionPressed("possess", false, true))
        {
            if (Active)
            {
                Leave();
            }
            else
            {
                Enter();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (!Active)
        {
            return;
        }

        switch (inputEvent)
        {
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }:
                Leave();
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseMotion motion:
                _yaw -= motion.Relative.X * Sensitivity;
                _pitch = Mathf.Clamp(_pitch - (motion.Relative.Y * Sensitivity), -MaxPitch, MaxPitch * 0.5f);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventKey { PhysicalKeycode: Key.W or Key.A or Key.S or Key.D }:
                // Walking, read every frame (_Process): not the map's keys of the same letters.
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton { Pressed: true } button:
                Click(button.ButtonIndex);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left }:
                CeaseFire();
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton:
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    public override void _Process(double delta)
    {
        if (!Active)
        {
            return;
        }

        if (_host is null || !_host.Simulation.World.TryGet(_unit, out var entity) || entity is not Unit { IsAlive: true } unit
            || _units?.ViewOf(_unit) is not { } view)
        {
            Leave();
            return;
        }

        // At the eyes of the unit as it is drawn, or behind and above them.
        var eyes = view.GlobalPosition + new Vector3(0f, unit.Archetype.Category == UnitCategory.Infantry ? EyeHeight : VehicleEyeHeight, 0f);
        var look = new Basis(Vector3.Up, _yaw) * new Basis(Vector3.Right, _pitch);
        _camera!.GlobalTransform = new Transform3D(look, eyes + (look * new Vector3(0f, _back * 0.35f, _back)));
        Hide(view, _back < 0.3f);

        // The map camera keeps over the unit, so leaving finds the strategy where the unit is.
        _rig?.CenterOn(view.GlobalPosition);

        var dt = (float)delta;
        if (_firing)
        {
            _aimLeft -= dt;
            if (_aimLeft <= 0f)
            {
                Fire(false);
            }
        }
        else
        {
            Walk(view.GlobalPosition);
        }
    }

    // W A S D: a step along the look — forward, back, to the sides — given anew near its end or when the look turns; let go, it stops.
    private void Walk(Vector3 at)
    {
        var forward = (Input.IsPhysicalKeyPressed(Key.W) ? 1f : 0f) - (Input.IsPhysicalKeyPressed(Key.S) ? 1f : 0f);
        var side = (Input.IsPhysicalKeyPressed(Key.D) ? 1f : 0f) - (Input.IsPhysicalKeyPressed(Key.A) ? 1f : 0f);
        if (forward == 0f && side == 0f)
        {
            if (_walking)
            {
                _host!.IssueStop([_unit]);
                _walking = false;
            }

            return;
        }

        var direction = (new Basis(Vector3.Up, _yaw) * new Vector3(side, 0f, -forward)).Normalized();
        var left = new Vector2(_stepTo.X - at.X, _stepTo.Z - at.Z).Length();
        if (_walking && left > StepRenew && direction.AngleTo(_walkDirection) < StepTurn)
        {
            return;
        }

        var ahead = at + (direction * StepAhead);
        var to = WorldCoords.FromGodot(ahead);
        if (_host!.Simulation.World.Contains(to))
        {
            _host.IssueMove([_unit], to, voice: !_walking);
            _walking = true;
            _walkDirection = direction;
            _stepTo = ahead;
        }
    }

    // The left button held: the enemy under the crosshair, else the ground there; a new order only when the aim changed.
    private void Fire(bool first)
    {
        _aimLeft = AimSeconds;
        if (_camera is null)
        {
            return;
        }

        _walking = false;
        var origin = _camera.GlobalPosition;
        var direction = -_camera.GlobalBasis.Z;
        if (Aimed(origin, direction) is { } target)
        {
            if (first || target != _shotAt)
            {
                _host!.IssueAttack([_unit], target, voice: first);
            }

            _shotAt = target;
            _shotPoint = null;
        }
        else if (Ground(origin, direction) is { } point && _host!.Simulation.World.Contains(point))
        {
            if (first || _shotPoint is not { } before || (before - point).LengthSquared > (long)WorldPos.LeptonsPerCell * WorldPos.LeptonsPerCell)
            {
                _host.IssueForceAttack([_unit], null, point, voice: first);
                _shotPoint = point;
            }

            _shotAt = null;
        }
    }

    // The left button let go: the unit holds its fire.
    private void CeaseFire()
    {
        if (!_firing)
        {
            return;
        }

        _firing = false;
        _shotAt = null;
        _shotPoint = null;
        _host?.IssueStop([_unit]);
    }

    private void Enter()
    {
        if (_host is null || _input is null || _units is null || _host.IsPlayback)
        {
            return;
        }

        var world = _host.Simulation.World;
        Unit? chosen = null;
        foreach (var id in _input.Selection.Units)
        {
            if (world.TryGet(id, out var entity) && entity is Unit { IsAlive: true } unit && unit.Owner == SimulationHost.LocalPlayer
                && _units.ViewOf(id) is not null)
            {
                chosen = unit;
                break;
            }
        }

        if (chosen is null)
        {
            return;
        }

        _unit = chosen.Id;
        _yaw = Units.UnitFacing.InterpolatedYaw(chosen.Facing, chosen.Facing, 0f);
        _pitch = -0.12f;
        _back = 0f;
        _walking = false;
        _firing = false;
        _shotAt = null;
        _shotPoint = null;
        _camera = new Camera3D { Name = "PossessionCamera", Fov = 70f, Near = 0.02f, Far = 90f, CullMask = 0xFFFFF & ~OwnLayer };
        AddChild(_camera);
        _camera.MakeCurrent();
        _overlay = Overlay();
        AddChild(_overlay);
        if (_rig is not null)
        {
            _rig.ProcessMode = ProcessModeEnum.Disabled;
        }

        if (_sun is not null)
        {
            _sunShadow = _sun.DirectionalShadowMaxDistance;
            _sun.DirectionalShadowMaxDistance = ShadowDistance;
        }

        if (_bars is not null)
        {
            _bars.Visible = false;
        }

        // From the eyes the shade of the fog would black out the sky and the land beyond the cells seen.
        if (Terrain.FogOfWar.Current is { } fog)
        {
            fog.Shaded = false;
        }

        Units.UnitView.SetRealSize(GetTree(), true);
        Models.ModelLibrary.SetFullHeight(GetTree(), true);
        Input.MouseMode = Input.MouseModeEnum.Captured;
        GD.Print($"[possess] into {chosen.Archetype.Id} #{chosen.Id.Value}");
    }

    private void Leave()
    {
        // Walking or firing, it stops: the keys and the button are the map's again.
        if (_walking || _firing)
        {
            _host?.IssueStop([_unit]);
            _walking = false;
            _firing = false;
        }

        Input.MouseMode = Input.MouseModeEnum.Visible;
        if (_units?.ViewOf(_unit) is { } view)
        {
            Hide(view, false);
        }

        _hidden.Clear();
        Units.UnitView.SetRealSize(GetTree(), false);
        Models.ModelLibrary.SetFullHeight(GetTree(), false);
        if (_rig is not null)
        {
            _rig.ProcessMode = ProcessModeEnum.Inherit;
            _rig.Camera.MakeCurrent();
        }

        if (_sun is not null && _sunShadow > 0f)
        {
            _sun.DirectionalShadowMaxDistance = _sunShadow;
        }

        if (_bars is not null)
        {
            _bars.Visible = true;
        }

        if (Terrain.FogOfWar.Current is { } fog)
        {
            fog.Shaded = true;
        }

        _camera?.QueueFree();
        _camera = null;
        _overlay?.QueueFree();
        _overlay = null;
        GD.Print("[possess] back to the map");
    }

    // The meshes of the possessed unit: out of the camera's sight while it is inside them (its shadow stays), back when it is not.
    private void Hide(Node3D view, bool hidden)
    {
        if (hidden && _hidden.Count == 0)
        {
            Collect(view);
            foreach (var (mesh, _) in _hidden)
            {
                mesh.Layers = OwnLayer;
            }
        }
        else if (!hidden && _hidden.Count > 0)
        {
            foreach (var (mesh, layers) in _hidden)
            {
                if (IsInstanceValid(mesh))
                {
                    mesh.Layers = layers;
                }
            }

            _hidden.Clear();
        }
    }

    private void Collect(Node node)
    {
        if (node is VisualInstance3D mesh)
        {
            _hidden.Add((mesh, mesh.Layers));
        }

        foreach (var child in node.GetChildren())
        {
            Collect(child);
        }
    }

    // Left: fire while held. Right: go where the crosshair points on the ground. The wheel: the camera in or out.
    private void Click(MouseButton button)
    {
        if (_host is null || _camera is null)
        {
            return;
        }

        var origin = _camera.GlobalPosition;
        var direction = -_camera.GlobalBasis.Z;
        switch (button)
        {
            case MouseButton.WheelDown:
                _back = Mathf.Min(MaxBack, _back + BackStep);
                break;
            case MouseButton.WheelUp:
                _back = Mathf.Max(0f, _back - BackStep);
                break;
            case MouseButton.Left:
                _firing = true;
                Fire(true);
                break;
            case MouseButton.Right when Ground(origin, direction) is { } point && _host.Simulation.World.Contains(point):
                _host.IssueMove([_unit], point);
                break;
        }
    }

    // Where the ray first goes under the ground, within the far end of the camera.
    private static WorldPos? Ground(Vector3 origin, Vector3 direction)
    {
        const float step = 0.05f;
        for (var along = 0.2f; along < 90f; along += step)
        {
            var point = origin + (direction * along);
            var ground = WorldCoords.ToGodotOnGround(WorldCoords.FromGodot(point)).Y;
            if (point.Y <= ground)
            {
                return WorldCoords.FromGodot(point);
            }
        }

        return null;
    }

    // The enemy — a unit or a building the player sees — nearest along the ray of all those it passes close by.
    private EntityId? Aimed(Vector3 origin, Vector3 direction)
    {
        var world = _host!.Simulation.World;
        EntityId? best = null;
        var bestAlong = float.MaxValue;
        foreach (var entity in world.Entities)
        {
            if (!entity.IsAlive || entity.Id == _unit || entity.Owner == SimulationHost.LocalPlayer || entity.Owner.IsNeutral
                || world.AreAllies(entity.Owner, SimulationHost.LocalPlayer) || !Terrain.FogOfWar.Shows(world, entity))
            {
                continue;
            }

            var (centre, radius) = entity switch
            {
                Unit unit => (WorldCoords.ToGodotOnUnit(unit, unit.Position, 0.3f), 0.35f),
                Building building => (WorldCoords.ToGodotOnGround(Rts.Simulation.Combat.Aim.CentreOf(building), 0.5f),
                    0.5f * Mathf.Max(building.Width, building.Height)),
                _ => (Vector3.Zero, 0f),
            };
            var along = (centre - origin).Dot(direction);
            if (radius <= 0f || along <= 0f || along >= bestAlong)
            {
                continue;
            }

            if ((origin + (direction * along)).DistanceTo(centre) <= radius)
            {
                best = entity.Id;
                bestAlong = along;
            }
        }

        return best;
    }

    // The crosshair and a line of what the keys do.
    private CanvasLayer Overlay()
    {
        var layer = new CanvasLayer { Name = "PossessionOverlay", Layer = 4 };
        // Two thin bars crossed at the middle of the screen.
        foreach (var (width, height) in new[] { (18f, 2f), (2f, 18f) })
        {
            layer.AddChild(new ColorRect
            {
                Color = new Color(1f, 1f, 1f, 0.85f),
                AnchorLeft = 0.5f,
                AnchorTop = 0.5f,
                AnchorRight = 0.5f,
                AnchorBottom = 0.5f,
                OffsetLeft = -width / 2f,
                OffsetTop = -height / 2f,
                OffsetRight = width / 2f,
                OffsetBottom = height / 2f,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        }

        var hint = new Label
        {
            Text = Tr("UI_POSSESS_HINT"),
            Position = new Vector2(16f, 12f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        hint.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.8f));
        layer.AddChild(hint);
        return layer;
    }
}
