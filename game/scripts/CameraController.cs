using Godot;

namespace Rts.Client;

/// <summary>
/// Temporary orthographic camera for M0: looks at the map from above at 45°, WASD/arrows pan,
/// mouse wheel zooms. Replaced by the RA2 camera in M1.
/// </summary>
public partial class CameraController : Camera3D
{
    private const float MapSize = 64f;
    private const float PanSpeed = 20f;
    private const float MinSize = 8f;
    private const float MaxSize = 90f;
    private const float ZoomStep = 1.15f;

    private static readonly Vector3 ViewOffset = new(0f, 40f, 40f);

    private Vector3 _focus = new(MapSize / 2, 0f, MapSize / 2);

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
        var pan = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) pan.Z -= 1f;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) pan.Z += 1f;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) pan.X -= 1f;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) pan.X += 1f;

        if (pan != Vector3.Zero)
        {
            // Pan speed follows the zoom so the screen moves at the same visual speed.
            _focus += pan.Normalized() * PanSpeed * (Size / 48f) * (float)delta;
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

    private void UpdateTransform()
    {
        Position = _focus + ViewOffset;
        LookAt(_focus, Vector3.Up);
    }
}
