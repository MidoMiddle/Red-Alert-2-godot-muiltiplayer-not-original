using Godot;
using Rts.Client.App;

namespace Rts.Client;

/// <summary>
/// Temporary orthographic camera for M0: looks at the map from above at 45°. Moves with the camera keys (actions), with the
/// mouse at the window edge and with a drag of the camera button (both are settings), and zooms with the mouse wheel.
/// Replaced by the RA2 camera in M1.
/// </summary>
public partial class CameraController : Camera3D
{
    private const float MapSize = 64f;
    private const float PanSpeed = 20f;
    private const float MinSize = 8f;
    private const float MaxSize = 90f;
    private const float ZoomStep = 1.15f;

    /// <summary>Scroll speed of 5 (the default) pans at the base speed.</summary>
    private const float NeutralScrollSpeed = 5f;

    /// <summary>Pixels from the window edge within which the mouse pans the camera.</summary>
    private const float EdgeMargin = 12f;

    private static readonly Vector3 ViewOffset = new(0f, 40f, 40f);

    private Vector3 _focus = new(MapSize / 2, 0f, MapSize / 2);

    /// <summary>The ground point held under the pointer during a drag, or null when no drag is running.</summary>
    private Vector3? _dragAnchor;

    public override void _Ready()
    {
        Projection = ProjectionType.Orthogonal;
        Size = 48f;
        Near = 0.1f;
        Far = 500f;
        UpdateTransform();
    }

    public override void _Process(double delta)
    {
        var settings = AppSettings.Instance?.Values ?? SettingsValues.Defaults;
        var pan = Vector3.Zero;
        if (Input.IsActionPressed("camera_up")) pan.Z -= 1f;
        if (Input.IsActionPressed("camera_down")) pan.Z += 1f;
        if (Input.IsActionPressed("camera_left")) pan.X -= 1f;
        if (Input.IsActionPressed("camera_right")) pan.X += 1f;
        if (settings.EdgeScroll)
        {
            pan += EdgePan();
        }

        if (pan != Vector3.Zero)
        {
            // Pan speed follows the zoom so the screen moves at the same visual speed.
            var speed = PanSpeed * (Size / 48f) * (settings.ScrollSpeed / NeutralScrollSpeed) * (float)delta;
            _focus += pan.Normalized() * speed;
            _focus = _focus.Clamp(Vector3.Zero, new Vector3(MapSize, 0f, MapSize));
            UpdateTransform();
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton { Pressed: true } wheel)
        {
            if (wheel.ButtonIndex == MouseButton.WheelUp)
            {
                Size = Mathf.Max(MinSize, Size / ZoomStep);
            }
            else if (wheel.ButtonIndex == MouseButton.WheelDown)
            {
                Size = Mathf.Min(MaxSize, Size * ZoomStep);
            }
        }
    }

    /// <summary>The camera button went down at <paramref name="screen"/>: the ground point under the pointer is held from now on.</summary>
    public void BeginDrag(Vector2 screen)
    {
        if (TryGroundAt(screen, out var ground))
        {
            _dragAnchor = ground;
        }
        else
        {
            _dragAnchor = null;
        }
    }

    /// <summary>The pointer moved during a drag: the camera moves so that the held ground point stays under the pointer.</summary>
    public void DragTo(Vector2 screen)
    {
        if (_dragAnchor is not { } anchor || !TryGroundAt(screen, out var current))
        {
            return;
        }

        _focus += anchor - current;
        _focus = _focus.Clamp(Vector3.Zero, new Vector3(MapSize, 0f, MapSize));
        UpdateTransform();
    }

    public void EndDrag()
    {
        _dragAnchor = null;
    }

    /// <summary>Where the ray through a screen point meets the ground (y = 0). False when the ray does not go down.</summary>
    public bool TryGroundAt(Vector2 screen, out Vector3 ground)
    {
        ground = default;
        var origin = ProjectRayOrigin(screen);
        var direction = ProjectRayNormal(screen);
        if (direction.Y >= 0f)
        {
            return false;
        }

        ground = origin + direction * (-origin.Y / direction.Y);
        return true;
    }

    /// <summary>The pan the mouse asks for at the window edge. Nothing while the pointer is outside the window.</summary>
    private Vector3 EdgePan()
    {
        var viewport = GetViewport();
        var mouse = viewport.GetMousePosition();
        var visible = viewport.GetVisibleRect().Size;
        if (mouse.X < 0f || mouse.Y < 0f || mouse.X > visible.X || mouse.Y > visible.Y)
        {
            return Vector3.Zero;
        }

        var pan = Vector3.Zero;
        if (mouse.X <= EdgeMargin) pan.X -= 1f;
        else if (mouse.X >= visible.X - EdgeMargin) pan.X += 1f;
        if (mouse.Y <= EdgeMargin) pan.Z -= 1f;
        else if (mouse.Y >= visible.Y - EdgeMargin) pan.Z += 1f;
        return pan;
    }

    private void UpdateTransform()
    {
        Position = _focus + ViewOffset;
        LookAt(_focus, Vector3.Up);
    }
}
