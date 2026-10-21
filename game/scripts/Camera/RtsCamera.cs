using Godot;
using Rts.Client.App;

namespace Rts.Client;

/// <summary>
/// RA2 camera (docs/implementation/M1_visual_spike.md, 4.1): orthographic, yaw 45°, pitch 30° down,
/// so a 1×1 ground cell looks like a 2:1 diamond. World X runs right-down on screen, world Y (Godot Z) left-down.
/// The rig only slides over the ground; the angle never changes.
/// Moves with the camera key actions, with the mouse at the window edge (setting <c>EdgeScroll</c>) and with a drag
/// of the camera button that <see cref="PlayerInput"/> passes in (setting <c>CameraDrag</c>, docs/implementation/M6_menus_skirmish.md).
/// Zooms with the mouse wheel.
/// </summary>
public partial class RtsCamera : Node3D
{
    public const float YawDegrees = 45f;
    public const float PitchDegrees = 30f;

    /// <summary>Vertical view extent in world units: a cell is about 60 px wide at the 1080 px base height.</summary>
    public const float DefaultSize = 25.5f;

    private const float MinZoom = 0.7f;
    private const float MaxZoom = 1.6f;
    private const float ZoomStep = 1.12f;
    private const float ZoomSmoothing = 12f;
    private const float CameraDistance = 100f;

    private const float ScrollSpeed = 14f; // cells per second at default zoom and the neutral speed setting
    private const float NeutralScrollSpeed = 5f; // the default of the scroll speed setting (1–10)
    private const float KeyAcceleration = 1.5f; // extra speed multiplier gained per second of holding
    private const float MaxKeyBoost = 2.5f;
    private const int EdgeBand = 8; // pixels

    private Camera3D _camera = null!;
    private SimulationHost _host = null!;
    private float _targetSize = DefaultSize;
    private float _keyHoldTime;

    /// <summary>The ground point held under the pointer during a drag, or null when no drag is running.</summary>
    private Vector3? _dragAnchor;

    public Camera3D Camera => _camera;

    public override void _Ready()
    {
        _host = GetNode<SimulationHost>("../SimulationHost");
        var pivot = GetNode<Node3D>("Pivot");
        pivot.RotationDegrees = new Vector3(-PitchDegrees, YawDegrees, 0f);

        _camera = GetNode<Camera3D>("Pivot/Camera3D");
        _camera.Projection = Camera3D.ProjectionType.Orthogonal;
        _camera.Size = DefaultSize;
        _camera.Position = new Vector3(0f, 0f, CameraDistance);
        _camera.Near = 1f;
        _camera.Far = CameraDistance * 2f;

        Position = new Vector3(_host.Simulation.World.WidthCells / 2f, 0f, _host.Simulation.World.HeightCells / 2f);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var settings = AppSettings.Instance?.Values ?? SettingsValues.Defaults;
        var screenVelocity = KeyboardScroll(dt) + (settings.EdgeScroll ? EdgeScroll() : Vector2.Zero);
        if (screenVelocity != Vector2.Zero)
        {
            var scale = _camera.Size / DefaultSize * (settings.ScrollSpeed / NeutralScrollSpeed) * dt;
            MoveOverGround(GroundRight() * screenVelocity.X * scale + GroundUp() * -screenVelocity.Y * scale);
        }

        _camera.Size = Mathf.Lerp(_camera.Size, _targetSize, 1f - Mathf.Exp(-ZoomSmoothing * dt));
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton { Pressed: true } wheel)
        {
            if (wheel.ButtonIndex == MouseButton.WheelUp)
            {
                _targetSize = Mathf.Max(DefaultSize * MinZoom, _targetSize / ZoomStep);
            }
            else if (wheel.ButtonIndex == MouseButton.WheelDown)
            {
                _targetSize = Mathf.Min(DefaultSize * MaxZoom, _targetSize * ZoomStep);
            }
        }
    }

    /// <summary>The camera button went down at <paramref name="screen"/>: the ground point under the pointer is held from now on.</summary>
    public void BeginDrag(Vector2 screen) => _dragAnchor = TryGroundAt(screen, out var ground) ? ground : null;

    /// <summary>The pointer moved during a drag: the rig moves so that the held ground point stays under the pointer.</summary>
    public void DragTo(Vector2 screen)
    {
        if (_dragAnchor is { } anchor && TryGroundAt(screen, out var current))
        {
            // With an orthographic camera, moving the rig shifts every ground point under the screen by the same offset.
            MoveOverGround(anchor - current);
        }
    }

    public void EndDrag() => _dragAnchor = null;

    /// <summary>Point on the ground plane (y = 0) under a screen position. Terrain heights come in step 3.</summary>
    public bool TryGroundAt(Vector2 screen, out Vector3 ground)
    {
        ground = default;
        var origin = _camera.ProjectRayOrigin(screen);
        var direction = _camera.ProjectRayNormal(screen);
        if (direction.Y >= 0f)
        {
            return false;
        }

        ground = origin + direction * (-origin.Y / direction.Y);
        return true;
    }

    /// <summary>Screen position of a world point.</summary>
    public Vector2 UnprojectPosition(Vector3 world) => _camera.UnprojectPosition(world);

    // Screen-space velocity in cells per second: x to the right, y downwards.
    private Vector2 KeyboardScroll(float dt)
    {
        var direction = Vector2.Zero;
        if (Input.IsActionPressed("camera_up")) direction.Y -= 1f;
        if (Input.IsActionPressed("camera_down")) direction.Y += 1f;
        if (Input.IsActionPressed("camera_left")) direction.X -= 1f;
        if (Input.IsActionPressed("camera_right")) direction.X += 1f;

        if (direction == Vector2.Zero)
        {
            _keyHoldTime = 0f;
            return Vector2.Zero;
        }

        _keyHoldTime += dt;
        var boost = Mathf.Min(MaxKeyBoost, 1f + _keyHoldTime * KeyAcceleration);
        return direction.Normalized() * ScrollSpeed * boost;
    }

    private Vector2 EdgeScroll()
    {
        if (_dragAnchor is not null || !DisplayServer.WindowIsFocused())
        {
            return Vector2.Zero;
        }

        var viewport = GetViewport();
        var rect = viewport.GetVisibleRect();
        var mouse = viewport.GetMousePosition();
        if (!rect.HasPoint(mouse))
        {
            return Vector2.Zero;
        }

        var velocity = Vector2.Zero;
        velocity.X -= EdgeStrength(mouse.X - rect.Position.X);
        velocity.X += EdgeStrength(rect.End.X - mouse.X);
        velocity.Y -= EdgeStrength(mouse.Y - rect.Position.Y);
        velocity.Y += EdgeStrength(rect.End.Y - mouse.Y);
        return velocity * ScrollSpeed;
    }

    // 0 outside the band, growing to 1 at the very edge.
    private static float EdgeStrength(float distanceToEdge) =>
        distanceToEdge >= EdgeBand ? 0f : 1f - Mathf.Max(0f, distanceToEdge) / EdgeBand;

    private Vector3 GroundRight() => Flatten(_camera.GlobalBasis.X);

    // Screen "up" on the ground: the camera's forward direction without its downward tilt.
    private Vector3 GroundUp() => Flatten(-_camera.GlobalBasis.Z);

    private static Vector3 Flatten(Vector3 vector) => new Vector3(vector.X, 0f, vector.Z).Normalized();

    /// <summary>Moves the rig over the ground and keeps its centre on the map.</summary>
    private void MoveOverGround(Vector3 offset)
    {
        var world = _host.Simulation.World;
        var target = Position + offset;
        Position = new Vector3(
            Mathf.Clamp(target.X, 0f, world.WidthCells),
            0f,
            Mathf.Clamp(target.Z, 0f, world.HeightCells));
    }
}
