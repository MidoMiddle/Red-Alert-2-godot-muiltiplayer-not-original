using System.Collections.Generic;
using Godot;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Client;

/// <summary>
/// Going into a unit (the owner, 2026-11-04: "play the strategy, then all at once be the sniper, in 3D, walking our map").
/// <c>possess</c> (F) with a unit of the player selected puts a perspective camera at its eyes; the mouse turns the look, the wheel draws
/// the camera back over its shoulder. W A S D walk it along the look (by the physical keys, whatever the layout), as plain moves of the
/// game given without the units' answers. The unit is under the player's hand (<see cref="Rts.Simulation.Commands.TakeControlCommand"/>):
/// it fires nothing by itself. The left button held fires as in a shooter: with an instant weapon an aimed shot each time it is loaded,
/// along the line from its eyes to what the crosshair is on — the rules cast the ray, with the spread of the weapon, and a soldier's head
/// kills (<see cref="Rts.Simulation.Systems.AimedShot"/>); with a rocket or a shell, an attack on what is under the crosshair, the unit
/// standing. The right button held looks through the sights: closer, closer still with a weapon that reaches far. While inside,
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

    // How often the aim of the held fire is looked at again with a weapon that is not instant: a new target is a new order.
    private const float AimSeconds = 0.2f;

    // The field of view: plain, through the sights, through the scope of a weapon that reaches farther than LongReach cells.
    private const float PlainFov = 70f;
    private const float SightsFov = 35f;
    private const float ScopeFov = 15f;
    private const int LongReach = 10;
    private const float HeadshotSeconds = 1.5f;

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
    private bool _ordered;
    private int _shotTick = -1;
    private bool _sights;
    private Crosshair? _crosshair;
    private Label? _headshot;
    private float _headshotLeft;

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
        if (_host is not null)
        {
            _host.EventRaised += OnEvent;
        }
    }

    // A shot of the possessed unit in a head: the word for it a while.
    private void OnEvent(Rts.Simulation.Events.SimEvent simEvent)
    {
        if (Active && simEvent is Rts.Simulation.Events.AimedHit { Headshot: true } hit && hit.Shooter == _unit && _headshot is not null)
        {
            _headshot.Visible = true;
            _headshotLeft = HeadshotSeconds;
        }
    }

    public override void _ExitTree()
    {
        if (_host is not null)
        {
            _host.EventRaised -= OnEvent;
        }

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
                // Slower through the sights, as much as the view is narrower.
                var sensitivity = Sensitivity * (_camera!.Fov / PlainFov);
                _yaw -= motion.Relative.X * sensitivity;
                _pitch = Mathf.Clamp(_pitch - (motion.Relative.Y * sensitivity), -MaxPitch, MaxPitch * 0.5f);
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
            case InputEventMouseButton { ButtonIndex: MouseButton.Right }:
                _sights = false;
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
        var weapon = WeaponOf(unit);
        var instant = weapon is not null && Instant(weapon);
        var fov = !_sights ? PlainFov : weapon is not null && weapon.Range.ToIntFloor() > LongReach ? ScopeFov : SightsFov;
        _camera.Fov = Mathf.Lerp(_camera.Fov, fov, Mathf.Min(1f, dt * 14f));
        if (_firing && instant)
        {
            Shoot(unit, eyes);
        }
        else if (_firing)
        {
            _aimLeft -= dt;
            if (_aimLeft <= 0f)
            {
                Fire(false);
            }
        }

        // An aimed shot leaves the unit walking (it strays twice as far); an attack order stops it.
        if (!_firing || instant)
        {
            Walk(view.GlobalPosition);
        }

        Mark(unit, weapon);
        if (_headshotLeft > 0f)
        {
            _headshotLeft -= dt;
            _headshot!.Visible = _headshotLeft > 0f;
        }
    }

    // The first weapon of the unit as it is now (dug in, its deployed one), or null.
    private Rts.Simulation.Rules.WeaponDef? WeaponOf(Unit unit)
    {
        var rules = _host!.Simulation.World.Rules;
        var slots = rules.WeaponsOf(unit);
        return slots.Length > 0 && slots[0] >= 0 ? rules.Database.Weapons[slots[0]] : null;
    }

    private bool Instant(Rts.Simulation.Rules.WeaponDef weapon) =>
        _host!.Simulation.World.Rules.Database.TryGetProjectile(weapon.Projectile, out var projectile)
        && projectile.Motion == Rts.Simulation.Rules.ProjectileMotion.Instant;

    // The left button held with an instant weapon: an aimed shot whenever it is loaded, once a tick at most, from the eyes to what the
    // crosshair is on.
    private void Shoot(Unit unit, Vector3 eyes)
    {
        var tick = _host!.Simulation.CurrentTick;
        if (unit.Weapons.Length == 0 || unit.Weapons[0].ReloadLeft > 0 || tick == _shotTick || _camera is null)
        {
            return;
        }

        var forward = -_camera.GlobalBasis.Z;
        var direction = forward;
        if (_back >= 0.3f)
        {
            // Behind the shoulder: the line from the eyes to the point the crosshair is on.
            var along = Along(_camera.GlobalPosition, forward) ?? 60f;
            direction = (_camera.GlobalPosition + (forward * along) - eyes).Normalized();
        }

        var yaw = Mathf.PosMod(Mathf.RoundToInt(Mathf.Atan2(direction.X, -direction.Z) / Mathf.Tau * 65536f), 65536);
        var pitch = Mathf.Clamp(Mathf.RoundToInt(Mathf.Asin(Mathf.Clamp(direction.Y, -1f, 1f)) / Mathf.Tau * 65536f), -16000, 16000);
        _host.IssueAimedShot(_unit, (ushort)yaw, (short)pitch);
        _shotTick = tick;
    }

    // The crosshair: its strokes as far apart as the shot may stray now, the reload under them.
    private void Mark(Unit unit, Rts.Simulation.Rules.WeaponDef? weapon)
    {
        if (_crosshair is null || _camera is null)
        {
            return;
        }

        var spread = weapon is null ? 0f : (float)weapon.AimSpread.Raw / 65536f * (unit.MoveTarget is null && !unit.PathPending ? 1f : 2f);
        var half = GetViewport().GetVisibleRect().Size.Y / 2f;
        _crosshair.Gap = Mathf.Max(3f, Mathf.Tan(Mathf.DegToRad(spread)) / Mathf.Tan(Mathf.DegToRad(_camera.Fov / 2f)) * half);
        _crosshair.Reload = weapon is { Reload: > 0 } && unit.Weapons.Length > 0
            ? Mathf.Clamp(unit.Weapons[0].ReloadLeft / (float)weapon.Reload, 0f, 1f)
            : 0f;
        _crosshair.QueueRedraw();
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
        _ordered = true;
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
        if (_ordered)
        {
            _host?.IssueStop([_unit]);
            _ordered = false;
        }
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
        _ordered = false;
        _sights = false;
        _headshotLeft = 0f;
        _host.IssueTakeControl(_unit, true);
        _camera = new Camera3D { Name = "PossessionCamera", Fov = PlainFov, Near = 0.02f, Far = 90f, CullMask = 0xFFFFF & ~OwnLayer };
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
        // Walking or firing, it stops: the keys and the button are the map's again; and it is its own again.
        if (_walking || _ordered)
        {
            _host?.IssueStop([_unit]);
        }

        _walking = false;
        _firing = false;
        _ordered = false;
        _host?.IssueTakeControl(_unit, false);

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

    // Left: fire while held. Right: through the sights while held. The wheel: the camera in or out.
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
                if (_host.Simulation.World.TryGet(_unit, out var entity) && entity is Unit unit && WeaponOf(unit) is { } weapon && !Instant(weapon))
                {
                    Fire(true);
                }

                break;
            case MouseButton.Right:
                _sights = true;
                break;
        }
    }

    // Where the ray first goes under the ground, within the far end of the camera.
    private static WorldPos? Ground(Vector3 origin, Vector3 direction) =>
        GroundAlong(origin, direction) is { } along ? WorldCoords.FromGodot(origin + (direction * along)) : null;

    // How far along the ray it first goes under the ground, or null.
    private static float? GroundAlong(Vector3 origin, Vector3 direction)
    {
        const float step = 0.05f;
        for (var along = 0.2f; along < 90f; along += step)
        {
            var point = origin + (direction * along);
            if (point.Y <= WorldCoords.ToGodotOnGround(WorldCoords.FromGodot(point)).Y)
            {
                return along;
            }
        }

        return null;
    }

    // How far along the ray the crosshair is on something: the ground, or nearer a unit or a building it passes close by.
    private float? Along(Vector3 origin, Vector3 direction)
    {
        var ground = GroundAlong(origin, direction);
        if (Aimed(origin, direction) is { } target && _host!.Simulation.World.TryGet(target, out var entity))
        {
            var centre = entity is Unit unit ? WorldCoords.ToGodotOnUnit(unit, unit.Position, 0.3f)
                : WorldCoords.ToGodotOnGround(Rts.Simulation.Combat.Aim.CentreOf((Building)entity), 0.5f);
            var along = (centre - origin).Dot(direction);
            return ground is { } floor ? Mathf.Min(floor, along) : along;
        }

        return ground;
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

    // The crosshair, the word for a shot in the head and a line of what the keys do.
    private CanvasLayer Overlay()
    {
        var layer = new CanvasLayer { Name = "PossessionOverlay", Layer = 4 };
        _crosshair = new Crosshair { Name = "Crosshair" };
        layer.AddChild(_crosshair);
        _headshot = new Label
        {
            Text = Tr("UI_POSSESS_HEADSHOT"),
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0f,
            AnchorRight = 1f,
            AnchorTop = 0.32f,
            AnchorBottom = 0.32f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _headshot.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.25f));
        _headshot.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.8f));
        _headshot.AddThemeFontSizeOverride("font_size", 28);
        layer.AddChild(_headshot);
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
