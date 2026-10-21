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
/// <param name="Shift">Whether Shift is held: Shift and a left click add a unit to the selection or take it out, Shift and a box add.</param>
/// <param name="HasSelection">Whether any unit is selected.</param>
/// <param name="Alt">Whether Alt is held: Alt and a left click on the ground spawn a test unit (debug).</param>
/// <param name="Ctrl">Whether Ctrl (or Command) is held: Ctrl + Shift and an order give an attack-move.</param>
/// <param name="DoubleClick">Whether this press is the second one of a double click.</param>
/// <param name="WaypointMode">Whether the waypoint key (Z) is held: an order adds a waypoint instead.</param>
/// <param name="HasWaypoints">Whether waypoints are laid out and not sent yet: an order without Z sends them.</param>
public readonly record struct PointerContext(
    WorldPos? Ground,
    PointerTarget Target,
    bool Shift,
    bool HasSelection,
    bool Alt = false,
    bool Ctrl = false,
    bool DoubleClick = false,
    bool WaypointMode = false,
    bool HasWaypoints = false);

/// <summary>What the player wants. The game applies it; the interpreter knows nothing about units or the camera.</summary>
public abstract record Intent;

public sealed record SelectUnit(EntityId Unit) : Intent;

/// <summary>Add the unit to the selection, or take it out if it is selected (Shift + click).</summary>
public sealed record ToggleUnit(EntityId Unit) : Intent;

/// <summary>A selection box started at <paramref name="From"/>: the left button was dragged past <see cref="InputInterpreter.ClickSlop"/>.</summary>
public sealed record BoxStarted(ScreenPoint From) : Intent;

/// <summary>The selection box now spans from <paramref name="From"/> to <paramref name="To"/>.</summary>
public sealed record BoxMoved(ScreenPoint From, ScreenPoint To) : Intent;

/// <summary>The box was released: select own units inside it, or add them to the selection when <paramref name="Add"/> (Shift).</summary>
public sealed record BoxSelect(ScreenPoint From, ScreenPoint To, bool Add) : Intent;

/// <summary>The box was given up (Escape).</summary>
public sealed record BoxCancelled : Intent;

public sealed record ClearSelection : Intent;

/// <summary>Select every own unit of the clicked unit's type on screen (double click); add them when <paramref name="Add"/> (Shift).</summary>
public sealed record SelectSameType(EntityId Unit, bool Add) : Intent;

/// <summary>Move the selection; <paramref name="Queue"/> (Shift) adds the point to the end of the route.</summary>
public sealed record OrderMove(WorldPos Target, bool Queue = false) : Intent;

/// <summary>Attack-move the selection (Ctrl + Shift and an order).</summary>
public sealed record OrderAttackMove(WorldPos Target) : Intent;

/// <summary>Lay out one more waypoint (an order with Z held); nothing is sent yet.</summary>
public sealed record AddWaypoint(WorldPos Point) : Intent;

/// <summary>The last waypoint (an order without Z after waypoints): send the route.</summary>
public sealed record FinishWaypoints(WorldPos Point) : Intent;

/// <summary>Drop the waypoints laid out so far (Escape).</summary>
public sealed record CancelWaypoints : Intent;

public sealed record SpawnUnit(WorldPos Position) : Intent;

public sealed record CameraDragStarted(ScreenPoint From) : Intent;

public sealed record CameraDragMoved(ScreenPoint To) : Intent;

public sealed record CameraDragEnded : Intent;

public sealed record OpenMenu : Intent;

/// <summary>
/// Turns mouse events into intents for the chosen scheme (docs/implementation/M6_menus_skirmish.md, 4.6). The game reports
/// presses, releases and pointer moves with what is under the cursor, and the interpreter answers with intents. The same
/// events give different intents in Classic and Modern.
/// The left button is followed until it is released (M2 step 12): released in place it is a click (select, Shift + click to
/// add or remove, order or cancel on the ground); dragged past <see cref="ClickSlop"/> it draws a selection box.
/// Orders follow RA2 in both schemes (M2 step 13, docs/implementation/M2_unit_control.md, 4.14): Shift queues a move,
/// Ctrl + Shift attack-moves, Z lays out waypoints and the next order without Z sends them; a double click selects the type.
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
    private ScreenPoint _lastAt;
    private bool _moved;
    private bool _dragging;
    private bool _boxing;
    private PointerContext _leftContext;

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

        if (context.Alt && button == PointerButton.Left)
        {
            return context.Ground is { } spawnAt && context.Target.Kind == PointerTargetKind.Ground
                ? new Intent[] { new SpawnUnit(spawnAt) }
                : Array.Empty<Intent>();
        }

        switch (button)
        {
            case PointerButton.Left:
                // Click or box: decided when the button is released or the pointer moves away.
                _tracked = button;
                _pressedAt = at;
                _lastAt = at;
                _moved = false;
                _boxing = false;
                _leftContext = context;
                return Array.Empty<Intent>();
            case PointerButton.Right when _scheme == ControlScheme.Classic:
                return Track(button, at);
            case PointerButton.Right:
                return Order(context);
            case PointerButton.Middle when _scheme == ControlScheme.Modern:
                return Track(button, at);
            default:
                return Array.Empty<Intent>();
        }
    }

    /// <summary>
    /// A button went up. The left button finishes its click or box; the camera button ends a drag, or cancels the selection
    /// after a right click in Classic.
    /// </summary>
    public IReadOnlyList<Intent> Release(PointerButton button)
    {
        if (_tracked != button)
        {
            return Array.Empty<Intent>();
        }

        var dragging = _dragging;
        var boxing = _boxing;
        var click = !_moved;
        _tracked = null;
        _dragging = false;
        _boxing = false;
        _moved = false;

        if (button == PointerButton.Left)
        {
            return boxing
                ? new Intent[] { new BoxSelect(_pressedAt, _lastAt, _leftContext.Shift) }
                : ClickLeft(_leftContext);
        }

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

        if (_tracked == PointerButton.Left)
        {
            _lastAt = at;
            if (_boxing)
            {
                return new Intent[] { new BoxMoved(_pressedAt, at) };
            }

            if (!_moved)
            {
                return Array.Empty<Intent>();
            }

            _boxing = true;
            return new Intent[] { new BoxStarted(_pressedAt), new BoxMoved(_pressedAt, at) };
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

    /// <summary>
    /// The cancel key (Escape) was pressed. With units selected it clears the selection. Without them it opens the pause menu,
    /// which takes all input, so a button still held down is never released in the game: the press is dropped here.
    /// </summary>
    public IReadOnlyList<Intent> EscapePressed(bool hasSelection, bool hasWaypoints = false)
    {
        if (hasWaypoints && _tracked is null)
        {
            // Escape first drops the waypoints laid out so far; the selection stays.
            return new Intent[] { new CancelWaypoints() };
        }

        if (_tracked == PointerButton.Left)
        {
            // Escape gives up a pressed left button; a box being drawn disappears.
            var boxing = _boxing;
            _tracked = null;
            _boxing = false;
            _moved = false;
            if (boxing)
            {
                return new Intent[] { new BoxCancelled() };
            }
        }

        if (hasSelection)
        {
            return new Intent[] { new ClearSelection() };
        }

        var intents = new List<Intent>();
        if (_dragging)
        {
            intents.Add(new CameraDragEnded());
        }

        _tracked = null;
        _dragging = false;
        _moved = false;
        intents.Add(new OpenMenu());
        return intents;
    }

    /// <summary>A left click released in place.</summary>
    private IReadOnlyList<Intent> ClickLeft(PointerContext context)
    {
        if (_scheme == ControlScheme.Classic && IsGroundOrder(context))
        {
            // Z or Ctrl + Shift give a point on the ground even over a unit.
            return Order(context);
        }

        switch (context.Target.Kind)
        {
            case PointerTargetKind.OwnUnit when context.DoubleClick:
                return new Intent[] { new SelectSameType(context.Target.Unit, context.Shift) };
            case PointerTargetKind.OwnUnit when context.Shift:
                return new Intent[] { new ToggleUnit(context.Target.Unit) };
            case PointerTargetKind.OwnUnit:
                return new Intent[] { new SelectUnit(context.Target.Unit) };
            case PointerTargetKind.Ground when _scheme == ControlScheme.Modern:
                return context.Ground is null ? Array.Empty<Intent>() : new Intent[] { new ClearSelection() };
            case PointerTargetKind.Ground:
                return Order(context);
            default:
                return Array.Empty<Intent>();
        }
    }

    /// <summary>The order modifiers that make a click a point on the ground whatever is under the cursor.</summary>
    private static bool IsGroundOrder(PointerContext context) =>
        context.HasSelection && (context.WaypointMode || (context.Ctrl && context.Shift));

    /// <summary>An order on the ground with the scheme's order button: waypoints, attack-move, a queued or a plain move.</summary>
    private static IReadOnlyList<Intent> Order(PointerContext context)
    {
        if (!context.HasSelection || context.Ground is not { } point)
        {
            return Array.Empty<Intent>();
        }

        Intent intent = context switch
        {
            { WaypointMode: true } => new AddWaypoint(point),
            { HasWaypoints: true } => new FinishWaypoints(point),
            { Ctrl: true, Shift: true } => new OrderAttackMove(point),
            _ => new OrderMove(point, context.Shift),
        };
        return new Intent[] { intent };
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
