using Godot;

namespace Rts.Client;

/// <summary>
/// RA2 camera (docs/implementation/M1_visual_spike.md, 4.1): orthographic, yaw 45°, pitch 30° down,
/// so a 1×1 ground cell looks like a 2:1 diamond. World X runs right-down on screen, world Y (Godot Z) left-down.
/// The rig only slides over the ground; the angle never changes.
/// Scroll: screen edges, WASD/arrows, holding the right mouse button (RA2 style); zoom: mouse wheel.
/// </summary>
public partial class RtsCamera : Node3D
{
    public const float YawDegrees = 45f;
    public const float PitchDegrees = 30f;

    /// <summary>Vertical view extent in world units: a cell is about 60 px wide at a 1080 px tall window.</summary>
    public const float DefaultSize = 25.5f;

    private const float MinZoom = 0.7f;
    private const float MaxZoom = 1.6f;
    private const float ZoomStep = 1.12f;
    private const float ZoomSmoothing = 12f;
    private const float CameraDistance = 100f;

    private const float ScrollSpeed = 14f; // cells per second at default zoom
    private const float KeyAcceleration = 1.5f; // extra speed multiplier gained per second of holding
    private const float MaxKeyBoost = 2.5f;
    private const int EdgeBand = 8; // pixels
    private const float DragSpeedPerPixel = 0.12f; // cells per second per pixel of drag offset
    private const float DragDeadZone = 4f;

    private Camera3D _camera = null!;
    private SimulationHost _host = null!;
    private float _targetSize = DefaultSize;
    private float _keyHoldTime;
    private Vector2? _dragAnchor;

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
        var screenVelocity = KeyboardScroll(dt) + EdgeScroll() + DragScroll();
        if (screenVelocity != Vector2.Zero)
        {
            var zoomScale = _camera.Size / DefaultSize;
            MoveOverGround(GroundRight() * screenVelocity.X * zoomScale * dt + GroundUp() * -screenVelocity.Y * zoomScale * dt);
        }

        _camera.Size = Mathf.Lerp(_camera.Size, _targetSize, 1f - Mathf.Exp(-ZoomSmoothing * dt));
        ClampToMap();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                _targetSize = Mathf.Max(DefaultSize * MinZoom, _targetSize / ZoomStep);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                _targetSize = Mathf.Min(DefaultSize * MaxZoom, _targetSize * ZoomStep);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right } right:
                _dragAnchor = right.Pressed ? right.Position : null;
                break;
        }
    }

    /// <summary>Point on the ground plane (y = 0) under a screen position. Terrain heights come in step 3.</summary>
    public bool TryScreenToGround(Vector2 screenPosition, out Vector3 point)
    {
        point = default;
        var origin = _camera.ProjectRayOrigin(screenPosition);
        var direction = _camera.ProjectRayNormal(screenPosition);
        if (direction.Y >= 0f)
        {
            return false;
        }

        point = origin + direction * (-origin.Y / direction.Y);
        return true;
    }

    // Screen-space velocity in cells per second: x to the right, y downwards.
    private Vector2 KeyboardScroll(float dt)
    {
        var direction = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) direction.Y -= 1f;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) direction.Y += 1f;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) direction.X -= 1f;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) direction.X += 1f;

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

    private Vector2 DragScroll()
    {
        if (_dragAnchor is not { } anchor)
        {
            return Vector2.Zero;
        }

        var offset = GetViewport().GetMousePosition() - anchor;
        return offset.Length() <= DragDeadZone ? Vector2.Zero : offset * DragSpeedPerPixel;
    }

    private Vector3 GroundRight() => Flatten(_camera.GlobalBasis.X);

    // Screen "up" on the ground: the camera's forward direction without its downward tilt.
    private Vector3 GroundUp() => Flatten(-_camera.GlobalBasis.Z);

    private static Vector3 Flatten(Vector3 vector) => new Vector3(vector.X, 0f, vector.Z).Normalized();

    private void MoveOverGround(Vector3 offset) => Position += offset;

    private void ClampToMap()
    {
        var world = _host.Simulation.World;
        Position = new Vector3(
            Mathf.Clamp(Position.X, 0f, world.WidthCells),
            0f,
            Mathf.Clamp(Position.Z, 0f, world.HeightCells));
    }
}
