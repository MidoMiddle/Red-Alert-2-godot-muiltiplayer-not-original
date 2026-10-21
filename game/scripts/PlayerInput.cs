using Godot;
using Rts.Simulation.Geometry;

namespace Rts.Client;

/// <summary>
/// Debug controls of M0 (docs/implementation/M0_foundation.md, 4.11). Clicks become commands;
/// keys control the host (pause, step, speed, replays).
/// </summary>
public partial class PlayerInput : Node
{
    private SimulationHost _host = null!;
    private RtsCamera _camera = null!;

    public override void _Ready()
    {
        _host = GetNode<SimulationHost>("../SimulationHost");
        _camera = GetNode<RtsCamera>("../CameraRig");
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                HandleClick(click);
                break;
            case InputEventKey { Pressed: true, Echo: false } key:
                HandleKey(key.Keycode);
                break;
        }
    }

    private void HandleClick(InputEventMouseButton click)
    {
        if (_host.IsPlayback || !TryGetGroundPoint(click.Position, out var point))
        {
            return;
        }

        if (click.ShiftPressed)
        {
            _host.IssueSpawn(point);
        }
        else
        {
            _host.IssueMove(point);
        }
    }

    private void HandleKey(Key key)
    {
        switch (key)
        {
            case Key.P:
                _host.Paused = !_host.Paused;
                break;
            case Key.N when _host.Paused:
                _host.StepOnce();
                break;
            case Key.Equal or Key.Plus or Key.KpAdd:
                _host.ChangeSpeed(+1);
                break;
            case Key.Minus or Key.KpSubtract:
                _host.ChangeSpeed(-1);
                break;
            case Key.F5:
                _host.SaveReplay();
                break;
            case Key.F9:
                _host.StartPlayback();
                break;
            case Key.R:
                _host.StartLiveSession();
                break;
        }
    }

    /// <summary>Ground point under the cursor, kept only if it is inside the map.</summary>
    private bool TryGetGroundPoint(Vector2 screenPosition, out WorldPos point)
    {
        point = default;
        if (!_camera.TryScreenToGround(screenPosition, out var hit))
        {
            return false;
        }

        point = WorldCoords.FromGodot(hit);
        return _host.Simulation.World.Contains(point);
    }
}
