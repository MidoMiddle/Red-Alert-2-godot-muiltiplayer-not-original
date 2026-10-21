using System.Collections.Generic;
using Godot;
using Rts.Client.App;
using Rts.Client.Controls;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Client;

/// <summary>
/// Player input of the debug game (docs/implementation/M6_menus_skirmish.md, 4.6). Mouse events go through the
/// <see cref="InputInterpreter"/> of the chosen scheme and come back as intents: select, order, cancel, camera drag. Keys are
/// actions of the key bindings, so the player can change them in the settings.
/// </summary>
public partial class PlayerInput : Node
{
    /// <summary>Screen distance in pixels within which a click picks a unit.</summary>
    private const float PickRadius = 22f;

    /// <summary>Height of the unit cubes: a click picks them at their middle.</summary>
    private const float UnitHeight = 0.25f;

    private SimulationHost _host = null!;
    private RtsCamera _camera = null!;
    private InputInterpreter _interpreter = null!;

    /// <summary>The units the player has selected. The unit renderer shows them.</summary>
    public SelectionSet Selection { get; } = new();

    public override void _Ready()
    {
        _host = GetNode<SimulationHost>("../SimulationHost");
        _camera = GetNode<RtsCamera>("../CameraRig");
        var settings = AppSettings.Instance?.Values ?? SettingsValues.Defaults;
        _interpreter = new InputInterpreter(settings.Scheme, settings.CameraDrag);
    }

    public override void _Process(double delta)
    {
        // A unit that died leaves the selection.
        Selection.RemoveWhere(unit => !_host.Simulation.World.TryGet(unit, out var entity) || !entity.IsAlive);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Right or MouseButton.Middle } mouse:
                HandleButton(mouse);
                break;
            case InputEventMouseMotion motion:
                Apply(_interpreter.Move(ToScreen(motion.Position)));
                break;
            case InputEventKey key:
                HandleKey(key);
                break;
        }
    }

    private void HandleButton(InputEventMouseButton mouse)
    {
        var pointer = ToPointerButton(mouse.ButtonIndex);
        var at = ToScreen(mouse.Position);
        var intents = mouse.Pressed
            ? _interpreter.Press(pointer, at, Context(at, mouse.ShiftPressed))
            : _interpreter.Release(pointer);
        Apply(intents);
    }

    private void HandleKey(InputEventKey key)
    {
        if (key.IsActionPressed("cancel_selection"))
        {
            Apply(_interpreter.CancelSelection());
        }
        else if (key.IsActionPressed("pause"))
        {
            _host.Paused = !_host.Paused;
        }
        else if (key.IsActionPressed("step") && _host.Paused)
        {
            _host.StepOnce();
        }
        else if (key.IsActionPressed("speed_up"))
        {
            _host.ChangeSpeed(+1);
        }
        else if (key.IsActionPressed("speed_down"))
        {
            _host.ChangeSpeed(-1);
        }
        else if (key.IsActionPressed("save_replay"))
        {
            _host.SaveReplay();
        }
        else if (key.IsActionPressed("load_replay"))
        {
            _host.StartPlayback();
        }
        else if (key.IsActionPressed("new_session"))
        {
            _host.StartLiveSession();
        }
    }

    /// <summary>Carries out the intents: the selection changes here, orders and spawns go to the host, drags to the camera.</summary>
    private void Apply(IEnumerable<Intent> intents)
    {
        foreach (var intent in intents)
        {
            if (intent is not CameraDragMoved)
            {
                GD.Print($"[input] {intent}");
            }

            switch (intent)
            {
                case SelectUnit select:
                    Selection.Replace(select.Unit);
                    break;
                case ClearSelection:
                    Selection.Clear();
                    break;
                case OrderMove order:
                    if (!_host.IsPlayback)
                    {
                        _host.IssueMove(Selection.Units, order.Target);
                    }

                    break;
                case SpawnUnit spawn:
                    if (!_host.IsPlayback)
                    {
                        _host.IssueSpawn(spawn.Position);
                    }

                    break;
                case CameraDragStarted started:
                    _camera.BeginDrag(ToVector(started.From));
                    break;
                case CameraDragMoved moved:
                    _camera.DragTo(ToVector(moved.To));
                    break;
                case CameraDragEnded:
                    _camera.EndDrag();
                    break;
            }
        }
    }

    private PointerContext Context(ScreenPoint at, bool shift) =>
        new(GroundAt(at), PickTarget(at), shift, !Selection.IsEmpty);

    /// <summary>The unit nearest to the pointer within <see cref="PickRadius"/>, or the ground when none is that close.</summary>
    private PointerTarget PickTarget(ScreenPoint at)
    {
        PointerTarget? nearest = null;
        var nearestDistance = PickRadius;
        foreach (var entity in _host.Simulation.World.Entities)
        {
            if (entity is not TestUnit { IsAlive: true } unit)
            {
                continue;
            }

            var screen = _camera.UnprojectPosition(WorldCoords.ToGodot(unit.Position, UnitHeight));
            var distance = at.DistanceTo(new ScreenPoint(screen.X, screen.Y));
            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearest = new PointerTarget(
                    unit.Owner == SimulationHost.LocalPlayer ? PointerTargetKind.OwnUnit : PointerTargetKind.OtherUnit,
                    unit.Id);
            }
        }

        return nearest ?? new PointerTarget(PointerTargetKind.Ground);
    }

    /// <summary>The point on the ground under the pointer, or null when the pointer is off the map.</summary>
    private WorldPos? GroundAt(ScreenPoint at)
    {
        if (!_camera.TryGroundAt(ToVector(at), out var hit))
        {
            return null;
        }

        var point = WorldCoords.FromGodot(hit);
        return _host.Simulation.World.Contains(point) ? point : null;
    }

    private static PointerButton ToPointerButton(MouseButton button) => button switch
    {
        MouseButton.Right => PointerButton.Right,
        MouseButton.Middle => PointerButton.Middle,
        _ => PointerButton.Left,
    };

    private static ScreenPoint ToScreen(Vector2 point) => new(point.X, point.Y);

    private static Vector2 ToVector(ScreenPoint point) => new(point.X, point.Y);
}
