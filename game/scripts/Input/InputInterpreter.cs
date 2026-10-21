using System;
using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Client.Controls;

/// <summary>The mouse buttons the game reads.</summary>
public enum PointerButton
{
    Left,
    Right,
    Middle,
}

/// <summary>What is under the cursor when a button goes down.</summary>
public enum PointerTargetKind
{
    Ground,
    OwnUnit,
    OtherUnit,
}

/// <summary>What is under the cursor. <see cref="Unit"/> means something only for the unit kinds.</summary>
public readonly record struct PointerTarget(PointerTargetKind Kind, EntityId Unit = default);

/// <summary>A point on the screen, in pixels.</summary>
public readonly record struct ScreenPoint(float X, float Y)
{
    public float DistanceTo(ScreenPoint other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}

/// <summary>What the interpreter knows about a press besides the button.</summary>
/// <param name="Ground">The point on the ground under the cursor, or null when the cursor is off the map.</param>
/// <param name="Target">What is under the cursor.</param>
/// <param name="Shift">Whether Shift is held: Shift and a left click spawn a test unit.</param>
/// <param name="HasSelection">Whether any unit is selected.</param>
public readonly record struct PointerContext(WorldPos? Ground, PointerTarget Target, bool Shift, bool HasSelection);

/// <summary>What the player wants. The game applies it; the interpreter knows nothing about units or the camera.</summary>
public abstract record Intent;

public sealed record SelectUnit(EntityId Unit) : Intent;

public sealed record ClearSelection : Intent;

public sealed record OrderMove(WorldPos Target) : Intent;

public sealed record SpawnUnit(WorldPos Position) : Intent;

public sealed record CameraDragStarted(ScreenPoint From) : Intent;

public sealed record CameraDragMoved(ScreenPoint To) : Intent;

public sealed record CameraDragEnded : Intent;

/// <summary>
/// Turns mouse events into intents for the chosen scheme (docs/implementation/M6_menus_skirmish.md, 4.6). The game reports
/// presses, releases and pointer moves with what is under the cursor, and the interpreter answers with intents. The same
/// events give different intents in Classic and Modern.
/// </summary>
public sealed class InputInterpreter
{
    /// <summary>Pixels the pointer may move before a press counts as a drag instead of a click.</summary>
    public const float ClickSlop = 6f;

    private readonly ControlScheme _scheme;
    private readonly bool _cameraDrag;

    /// <summary>The button whose press is followed until its release: the camera button, or the right button in Classic.</summary>
    private PointerButton? _tracked;
    private ScreenPoint _pressedAt;
    private bool _moved;
    private bool _dragging;

    public InputInterpreter(ControlScheme scheme, bool cameraDrag)
    {
        _scheme = scheme;
        _cameraDrag = cameraDrag;
    }

    /// <summary>The button that moves the camera: right in Classic, middle in Modern.</summary>
    private PointerButton CameraButton => _scheme == ControlScheme.Classic ? PointerButton.Right : PointerButton.Middle;

    /// <summary>A button went down at <paramref name="at"/>.</summary>
    public IReadOnlyList<Intent> Press(PointerButton button, ScreenPoint at, PointerContext context)
    {
        if (_tracked is not null)
        {
            return Array.Empty<Intent>();
        }

        if (context.Shift && button == PointerButton.Left)
        {
            return context.Ground is { } spawnAt && context.Target.Kind == PointerTargetKind.Ground
                ? new Intent[] { new SpawnUnit(spawnAt) }
                : Array.Empty<Intent>();
        }

        switch (button)
        {
            case PointerButton.Left:
                return PressLeft(context);
            case PointerButton.Right when _scheme == ControlScheme.Classic:
                return Track(button, at);
            case PointerButton.Right:
                return context.HasSelection && context.Ground is { } order
                    ? new Intent[] { new OrderMove(order) }
                    : Array.Empty<Intent>();
            case PointerButton.Middle when _scheme == ControlScheme.Modern:
                return Track(button, at);
            default:
                return Array.Empty<Intent>();
        }
    }

    /// <summary>A button went up. Ends a camera drag, or cancels the selection after a right click in Classic.</summary>
    public IReadOnlyList<Intent> Release(PointerButton button)
    {
        if (_tracked != button)
        {
            return Array.Empty<Intent>();
        }

        var dragging = _dragging;
        var click = !_moved;
        _tracked = null;
        _dragging = false;
        _moved = false;

        if (dragging)
        {
            return new Intent[] { new CameraDragEnded() };
        }

        return click && _scheme == ControlScheme.Classic && button == PointerButton.Right
            ? new Intent[] { new ClearSelection() }
            : Array.Empty<Intent>();
    }

    /// <summary>The pointer moved to <paramref name="at"/>.</summary>
    public IReadOnlyList<Intent> Move(ScreenPoint at)
    {
        if (_tracked is null)
        {
            return Array.Empty<Intent>();
        }

        if (_pressedAt.DistanceTo(at) > ClickSlop)
        {
            _moved = true;
        }

        if (_dragging)
        {
            return new Intent[] { new CameraDragMoved(at) };
        }

        if (_moved && _cameraDrag && _tracked == CameraButton)
        {
            _dragging = true;
            return new Intent[] { new CameraDragStarted(_pressedAt), new CameraDragMoved(at) };
        }

        return Array.Empty<Intent>();
    }

    /// <summary>The cancel key (Escape) was pressed.</summary>
    public IReadOnlyList<Intent> CancelSelection() => new Intent[] { new ClearSelection() };

    private IReadOnlyList<Intent> PressLeft(PointerContext context)
    {
        switch (context.Target.Kind)
        {
            case PointerTargetKind.OwnUnit:
                return new Intent[] { new SelectUnit(context.Target.Unit) };
            case PointerTargetKind.Ground when _scheme == ControlScheme.Modern:
                return context.Ground is null ? Array.Empty<Intent>() : new Intent[] { new ClearSelection() };
            case PointerTargetKind.Ground when context.HasSelection && context.Ground is { } order:
                return new Intent[] { new OrderMove(order) };
            default:
                return Array.Empty<Intent>();
        }
    }

    /// <summary>The camera button went down: follow it until it is released. Modern starts the drag at once, Classic after a move.</summary>
    private IReadOnlyList<Intent> Track(PointerButton button, ScreenPoint at)
    {
        _tracked = button;
        _pressedAt = at;
        _moved = false;
        _dragging = _scheme == ControlScheme.Modern && _cameraDrag;
        return _dragging ? new Intent[] { new CameraDragStarted(at) } : Array.Empty<Intent>();
    }
}
