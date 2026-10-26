using Godot;
using Rts.Client.App;
using Rts.Client.Hud;

namespace Rts.Client;

/// <summary>
/// RA2 camera (docs/implementation/M1_visual_spike.md, 4.1): orthographic, yaw 45°, pitch 30° down,
/// so a 1×1 ground cell looks like a 2:1 diamond. World X runs right-down on screen, world Y (Godot Z) left-down.
/// The rig only slides over the ground; the angle never changes.
/// Moves with the camera key actions, with the mouse at the edge of the field (setting <c>EdgeScroll</c>) and with a drag
/// of the camera button that <see cref="PlayerInput"/> passes in (setting <c>CameraDrag</c>, docs/implementation/M6_menus_skirmish.md).
/// Zooms with the mouse wheel. The sidebar covers <see cref="ReservedRight"/> on the right, so the rig point sits in the middle of the field.
/// As in RA2 the view stops at the edge of the map: the field never goes past the frame of the map on screen. On a map with a
/// playable area (<see cref="Rts.Data.Maps.MapArea"/>) the frame is that rectangle, so the corners of the field are corners of
/// the battlefield; on a square map it is the bounding box of its diamond, and the triangles between the diamond and the frame
/// show the land beyond the edge (<see cref="Rts.Client.Terrain.MapSkirt"/>).
/// </summary>
public partial class RtsCamera : Node3D
{
    public const float YawDegrees = 45f;
    public const float PitchDegrees = 30f;

    /// <summary>Vertical view extent in world units: a cell is about 60 px wide at the 1080 px base height.</summary>
    public const float DefaultSize = 25.5f;

    private const float MinZoom = 0.7f;
    public const float MaxZoom = 1.6f;
    private const float ZoomStep = 1.12f;
    private const float ZoomSmoothing = 12f;
    private const float CameraDistance = 50f; // close enough that the ground stays well inside the sun shadow distance

    private const float ScrollSpeed = 14f; // cells per second at default zoom and the neutral speed setting
    private const float NeutralScrollSpeed = 5f; // the default of the scroll speed setting (1–10)
    private const float KeyAcceleration = 1.5f; // extra speed multiplier gained per second of holding
    private const float MaxKeyBoost = 2.5f;
    private const int EdgeBand = 8; // pixels

    private Camera3D _camera = null!;
    private System.Func<Rts.Simulation.Map.MapGrid> _map = null!;
    private System.Func<Rts.Client.Climates.MapLook> _look = null!;
    private float _targetSize = DefaultSize;
    private float _keyHoldTime;

    /// <summary>The ground point held under the pointer during a drag, or null when no drag is running.</summary>
    private Vector3? _dragAnchor;

    public Camera3D Camera => _camera;

    /// <summary>Width in design pixels of the strip on the right that the sidebar covers. The field is the view without it.</summary>
    public float ReservedRight { get; set; }

    /// <summary>Height in design pixels of the strip at the bottom that the command bar covers. The field is the view without it.</summary>
    public float ReservedBottom { get; set; }

    public override void _Ready()
    {
        _map = Rts.Client.Terrain.MapSources.Find(this);
        _look = Rts.Client.Terrain.MapSources.FindLook(this);
        var pivot = GetNode<Node3D>("Pivot");
        pivot.RotationDegrees = new Vector3(-PitchDegrees, YawDegrees, 0f);

        _camera = GetNode<Camera3D>("Pivot/Camera3D");
        _camera.Projection = Camera3D.ProjectionType.Orthogonal;
        _camera.Size = DefaultSize;
        _camera.Position = new Vector3(0f, 0f, CameraDistance);
        _camera.Near = 1f;
        _camera.Far = CameraDistance * 2f;

        Position = new Vector3(_map().Width / 2f, 0f, _map().Height / 2f);
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
        // A zoom out or a smaller window shows more: the view stays inside the frame of the map.
        Position = ClampToFrame(Position);
        // The rig point sits in the middle of the field: the view without the strips that the sidebar and the command bar cover.
        var (shiftX, shiftY) = CommandBarLayout.CentreOffset(ReservedRight, ReservedBottom, _camera.Size, GetViewport().GetVisibleRect().Size.Y);
        _camera.Position = new Vector3(shiftX, shiftY, CameraDistance);
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

    /// <summary>Zooms out as far as the wheel can, for the performance run with all units on screen.</summary>
    public void ZoomOutFully() => _targetSize = DefaultSize * MaxZoom;

    /// <summary>
    /// Point on the ground under a screen position: on the terrain when there is one, else on the plane y = 0 — or on the deck of a
    /// high bridge when the ray meets it first (<see cref="Rts.Client.Terrain.DeckPicking"/>).
    /// </summary>
    public bool TryGroundAt(Vector2 screen, out Vector3 ground)
    {
        ground = default;
        var origin = _camera.ProjectRayOrigin(screen);
        var direction = _camera.ProjectRayNormal(screen);
        if (direction.Y >= 0f)
        {
            return false;
        }

        if (WorldCoords.Terrain is { } terrain)
        {
            if (!terrain.TryIntersectRay(
                    new System.Numerics.Vector3(origin.X, origin.Y, origin.Z),
                    new System.Numerics.Vector3(direction.X, direction.Y, direction.Z),
                    out var hit))
            {
                return false;
            }

            ground = new Vector3(hit.X, hit.Y, hit.Z);
            if (WorldCoords.Map is { } map)
            {
                var from = new System.Numerics.Vector3(origin.X, origin.Y, origin.Z);
                var along = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(direction.X, direction.Y, direction.Z));
                if (Rts.Client.Terrain.DeckPicking.TryHit(map, from, along, System.Numerics.Vector3.Distance(from, hit), out var deck))
                {
                    ground = new Vector3(deck.X, deck.Y, deck.Z);
                }
            }

            return true;
        }

        ground = origin + direction * (-origin.Y / direction.Y);
        return true;
    }

    /// <summary>Screen position of a world point.</summary>
    public Vector2 UnprojectPosition(Vector3 world) => _camera.UnprojectPosition(world);

    /// <summary>The part of the view where the field shows: the view without the strip the sidebar covers.</summary>
    public Rect2 FieldRect()
    {
        var view = GetViewport().GetVisibleRect();
        return new Rect2(view.Position, new Vector2(view.Size.X - ReservedRight, view.Size.Y - ReservedBottom));
    }

    /// <summary>Moves the view to a ground point, for example to a click on the minimap.</summary>
    public void CenterOn(Vector3 ground) => MoveOverGround(new Vector3(ground.X, 0f, ground.Z) - Position);

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

        // At the edges of the window, as in RA2: the right one over the sidebar and the bottom one over the command bar too; the mouse
        // is held in the window while playing (MouseConfinement), so it never overshoots.
        var viewport = GetViewport();
        var window = viewport.GetVisibleRect();
        var mouse = viewport.GetMousePosition();
        if (!window.HasPoint(mouse))
        {
            return Vector2.Zero;
        }

        var velocity = Vector2.Zero;
        velocity.X -= EdgeStrength(mouse.X - window.Position.X);
        velocity.X += EdgeStrength(window.End.X - 1 - mouse.X);
        velocity.Y -= EdgeStrength(mouse.Y - window.Position.Y);
        velocity.Y += EdgeStrength(window.End.Y - 1 - mouse.Y);
        return velocity * ScrollSpeed;
    }

    // 0 outside the band, growing to 1 at the very edge.
    private static float EdgeStrength(float distanceToEdge) =>
        distanceToEdge >= EdgeBand ? 0f : 1f - Mathf.Max(0f, distanceToEdge) / EdgeBand;

    private Vector3 GroundRight() => Flatten(_camera.GlobalBasis.X);

    // Screen "up" on the ground: the camera's forward direction without its downward tilt.
    private Vector3 GroundUp() => Flatten(-_camera.GlobalBasis.Z);

    private static Vector3 Flatten(Vector3 vector) => new Vector3(vector.X, 0f, vector.Z).Normalized();

    /// <summary>Moves the rig over the ground and keeps the field inside the frame of the map.</summary>
    private void MoveOverGround(Vector3 offset) => Position = ClampToFrame(Position + offset);

    /// <summary>
    /// The rig point nearest to <paramref name="target"/> whose field stays inside the frame of the map on screen: along the
    /// screen's right and up the ground spans of the field (its pixels at the current size, the up span stretched by the pitch)
    /// must lie between the frame's corners that reach furthest that way. Where the field is wider than the frame, it is centred.
    /// </summary>
    private Vector3 ClampToFrame(Vector3 target)
    {
        var corners = FrameCorners();
        var (right, up) = (GroundRight(), GroundUp());
        var field = FieldRect().Size;
        var perPixel = _camera.Size / GetViewport().GetVisibleRect().Size.Y;
        var halfRight = field.X / 2f * perPixel;
        var halfUp = field.Y / 2f * perPixel / Mathf.Sin(Mathf.DegToRad(PitchDegrees));
        var (minRight, maxRight, minUp, maxUp) = (float.MaxValue, float.MinValue, float.MaxValue, float.MinValue);
        foreach (var corner in corners)
        {
            (minRight, maxRight) = (Mathf.Min(minRight, corner.Dot(right)), Mathf.Max(maxRight, corner.Dot(right)));
            (minUp, maxUp) = (Mathf.Min(minUp, corner.Dot(up)), Mathf.Max(maxUp, corner.Dot(up)));
        }

        var alongRight = Within(target.Dot(right), minRight + halfRight, maxRight - halfRight);
        var alongUp = Within(target.Dot(up), minUp + halfUp, maxUp - halfUp);
        return right * alongRight + up * alongUp;
    }

    /// <summary>
    /// The corners of the frame on the ground: those of the playable area — in the screen's axes <c>x = (u + v) / 2</c>,
    /// <c>z = (u − v) / 2</c>, its sides through the centres of its edge cells — or of the whole map.
    /// </summary>
    public Vector3[] FrameCorners()
    {
        if (_look() is { Playable: { } area })
        {
            Vector3 At(float u, float v) => new((u + v) / 2f, 0f, (u - v) / 2f);
            return [At(area.Top, area.Left), At(area.Top, area.Right), At(area.Bottom, area.Left), At(area.Bottom, area.Right)];
        }

        var map = _map();
        return [Vector3.Zero, new Vector3(map.Width, 0f, 0f), new Vector3(0f, 0f, map.Height), new Vector3(map.Width, 0f, map.Height)];
    }

    // Clamp, or the middle when the range is empty (the field is wider than the frame).
    private static float Within(float value, float min, float max) => min <= max ? Mathf.Clamp(value, min, max) : (min + max) / 2f;
}
